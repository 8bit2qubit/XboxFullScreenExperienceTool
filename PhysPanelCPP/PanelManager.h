#pragma once
#include "pch.h"

namespace PanelManager {

    struct Dimensions {
        UINT WidthMm;
        UINT HeightMm;
    };

    std::optional<Dimensions> GetDisplaySize();

    NTSTATUS SetDisplaySize(const Dimensions& dims);
}