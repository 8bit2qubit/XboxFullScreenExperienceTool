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

#pragma once

// Windows and COM
#include <Windows.h>
#include <comdef.h>
#include <ShlObj.h>
#include <TlHelp32.h>

// C++ Standard Library
#include <iostream>
#include <string>
#include <vector>
#include <chrono>
#include <thread>
#include <optional>
#include <iomanip>

// Link necessary libraries
#pragma comment(lib, "ntdll.lib")
#pragma comment(lib, "version.lib")