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