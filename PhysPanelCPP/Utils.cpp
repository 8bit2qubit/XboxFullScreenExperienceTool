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
#include "Utils.h"

std::wstring GetAppVersion() {
    wchar_t filename[MAX_PATH];
    if (GetModuleFileNameW(NULL, filename, MAX_PATH) == 0) {
        return L"Unknown";
    }

    DWORD handle = 0;
    DWORD size = GetFileVersionInfoSizeW(filename, &handle);
    if (size == 0) {
        return L"Unknown";
    }

    std::vector<BYTE> buffer(size);
    if (!GetFileVersionInfoW(filename, 0, size, buffer.data())) {
        return L"Unknown";
    }

    VS_FIXEDFILEINFO* fileInfo = nullptr;
    UINT len = 0;
    if (VerQueryValueW(buffer.data(), L"\\", (LPVOID*)&fileInfo, &len)) {
        if (len && fileInfo->dwSignature == 0xFEEF04BD) {
            return std::to_wstring(HIWORD(fileInfo->dwProductVersionMS)) + L"." +
                std::to_wstring(LOWORD(fileInfo->dwProductVersionMS)) + L"." +
                std::to_wstring(HIWORD(fileInfo->dwProductVersionLS));
        }
    }

    return L"Unknown";
}