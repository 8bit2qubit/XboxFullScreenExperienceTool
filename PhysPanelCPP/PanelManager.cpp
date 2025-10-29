#include "pch.h"
#include "PanelManager.h"

typedef const struct _WNF_STATE_NAME* PCWNF_STATE_NAME;

typedef const GUID* PCWNF_TYPE_ID;

typedef struct _WNF_STATE_NAME {
    ULONG Data1;
    ULONG Data2;
} WNF_STATE_NAME, * PWNF_STATE_NAME;

extern "C" {
    NTSTATUS NTAPI NtQueryWnfStateData(
        _In_ PCWNF_STATE_NAME StateName,
        _In_opt_ PCWNF_TYPE_ID TypeId,
        _In_opt_ const VOID* ExplicitScope,
        _Out_ PULONG ChangeStamp,
        _Out_writes_bytes_to_opt_(*BufferSize, *BufferSize) PVOID Buffer,
        _Inout_ PULONG BufferSize);

    NTSTATUS NTAPI NtUpdateWnfStateData(
        _In_ PCWNF_STATE_NAME StateName,
        _In_reads_bytes_(Length) const VOID* Buffer,
        _In_ ULONG Length,
        _In_opt_ PCWNF_TYPE_ID TypeId,
        _In_opt_ const VOID* ExplicitScope,
        _In_ ULONG MatchingChangeStamp,
        _In_ ULONG CheckStamp);
}

namespace PanelManager {

    const WNF_STATE_NAME WNF_DX_INTERNAL_PANEL_DIMENSIONS = { 0xA3BC4875, 0x41C61629 };

    std::optional<Dimensions> GetDisplaySize() {
        ULONG bufferSize = sizeof(ULONGLONG);
        ULONGLONG rawDimensions = 0;
        ULONG changeStamp;

        NTSTATUS status = NtQueryWnfStateData(
            &WNF_DX_INTERNAL_PANEL_DIMENSIONS,
            nullptr,
            nullptr,
            &changeStamp,
            &rawDimensions,
            &bufferSize
        );

        if (status == 0 && bufferSize == sizeof(ULONGLONG)) {
            Dimensions dims;
            dims.WidthMm = (UINT)(rawDimensions & 0xFFFFFFFF);
            dims.HeightMm = (UINT)((rawDimensions >> 32) & 0xFFFFFFFF);
            return dims;
        }

        return std::nullopt;
    }

    NTSTATUS SetDisplaySize(const Dimensions& dims) {
        ULONGLONG dimensions = ((ULONGLONG)dims.HeightMm << 32) | dims.WidthMm;

        return NtUpdateWnfStateData(
            &WNF_DX_INTERNAL_PANEL_DIMENSIONS,
            &dimensions,
            sizeof(dimensions),
            nullptr,
            nullptr,
            0,
            FALSE
        );
    }
}