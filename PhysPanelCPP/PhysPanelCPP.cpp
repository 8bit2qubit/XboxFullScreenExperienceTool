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
    std::wstring version = GetAppVersion();
    std::wcout << std::endl;
    std::wcout << L"Xbox Full Screen Experience Tool" << std::endl;
    std::wcout << L"PhysPanelCPP Utility v" << version << std::endl;
    std::wcout << L"Copyright (C) 2025 8bit2qubit" << std::endl;
    std::wcout << L"-----------------------------------------------------" << std::endl;
    std::wcout << L"Usage: PhysPanelCPP <command> [arguments...]" << std::endl;
    std::wcout << std::endl;
    std::wcout << L"Commands:" << std::endl;
    std::wcout << L"  get                  Get the current physical display size (in mm and inches)." << std::endl;
    std::wcout << L"  set <width> <height> Set a new physical display size (in mm). Requires SYSTEM privileges." << std::endl;
    std::wcout << L"  startkeyboard        Launches and prepares the touch keyboard for use." << std::endl;
    std::wcout << std::endl;
    std::wcout << L"Examples:" << std::endl;
    std::wcout << L"  PhysPanelCPP get" << std::endl;
    std::wcout << L"  PhysPanelCPP set 155 87" << std::endl;
    std::wcout << L"  PhysPanelCPP startkeyboard" << std::endl;
    std::wcout << std::endl;
}

int HandleGet() {
    auto dimsOpt = PanelManager::GetDisplaySize();
    if (dimsOpt.has_value()) {
        const auto& dims = dimsOpt.value();
        double diagonalMm = std::sqrt(std::pow(dims.WidthMm, 2) + std::pow(dims.HeightMm, 2));
        double diagonalInches = diagonalMm / 25.4;

        std::wcout << std::fixed << std::setprecision(2);
        std::wcout << L"Current Size: Width = " << dims.WidthMm << L" mm, Height = " << dims.HeightMm
            << L" mm (Diagonal approx. " << diagonalInches << L" inches)" << std::endl;

        std::wcout.flush();
        return 0;
    }
    else {
        std::wcerr << L"Error: Failed to get display size. An override may not be set." << std::endl;
        std::wcerr.flush();
        return -1;
    }
}

int HandleSet(int argc, wchar_t* argv[]) {
    if (argc != 4) {
        std::wcerr << L"Error: The 'set' command requires two positive integer arguments (width and height)." << std::endl;
        PrintUsage();
        std::wcerr.flush();
        return 1;
    }

    try {
        PanelManager::Dimensions newSize;
        newSize.WidthMm = std::stoul(argv[2]);
        newSize.HeightMm = std::stoul(argv[3]);

        NTSTATUS status = PanelManager::SetDisplaySize(newSize);
        if (status == 0) {
            std::wcout << L"Success: Display size has been set." << std::endl;
            std::wcout.flush();
            return 0;
        }
        else {
            std::wcerr << L"Error: Failed to set display size. This operation requires SYSTEM privileges." << std::endl;
            std::wcerr << L"  > NTSTATUS Error Code: 0x" << std::hex << std::uppercase << status << std::endl;
            std::wcerr.flush();
            return -1;
        }
    }
    catch (const std::invalid_argument&) {
        std::wcerr << L"Error: The 'set' command requires two positive integer arguments (width and height)." << std::endl;
        PrintUsage();
        std::wcerr.flush();
        return 1;
    }
    catch (const std::out_of_range&) {
        std::wcerr << L"Error: The 'set' command requires two positive integer arguments (width and height)." << std::endl;
        PrintUsage();
        std::wcerr.flush();
        return 1;
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

    std::wcout.clear();
    std::wcerr.clear();
    std::wcout.sync_with_stdio(true);
    std::wcerr.sync_with_stdio(true);

    (void)_setmode(_fileno(stdout), _O_WTEXT);
    (void)_setmode(_fileno(stderr), _O_WTEXT);

    return true;
}

int wmain(int argc, wchar_t* argv[]) {
    _wsetlocale(LC_ALL, L"");

    bool needsConsole = true;
    std::wstring action = L"";

    if (argc >= 2) {
        action = argv[1];
        for (auto& c : action) c = towlower(c);

        if (action == L"startkeyboard") {
            needsConsole = false;
        }
    }

    bool consoleAttached = false;
    if (needsConsole) {
        consoleAttached = AttachConsoleAndRedirectIO();
    }

    if (argc < 2) {
        if (consoleAttached) {
            PrintUsage();
            std::wcout.flush();
        }
        return 1;
    }

    if (action == L"get") {
        return HandleGet();
    }
    if (action == L"set") {
        return HandleSet(argc, argv);
    }
    if (action == L"startkeyboard") {
        return HandleStartKeyboard();
    }

    if (consoleAttached) {
        std::wcerr << L"Error: Unknown command '" << argv[1] << L"'." << std::endl;
        PrintUsage();
        std::wcerr.flush();
    }
    return 1;
}