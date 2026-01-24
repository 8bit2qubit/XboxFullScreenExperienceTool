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
#include "TouchManager.h"
#include <wtsapi32.h>
#include <userenv.h>
#include <string>
#include <vector>
#include "Utils.h"

#pragma comment(lib, "Wtsapi32.lib")
#pragma comment(lib, "Userenv.lib")

namespace TouchManager {

    const LPCWSTR MASTER_MUTEX_NAME = L"Global\\XFEST_TouchSvc_Master_Lock";

    typedef BOOL(NTAPI* PInitializeTouchInjection)(UINT32, DWORD);

    std::wstring BuildMutexName(DWORD sessionId, const std::wstring& desktopPathOrName) {
        std::wstring shortName = desktopPathOrName;
        size_t lastSlash = shortName.find_last_of(L'\\');
        if (lastSlash != std::wstring::npos) {
            shortName = shortName.substr(lastSlash + 1);
        }

        for (auto& c : shortName) c = towlower(c);

        return L"Global\\XFEST_TouchSvc_Worker_" + std::to_wstring(sessionId) + L"_" + shortName;
    }

    void RunTouchLogic() {
        WCHAR szDesktopName[128] = { 0 };
        HDESK hDesk = GetThreadDesktop(GetCurrentThreadId());
        DWORD len = 0;
        GetUserObjectInformationW(hDesk, UOI_NAME, szDesktopName, sizeof(szDesktopName), &len);

        DWORD sessionId = 0;
        ProcessIdToSessionId(GetCurrentProcessId(), &sessionId);

        LogDebug(L"--- RunTouchLogic() Started [Session: %d, Desktop: %s] ---", sessionId, szDesktopName);

        std::wstring instanceMutexName = BuildMutexName(sessionId, szDesktopName);
        HANDLE hInstanceMutex = CreateMutexW(NULL, TRUE, instanceMutexName.c_str());

        if (hInstanceMutex == NULL) {
            LogDebug(L"Error: CreateMutexW failed (Error: %d). Aborting.", GetLastError());
            return;
        }
        if (GetLastError() == ERROR_ALREADY_EXISTS) {
            LogDebug(L"Instance already running. Aborting self.");
            CloseHandle(hInstanceMutex);
            return;
        }

        HANDLE hMasterMutex = OpenMutexW(SYNCHRONIZE, FALSE, MASTER_MUTEX_NAME);
        if (!hMasterMutex) {
            LogDebug(L"Master mutex not found. Service stopped? Aborting worker.");
            ReleaseMutex(hInstanceMutex);
            CloseHandle(hInstanceMutex);
            return;
        }
        LogDebug(L"Master mutex acquired (Handle open).");

        HMODULE hUser32 = LoadLibraryW(L"User32.dll");
        if (hUser32) {
            auto InitializeTouchInjection = (PInitializeTouchInjection)GetProcAddress(hUser32, "InitializeTouchInjection");

            if (InitializeTouchInjection && InitializeTouchInjection(10, 0x1)) {
                LogDebug(L"InitializeTouchInjection SUCCESS. Touch simulation active.");

                bool bRunning = true;
                MSG msg;

                LogDebug(L"Entering message loop...");
                while (bRunning) {
                    DWORD waitResult = MsgWaitForMultipleObjects(1, &hMasterMutex, FALSE, INFINITE, QS_ALLINPUT);

                    switch (waitResult) {
                    case WAIT_OBJECT_0:
                    case WAIT_ABANDONED_0:
                        LogDebug(L"Master service stopped/abandoned. Worker shutting down.");
                        bRunning = false;
                        break;

                    case WAIT_OBJECT_0 + 1:
                        while (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE)) {
                            if (msg.message == WM_QUIT) {
                                LogDebug(L"WM_QUIT received. Shutting down.");
                                bRunning = false;
                                break;
                            }
                            TranslateMessage(&msg);
                            DispatchMessage(&msg);
                        }
                        break;

                    case WAIT_FAILED:
                        LogDebug(L"MsgWaitForMultipleObjects failed (Error: %d).", GetLastError());
                        bRunning = false;
                        break;
                    }
                }
                LogDebug(L"Exiting message loop.");
            }
            else {
                LogDebug(L"Error: InitializeTouchInjection failed or API missing.");
            }
            FreeLibrary(hUser32);
        }
        else {
            LogDebug(L"Error: Failed to load User32.dll.");
        }

