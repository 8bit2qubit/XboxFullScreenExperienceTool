// Xbox Full Screen Experience Tool
// Copyright (C) 2025 8bit2qubit

// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

#include "pch.h"
#include "KeyboardManager.h"
#include <fstream>
#include <dwmapi.h>
#pragma comment(lib, "dwmapi.lib")

class tee_buf : public std::wstreambuf {
public:
    tee_buf(std::wstreambuf* buf1, std::wstreambuf* buf2)
        : m_buf1(buf1), m_buf2(buf2) {
    }

private:
    std::streamsize xsputn(const wchar_t* s, std::streamsize n) override {
        std::streamsize res1 = m_buf1->sputn(s, n);
        std::streamsize res2 = m_buf2->sputn(s, n);
        return (std::min)(res1, res2);
    }

    int_type overflow(int_type c) override {
        if (c == std::wstreambuf::traits_type::eof()) {
            return std::wstreambuf::traits_type::eof();
        }
        int_type res1 = m_buf1->sputc(c);
        int_type res2 = m_buf2->sputc(c);
        if (res1 == std::wstreambuf::traits_type::eof() || res2 == std::wstreambuf::traits_type::eof()) {
            return std::wstreambuf::traits_type::eof();
        }
        return c;
    }

    int sync() override {
        int res1 = m_buf1->pubsync();
        int res2 = m_buf2->pubsync();
        return (res1 == 0 && res2 == 0) ? 0 : -1;
    }

    std::wstreambuf* m_buf1;
    std::wstreambuf* m_buf2;
};

std::string WstringToString(const std::wstring& wstr) {
    if (wstr.empty()) return std::string();
    int size_needed = WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)wstr.size(), NULL, 0, NULL, NULL);
    std::string strTo(size_needed, 0);
    WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)wstr.size(), &strTo[0], size_needed, NULL, NULL);
    return strTo;
}

namespace KeyboardManager {

    constexpr bool DEBUG_LOGGING_ENABLED = false;

    static std::wofstream g_logFile;
    static std::unique_ptr<tee_buf> g_teeBuf;
    static std::wstreambuf* g_originalConsoleBuf = nullptr;

    void InitDebugConsole() {
        if (!DEBUG_LOGGING_ENABLED) return;

        if (GetConsoleWindow() == NULL) {
            AllocConsole();

            FILE* pFile;
            freopen_s(&pFile, "CONOUT$", "w", stdout);
            freopen_s(&pFile, "CONOUT$", "w", stderr);
            freopen_s(&pFile, "CONIN$", "r", stdin);

            std::cout.clear();
            std::cerr.clear();
            std::cin.clear();
            std::wcout.clear();
            std::wcerr.clear();
            std::wcin.clear();

            std::locale::global(std::locale(""));
            std::wcout.imbue(std::locale(""));
            std::cout.imbue(std::locale(""));

            g_logFile.open("XFSET_KeyboardManager_Log.txt", std::ios::out | std::ios::trunc);
            if (g_logFile.is_open()) {
                g_logFile.imbue(std::locale(""));
                g_originalConsoleBuf = std::wcout.rdbuf();
                std::wstreambuf* fileBuf = g_logFile.rdbuf();
                g_teeBuf = std::make_unique<tee_buf>(g_originalConsoleBuf, fileBuf);
                std::wcout.rdbuf(g_teeBuf.get());
            }

            std::wcout << L"--- Debug Console Initialized (Logging to XFSET_KeyboardManager_Log.txt) ---" << std::endl;
        }
    }

    struct __declspec(uuid("4CE576FA-83DC-4F88-951C-9D0782B4E376")) TipInvocation;
    struct __declspec(uuid("37c994e7-432b-4834-a2f7-dce1f13b834b")) ITipInvocation : IUnknown {
        virtual HRESULT __stdcall Toggle(HWND hwnd) = 0;
    };

    constexpr auto TABTIP_PROCESS_NAME = L"TabTip.exe";
    constexpr auto SHELL_PROCESS_NAME = L"explorer.exe";
    const auto SHELL_READY_TIMEOUT = std::chrono::seconds(30);
    const auto COM_SERVICE_TIMEOUT = std::chrono::seconds(10);
    const auto POST_LAUNCH_DELAY = std::chrono::seconds(5);

