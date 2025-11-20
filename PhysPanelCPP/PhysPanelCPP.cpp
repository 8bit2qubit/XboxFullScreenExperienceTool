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
#include "PanelManager.h"
#include "KeyboardManager.h"
#include "Utils.h"
#include <io.h>
#include <fcntl.h>

void PrintUsage() {
    wchar_t version[32];
    GetAppVersion(version, 32);

    wprintf(L"\n");
    wprintf(L"Xbox Full Screen Experience Tool\n");
    wprintf(L"PhysPanelCPP Utility v%s\n", version);
    wprintf(L"Copyright (C) 2025 8bit2qubit\n");
    wprintf(L"-----------------------------------------------------\n");
    wprintf(L"Usage: PhysPanelCPP <command> [arguments...]\n\n");
    wprintf(L"Commands:\n");
    wprintf(L"  get                  Get the current physical display size (in mm and inches).\n");
    wprintf(L"  set <w> <h> [opt]    Set display size (mm). Use 'reg' as 3rd arg to update OEM registry.\n");
    wprintf(L"                       Requires SYSTEM privileges.\n");
    wprintf(L"  startkeyboard        Launches and prepares the touch keyboard for use.\n\n");
    wprintf(L"Examples:\n");
    wprintf(L"  PhysPanelCPP get\n");
    wprintf(L"  PhysPanelCPP set 155 87\n");
    wprintf(L"  PhysPanelCPP set 155 87 reg\n");
    wprintf(L"  PhysPanelCPP startkeyboard\n\n");
}

int HandleGet() {
    auto dimsOpt = PanelManager::GetDisplaySize();
    if (dimsOpt.has_value()) {
        const auto& dims = dimsOpt.value();
        double diagonalMm = std::sqrt(std::pow(dims.WidthMm, 2) + std::pow(dims.HeightMm, 2));
        double diagonalInches = diagonalMm / 25.4;

        wprintf(L"Current Size: Width = %u mm, Height = %u mm (Diagonal approx. %.2f inches)\n",
            dims.WidthMm, dims.HeightMm, diagonalInches);
        return 0;
    }
    else {
        fwprintf(stderr, L"Error: Failed to get display size. An override may not be set.\n");
        return -1;
    }
}

int HandleSet(int argc, wchar_t* argv[]) {
    if (argc != 4 && argc != 5) {
        fwprintf(stderr, L"Error: The 'set' command requires width and height (optional: reg).\n");
        PrintUsage();
        return 1;
    }

    wchar_t* endPtr = nullptr;

    unsigned long w = wcstoul(argv[2], &endPtr, 10);
    if (argv[2] == endPtr || w == 0) {
        fwprintf(stderr, L"Error: Arguments must be positive integers.\n");
        PrintUsage();
        return 1;
    }

    unsigned long h = wcstoul(argv[3], &endPtr, 10);
    if (argv[3] == endPtr || h == 0) {
        fwprintf(stderr, L"Error: Arguments must be positive integers.\n");
        PrintUsage();
        return 1;
    }

    PanelManager::Dimensions newSize;
    newSize.WidthMm = static_cast<UINT>(w);
    newSize.HeightMm = static_cast<UINT>(h);

    NTSTATUS status = PanelManager::SetDisplaySize(newSize);
    if (status == 0) {
        wprintf(L"Success: Display size has been set.\n");

        if (argc == 5) {
            wchar_t* arg = argv[4];
            if (_wcsicmp(arg, L"reg") == 0) {
                if (PanelManager::SetOEMDeviceForm()) {
                    wprintf(L"Success: OEM DeviceForm registry key set to 0x2e.\n");
                }
                else {
                    fwprintf(stderr, L"Error: Failed to set OEM DeviceForm registry key.\n");
                }
            }
            else {
                wprintf(L"Info: Unknown argument '%s'. Registry update skipped. Use 'reg' to enable it.\n", arg);
            }
        }
        return 0;
    }
    else {
        fwprintf(stderr, L"Error: Failed to set display size. This operation requires SYSTEM privileges.\n");
        fwprintf(stderr, L"  > NTSTATUS Error Code: 0x%X\n", status);
        return -1;
    }
}

int HandleStartKeyboard() {
    try {
        KeyboardManager::StartTouchKeyboard();
        return 0;
    }
    catch (const TabTipNotFoundException&) { return -1; }
    catch (const TabTipActivationException&) { return -1; }
    catch (const std::exception&) { return -1; }
}

bool AttachConsoleAndRedirectIO() {
    bool consoleAttached = false;
    if (AttachConsole(ATTACH_PARENT_PROCESS)) {
        consoleAttached = true;
    }
    else if (AllocConsole()) {
        consoleAttached = true;
    }

    if (!consoleAttached) {
        return false;
    }

    FILE* fp_stdout, * fp_stderr;
    freopen_s(&fp_stdout, "CONOUT$", "w", stdout);
    freopen_s(&fp_stderr, "CONOUT$", "w", stderr);

    (void)_setmode(_fileno(stdout), _O_WTEXT);
    (void)_setmode(_fileno(stderr), _O_WTEXT);

    return true;
}

int wmain(int argc, wchar_t* argv[]) {
    _wsetlocale(LC_ALL, L"");

    bool needsConsole = true;
    wchar_t* action = nullptr;

    if (argc >= 2) {
        action = argv[1];
        if (_wcsicmp(action, L"startkeyboard") == 0) {
#if defined(_DEBUG)
            needsConsole = true;
#else
            needsConsole = false;
#endif
        }
    }

    bool consoleAttached = false;
    if (needsConsole) {
        consoleAttached = AttachConsoleAndRedirectIO();
    }

    if (argc < 2) {
        if (consoleAttached) {
            PrintUsage();
        }
        return 1;
    }

    if (_wcsicmp(action, L"get") == 0) {
        return HandleGet();
    }
    if (_wcsicmp(action, L"set") == 0) {
        return HandleSet(argc, argv);
    }
    if (_wcsicmp(action, L"startkeyboard") == 0) {
        return HandleStartKeyboard();
    }

    if (consoleAttached) {
        fwprintf(stderr, L"Error: Unknown command '%s'.\n", argv[1]);
        PrintUsage();
    }
    return 1;
}