        if (hMasterMutex) CloseHandle(hMasterMutex);
        ReleaseMutex(hInstanceMutex);
        CloseHandle(hInstanceMutex);
        LogDebug(L"--- RunTouchLogic() Ended ---");
    }

    bool IsWorkerRunning(DWORD sessionId, LPCWSTR lpDesktop) {
        std::wstring mutexName = BuildMutexName(sessionId, lpDesktop);
        HANDLE hMutex = OpenMutexW(SYNCHRONIZE, FALSE, mutexName.c_str());
        if (hMutex) {
            CloseHandle(hMutex);
            return true;
        }
        return false;
    }

    bool LaunchAsSystemInSession(DWORD targetSessionId, LPCWSTR lpDesktop) {
        HANDLE hCurrentToken = nullptr;
        HANDLE hTokenDup = nullptr;
        LPVOID pEnv = nullptr;
        bool result = false;
        PROCESS_INFORMATION pi = { 0 };
        STARTUPINFOW si = { sizeof(si) };

        LogDebug(L"Launching in Session %d on Desktop %s...", targetSessionId, lpDesktop);

        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ALL_ACCESS, &hCurrentToken)) {
            LogDebug(L"Launch failed: OpenProcessToken error %d", GetLastError());
            return false;
        }

        if (!DuplicateTokenEx(hCurrentToken, MAXIMUM_ALLOWED, NULL, SecurityIdentification, TokenPrimary, &hTokenDup)) {
            LogDebug(L"Launch failed: DuplicateTokenEx error %d", GetLastError());
            CloseHandle(hCurrentToken);
            return false;
        }

        if (!SetTokenInformation(hTokenDup, TokenSessionId, &targetSessionId, sizeof(DWORD))) {
            LogDebug(L"Launch failed: SetTokenInformation error %d (Need SE_TCB_NAME?)", GetLastError());
            CloseHandle(hTokenDup);
            CloseHandle(hCurrentToken);
            return false;
        }

        if (CreateEnvironmentBlock(&pEnv, hTokenDup, FALSE)) {

            si.lpDesktop = (LPWSTR)lpDesktop;

            WCHAR szPath[MAX_PATH];
            GetModuleFileNameW(NULL, szPath, MAX_PATH);

            std::wstring cmdLineStr = std::wstring(L"\"") + szPath + L"\" touchservice";
            std::vector<wchar_t> cmdLineMutable(cmdLineStr.begin(), cmdLineStr.end());
            cmdLineMutable.push_back(0);

            if (CreateProcessAsUserW(
                hTokenDup,
                szPath,
                cmdLineMutable.data(),
                NULL, NULL, FALSE,
                CREATE_UNICODE_ENVIRONMENT,
                pEnv,
                NULL,
                &si, &pi
            )) {
                LogDebug(L"SUCCESS: Launched PID: %d in Session %d on %s", pi.dwProcessId, targetSessionId, lpDesktop);

                CloseHandle(pi.hProcess);
                CloseHandle(pi.hThread);
                result = true;
            }
            else {
                LogDebug(L"Launch failed: CreateProcessAsUserW error %d", GetLastError());
            }

            DestroyEnvironmentBlock(pEnv);
        }
        else {
            LogDebug(L"Launch failed: CreateEnvironmentBlock error %d", GetLastError());
        }

        if (hTokenDup) CloseHandle(hTokenDup);
        if (hCurrentToken) CloseHandle(hCurrentToken);

        return result;
    }

    int RunService() {
        DWORD currentSessionId;
        ProcessIdToSessionId(GetCurrentProcessId(), &currentSessionId);

        if (currentSessionId != 0) {
            RunTouchLogic();
            return 0;
        }

        LogDebug(L"--- RunService() Master Started (Session 0) ---");

        HANDLE hMasterMutex = CreateMutexW(NULL, TRUE, MASTER_MUTEX_NAME);

        if (hMasterMutex == NULL) {
            LogDebug(L"Fatal: Create Master Mutex failed %d", GetLastError());
            return 1;
        }

        if (GetLastError() == ERROR_ALREADY_EXISTS) {
            LogDebug(L"Master service already running. Exiting.");
            CloseHandle(hMasterMutex);
            return 0;
        }

        const std::vector<LPCWSTR> targetDesktops = { L"winsta0\\default", L"winsta0\\winlogon" };
        DWORD eventFlag = 0;

        LogDebug(L"Master Loop Active. Waiting for Session events...");

        while (true) {
            DWORD activeSessionId = WTSGetActiveConsoleSessionId();

            LogDebug(L"Loop check: Active Session is %d", activeSessionId); 

            if (activeSessionId != 0xFFFFFFFF && activeSessionId != 0) {
                for (const auto& desktop : targetDesktops) {
                    if (!IsWorkerRunning(activeSessionId, desktop)) {
                        LogDebug(L"Monitor: Worker missing on Session %d [%s]. Launching...", activeSessionId, desktop);
                        LaunchAsSystemInSession(activeSessionId, desktop);
                    }
                }
            }

            LogDebug(L"Entering WTSWaitSystemEvent (Blocking wait)...");

            if (WTSWaitSystemEvent(WTS_CURRENT_SERVER_HANDLE, WTS_EVENT_ALL, &eventFlag)) {
                LogDebug(L"!!! WTS Event Received !!! Flag: 0x%X", eventFlag);

                eventFlag = 0;
                Sleep(500);
            }
            else {
                LogDebug(L"Error: WTSWaitSystemEvent failed. Code: %d", GetLastError());
                Sleep(2000);
            }
        }

        ReleaseMutex(hMasterMutex);
        CloseHandle(hMasterMutex);
        LogDebug(L"--- RunService() Ended ---");

        return 0;
    }
}