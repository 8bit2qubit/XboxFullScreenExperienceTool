// Xbox Fullscreen Experience Tool
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

std::string WstringToString(const std::wstring& wstr) {
    if (wstr.empty()) return std::string();
    int size_needed = WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)wstr.size(), NULL, 0, NULL, NULL);
    std::string strTo(size_needed, 0);
    WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)wstr.size(), &strTo[0], size_needed, NULL, NULL);
    return strTo;
}

namespace KeyboardManager {

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
        auto start = std::chrono::high_resolution_clock::now();
        while (std::chrono::high_resolution_clock::now() - start < timeout) {
            if (IsProcessRunning(processName)) {
                return true;
            }
            std::this_thread::sleep_for(std::chrono::milliseconds(500));
        }
        return false;
    }

    void StartTouchKeyboard() {
        if (IsProcessRunning(TABTIP_PROCESS_NAME)) {
            return;
        }

        PWSTR pszPath = nullptr;
        HRESULT hr_path = SHGetKnownFolderPath(FOLDERID_ProgramFilesCommon, 0, NULL, &pszPath);
        if (FAILED(hr_path)) {
            throw TabTipNotFoundException("Could not get Common Program Files path.");
        }

        std::wstring tabTipPath(pszPath);
        CoTaskMemFree(pszPath);
        tabTipPath += L"\\Microsoft Shared\\ink\\TabTip.exe";

        DWORD fileAttr = GetFileAttributesW(tabTipPath.c_str());
        if (fileAttr == INVALID_FILE_ATTRIBUTES || (fileAttr & FILE_ATTRIBUTE_DIRECTORY)) {
            throw TabTipNotFoundException("TabTip.exe not found at its expected path.");
        }

        try {
            if (!WaitForProcess(SHELL_PROCESS_NAME, SHELL_READY_TIMEOUT)) {
                throw TabTipActivationException("Timed out waiting for Windows Shell (explorer.exe).");
            }

            ShellExecuteW(NULL, L"open", tabTipPath.c_str(), NULL, NULL, SW_SHOWNORMAL);

            std::this_thread::sleep_for(POST_LAUNCH_DELAY);

            HRESULT hr = CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
            if (FAILED(hr)) {
                throw TabTipActivationException("Failed to initialize COM.");
            }

            ITipInvocation* pTip = nullptr;
            auto start = std::chrono::high_resolution_clock::now();
            while (std::chrono::high_resolution_clock::now() - start < COM_SERVICE_TIMEOUT) {
                hr = CoCreateInstance(__uuidof(TipInvocation), NULL, CLSCTX_LOCAL_SERVER, __uuidof(ITipInvocation), (void**)&pTip);
                if (SUCCEEDED(hr)) break;
                std::this_thread::sleep_for(std::chrono::milliseconds(250));
            }

            if (pTip) {
                pTip->Toggle(GetDesktopWindow());
                pTip->Release();
            }
            else {
                CoUninitialize();
                throw TabTipActivationException("Timed out waiting for TabTip COM service.");
            }

            CoUninitialize();
        }
        catch (const _com_error& e) {
            std::wstring wmessage = L"A COM error occurred: ";
            wmessage += e.ErrorMessage();
            throw TabTipActivationException(WstringToString(wmessage));
        }
        catch (...) {
            throw;
        }
    }
}