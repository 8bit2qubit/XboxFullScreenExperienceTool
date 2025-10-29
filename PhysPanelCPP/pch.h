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