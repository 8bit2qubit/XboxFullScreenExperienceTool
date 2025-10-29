#pragma once
#include "pch.h"

class TabTipNotFoundException : public std::runtime_error {
public:
    explicit TabTipNotFoundException(const std::string& message) : std::runtime_error(message) {}
};

class TabTipActivationException : public std::runtime_error {
public:
    explicit TabTipActivationException(const std::string& message) : std::runtime_error(message) {}
};

namespace KeyboardManager {
    void StartTouchKeyboard();
}