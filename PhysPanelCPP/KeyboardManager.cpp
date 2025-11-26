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
#include <dwmapi.h>
#pragma comment(lib, "dwmapi.lib")

std::string WstringToString(const std::wstring& wstr) {
    if (wstr.empty()) return std::string();
    int size_needed = WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)wstr.size(), NULL, 0, NULL, NULL);
    std::string strTo(size_needed, 0);
    WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)wstr.size(), &strTo[0], size_needed, NULL, NULL);
    return strTo;
}

namespace KeyboardManager {

    void LogDebug(const wchar_t* format, ...) {
#if defined(_DEBUG)
        va_list args;
        va_start(args, format);
        vwprintf(format, args);
        wprintf(L"\n");
        va_end(args);
#else
        UNREFERENCED_PARAMETER(format);
#endif
    }

    struct __declspec(uuid("4CE576FA-83DC-4F88-951C-9D0782B4E376")) TipInvocation;
    struct __declspec(uuid("37c994e7-432b-4834-a2f7-dce1f13b834b")) ITipInvocation : IUnknown {
        virtual HRESULT __stdcall Toggle(HWND hwnd) = 0;
    };

    constexpr auto TABTIP_PROCESS_NAME = L"TabTip.exe";
    constexpr auto SHELL_PROCESS_NAME = L"explorer.exe";
    const auto SHELL_READY_TIMEOUT = std::chrono::seconds(30);
    const auto COM_SERVICE_TIMEOUT = std::chrono::seconds(10);
    const auto POST_LAUNCH_DELAY = std::chrono::seconds(7);

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
        LogDebug(L"[Debug] Waiting for process: %s", processName);
        auto start = std::chrono::high_resolution_clock::now();
        while (std::chrono::high_resolution_clock::now() - start < timeout) {
            if (IsProcessRunning(processName)) {
                LogDebug(L"[Debug] Process found: %s", processName);
                return true;
            }
            std::this_thread::sleep_for(std::chrono::milliseconds(500));
        }
        LogDebug(L"[Debug] Wait for process TIMEOUT: %s", processName);
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

        LogDebug(L"[Debug] Poll Check: Keyboard is CONFIRMED VISIBLE (Host HWND=%p).", parentHwnd);
        return true;
    }

    void StartTouchKeyboard() {
        LogDebug(L"--- StartTouchKeyboard() ---");

        if (IsProcessRunning(TABTIP_PROCESS_NAME)) {
            LogDebug(L"[Debug] TabTip.exe is already running. Exiting.");
            return;
        }

        PWSTR pszPath = nullptr;
        HRESULT hr_path = SHGetKnownFolderPath(FOLDERID_ProgramFilesCommon, 0, NULL, &pszPath);
        if (FAILED(hr_path)) {
            LogDebug(L"[Debug] FAILED to get FOLDERID_ProgramFilesCommon.");
            throw TabTipNotFoundException("Could not get Common Program Files path.");
        }

        std::wstring tabTipPath(pszPath);
        CoTaskMemFree(pszPath);
        tabTipPath += L"\\Microsoft Shared\\ink\\TabTip.exe";
        LogDebug(L"[Debug] TabTip path: %s", tabTipPath.c_str());

        DWORD fileAttr = GetFileAttributesW(tabTipPath.c_str());
        if (fileAttr == INVALID_FILE_ATTRIBUTES || (fileAttr & FILE_ATTRIBUTE_DIRECTORY)) {
            LogDebug(L"[Debug] TabTip.exe not found at path.");
            throw TabTipNotFoundException("TabTip.exe not found at its expected path.");
        }

        bool comInitialized = false;
        try {
            if (!WaitForProcess(SHELL_PROCESS_NAME, SHELL_READY_TIMEOUT)) {
                throw TabTipActivationException("Timed out waiting for Windows Shell (explorer.exe).");
            }

            LogDebug(L"[Debug] Executing ShellExecuteW to open TabTip.exe...");
            ShellExecuteW(NULL, L"open", tabTipPath.c_str(), NULL, NULL, SW_SHOWNORMAL);

            LogDebug(L"[Debug] Starting post-launch delay...");
            std::this_thread::sleep_for(POST_LAUNCH_DELAY);
            LogDebug(L"[Debug] Post-launch delay finished.");

            LogDebug(L"[Debug] Starting visibility poll (max 10s)...");
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
                LogDebug(L"[Debug] Initializing COM for Toggle (Close)...");
                HRESULT hr = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
                if (FAILED(hr)) {
                    LogDebug(L"[Debug] CoInitializeEx FAILED.");
                    throw TabTipActivationException("Failed to initialize COM for Toggle.");
                }
                comInitialized = true;

                LogDebug(L"[Debug] Attempting to connect to COM service to Toggle (Close)...");
                ITipInvocation* pTip = nullptr;
                auto start = std::chrono::high_resolution_clock::now();
                while (std::chrono::high_resolution_clock::now() - start < COM_SERVICE_TIMEOUT) {
                    hr = CoCreateInstance(__uuidof(TipInvocation), NULL, CLSCTX_LOCAL_SERVER, __uuidof(ITipInvocation), (void**)&pTip);
                    if (SUCCEEDED(hr)) break;
                    std::this_thread::sleep_for(std::chrono::milliseconds(250));
                }

                if (pTip) {
                    LogDebug(L"[Debug] COM service connected. Invoking Toggle() to CLOSE keyboard.");
                    pTip->Toggle(GetShellWindow());
                    pTip->Release();
                }
                else {
                    LogDebug(L"[Debug] FAILED to connect to COM service (pTip is null).");
                    throw TabTipActivationException("Timed out waiting for TabTip COM service (after keyboard was visible).");
                }
            }
            else {
                LogDebug(L"[Debug] POLL TIMEOUT: Keyboard did NOT become visible. Skipping Toggle (close).");
            }

            if (comInitialized) {
                LogDebug(L"[Debug] CoUninitialize.");
                CoUninitialize();
            }
        }
        catch (const _com_error& e) {
            if (comInitialized) CoUninitialize();
            throw TabTipActivationException(WstringToString(e.ErrorMessage()));
        }
        catch (const TabTipActivationException& e) {
            UNREFERENCED_PARAMETER(e);
            if (comInitialized) CoUninitialize();
            throw;
        }
        catch (...) {
            LogDebug(L"[Debug] EXCEPTION (Unknown).");
            if (comInitialized) CoUninitialize();
            throw;
        }

        LogDebug(L"--- StartTouchKeyboard() Finished ---");
    }
}