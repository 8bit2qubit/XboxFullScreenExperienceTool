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
#include "Utils.h"

bool GetAppVersion(wchar_t* buffer, size_t size) {
    if (!buffer || size == 0) return false;

    swprintf_s(buffer, size, L"Unknown");

    wchar_t filename[MAX_PATH];
    if (GetModuleFileNameW(NULL, filename, MAX_PATH) == 0) {
        return false;
    }

    DWORD handle = 0;
    DWORD verSize = GetFileVersionInfoSizeW(filename, &handle);
    if (verSize == 0) {
        return false;
    }

    BYTE* verData = new (std::nothrow) BYTE[verSize];
    if (!verData) return false;

    if (!GetFileVersionInfoW(filename, 0, verSize, verData)) {
        delete[] verData;
        return false;
    }

    VS_FIXEDFILEINFO* fileInfo = nullptr;
    UINT len = 0;
    if (VerQueryValueW(verData, L"\\", (LPVOID*)&fileInfo, &len)) {
        if (len && fileInfo->dwSignature == 0xFEEF04BD) {
            swprintf_s(buffer, size, L"%hu.%hu.%hu",
                HIWORD(fileInfo->dwProductVersionMS),
                LOWORD(fileInfo->dwProductVersionMS),
                HIWORD(fileInfo->dwProductVersionLS));

            delete[] verData;
            return true;
        }
    }

    delete[] verData;
    return false;
}