    bool IsProcessRunning(const wchar_t* processName) {
        HANDLE snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == INVALID_HANDLE_VALUE) return false;
        PROCESSENTRY32W entry;
        entry.dwSize = sizeof(entry);
        bool found = false;
        if (Process32FirstW(snapshot, &entry)) {
            do {
                if (_wcsicmp(entry.szExeFile, processName) == 0) {
                    found = true;
                    break;
                }
            } while (Process32NextW(snapshot, &entry));
        }
        CloseHandle(snapshot);
        return found;
    }

    bool WaitForProcess(const wchar_t* processName, std::chrono::milliseconds timeout) {
        if (DEBUG_LOGGING_ENABLED) std::wcout << L"[Debug] Waiting for process: " << processName << std::endl;
        auto start = std::chrono::high_resolution_clock::now();
        while (std::chrono::high_resolution_clock::now() - start < timeout) {
            if (IsProcessRunning(processName)) {
                if (DEBUG_LOGGING_ENABLED) std::wcout << L"[Debug] Process found: " << processName << std::endl;
                return true;
            }
            std::this_thread::sleep_for(std::chrono::milliseconds(500));
        }
        if (DEBUG_LOGGING_ENABLED) std::wcout << L"[Debug] Wait for process TIMEOUT: " << processName << std::endl;
        return false;
    }

    bool IsTouchKeyboardVisible() {
        const wchar_t* PARENT_CLASS_NAME = L"ApplicationFrameWindow";
        HWND parentHwnd = FindWindowW(PARENT_CLASS_NAME, NULL);
        if (parentHwnd == NULL) return false;

        BOOL isCloaked = FALSE;
        HRESULT hr = DwmGetWindowAttribute(parentHwnd, DWMWA_CLOAKED, &isCloaked, sizeof(isCloaked));
        if (SUCCEEDED(hr) && isCloaked) return false;

        if (!IsWindowVisible(parentHwnd) || IsIconic(parentHwnd)) return false;

        if (DEBUG_LOGGING_ENABLED) std::wcout << L"[Debug] Poll Check: Keyboard is CONFIRMED VISIBLE (Host HWND=" << (void*)parentHwnd << L")." << std::endl;
        return true;
    }

    void StartTouchKeyboard() {
        InitDebugConsole();
        if (DEBUG_LOGGING_ENABLED) std::wcout << L"--- StartTouchKeyboard() ---" << std::endl;

        if (IsProcessRunning(TABTIP_PROCESS_NAME)) {
            if (DEBUG_LOGGING_ENABLED) std::wcout << L"[Debug] TabTip.exe is already running. Exiting." << std::endl;
            return;
        }

        PWSTR pszPath = nullptr;
        HRESULT hr_path = SHGetKnownFolderPath(FOLDERID_ProgramFilesCommon, 0, NULL, &pszPath);
        if (FAILED(hr_path)) {
            if (DEBUG_LOGGING_ENABLED) std::wcout << L"[Debug] FAILED to get FOLDERID_ProgramFilesCommon." << std::endl;
            throw TabTipNotFoundException("Could not get Common Program Files path.");
        }

        std::wstring tabTipPath(pszPath);
        CoTaskMemFree(pszPath);
        tabTipPath += L"\\Microsoft Shared\\ink\\TabTip.exe";
        if (DEBUG_LOGGING_ENABLED) std::wcout << L"[Debug] TabTip path: " << tabTipPath << std::endl;

        DWORD fileAttr = GetFileAttributesW(tabTipPath.c_str());
        if (fileAttr == INVALID_FILE_ATTRIBUTES || (fileAttr & FILE_ATTRIBUTE_DIRECTORY)) {
            if (DEBUG_LOGGING_ENABLED) std::wcout << L"[Debug] TabTip.exe not found at path." << std::endl;
            throw TabTipNotFoundException("TabTip.exe not found at its expected path.");
        }

        bool comInitialized = false;
        try {
            if (!WaitForProcess(SHELL_PROCESS_NAME, SHELL_READY_TIMEOUT)) {
                throw TabTipActivationException("Timed out waiting for Windows Shell (explorer.exe).");
            }

            if (DEBUG_LOGGING_ENABLED) std::wcout << L"[Debug] Executing ShellExecuteW to open TabTip.exe..." << std::endl;
            ShellExecuteW(NULL, L"open", tabTipPath.c_str(), NULL, NULL, SW_SHOWNORMAL);

            if (DEBUG_LOGGING_ENABLED) std::wcout << L"[Debug] Starting 5-second post-launch delay..." << std::endl;
            std::this_thread::sleep_for(POST_LAUNCH_DELAY);
            if (DEBUG_LOGGING_ENABLED) std::wcout << L"[Debug] Post-launch delay finished." << std::endl;

            if (DEBUG_LOGGING_ENABLED) std::wcout << L"[Debug] Starting visibility poll (max 10s)..." << std::endl;
            bool visible = false;
            auto pollStart = std::chrono::high_resolution_clock::now();

            while (std::chrono::high_resolution_clock::now() - pollStart < COM_SERVICE_TIMEOUT) {
                if (IsTouchKeyboardVisible()) {
                    visible = true;
                    break;
                }
                std::this_thread::sleep_for(std::chrono::milliseconds(250));
            }

            if (visible) {
                if (DEBUG_LOGGING_ENABLED) std::wcout << L"[Debug] Initializing COM for Toggle (Close)..." << std::endl;
                HRESULT hr = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
                if (FAILED(hr)) {
                    if (DEBUG_LOGGING_ENABLED) std::wcout << L"[Debug] CoInitializeEx FAILED." << std::endl;
                    throw TabTipActivationException("Failed to initialize COM for Toggle.");
                }
                comInitialized = true;

                if (DEBUG_LOGGING_ENABLED) std::wcout << L"[Debug] Attempting to connect to COM service to Toggle (Close)..." << std::endl;
                ITipInvocation* pTip = nullptr;
                auto start = std::chrono::high_resolution_clock::now();
                while (std::chrono::high_resolution_clock::now() - start < COM_SERVICE_TIMEOUT) {
                    hr = CoCreateInstance(__uuidof(TipInvocation), NULL, CLSCTX_LOCAL_SERVER, __uuidof(ITipInvocation), (void**)&pTip);
                    if (SUCCEEDED(hr)) break;
                    std::this_thread::sleep_for(std::chrono::milliseconds(250));
                }

                if (pTip) {
                    if (DEBUG_LOGGING_ENABLED) std::wcout << L"[Debug] COM service connected. Invoking Toggle() to CLOSE keyboard." << std::endl;
                    pTip->Toggle(GetShellWindow());
                    pTip->Release();
                }
                else {
                    if (DEBUG_LOGGING_ENABLED) std::wcout << L"[Debug] FAILED to connect to COM service (pTip is null)." << std::endl;
                    throw TabTipActivationException("Timed out waiting for TabTip COM service (after keyboard was visible).");
                }
            }
            else {
                if (DEBUG_LOGGING_ENABLED) std::wcout << L"[Debug] POLL TIMEOUT: Keyboard did NOT become visible (ApplicationFrameWindow not found or was cloaked/hidden). Skipping Toggle (close)." << std::endl;
            }

            if (comInitialized) {
                if (DEBUG_LOGGING_ENABLED) std::wcout << L"[Debug] CoUninitialize." << std::endl;
                CoUninitialize();
            }
        }
        catch (const _com_error& e) {
            if (DEBUG_LOGGING_ENABLED) {
                std::wstring wmessage = L"A COM error occurred: ";
                wmessage += e.ErrorMessage();
                std::wcout << L"[Debug] EXCEPTION: " << wmessage << std::endl;
            }
            if (comInitialized) CoUninitialize();
            throw TabTipActivationException(WstringToString(e.ErrorMessage()));
        }
        catch (const TabTipActivationException& e) {
            if (DEBUG_LOGGING_ENABLED) std::wcout << "[Debug] EXCEPTION (TabTipActivationException): " << e.what() << std::endl;
            if (comInitialized) CoUninitialize();
            throw;
        }
        catch (...) {
            if (DEBUG_LOGGING_ENABLED) std::wcout << L"[Debug] EXCEPTION (Unknown)." << std::endl;
            if (comInitialized) CoUninitialize();
            throw;
        }

        if (DEBUG_LOGGING_ENABLED) {
            std::wcout << L"--- StartTouchKeyboard() Finished ---" << std::endl;
            g_logFile.flush();
            g_logFile.close();
        }
    }
}