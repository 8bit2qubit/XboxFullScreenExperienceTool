# Xbox Full Screen Experience Tool

> 🌐 **English** | [繁體中文](README.zh-TW.md)

<p align="center">
<img src="app.ico" alt="Xbox Full Screen Experience Tool Icon" style="width: 150px; object-fit: contain; display: block; margin: 0 auto;">
</p>

<p align="center">
<img src="demo.png" alt="Xbox Full Screen Experience Tool Demo" style="width: 521px; object-fit: contain; display: block; margin: 0 auto;">
</p>

<p align="center">
<a href="https://github.com/8bit2qubit/XboxFullScreenExperienceTool/releases/latest"><img src="https://img.shields.io/github/v/release/8bit2qubit/XboxFullScreenExperienceTool?style=flat-square&color=blue" alt="Latest Release"></a>
<a href="https://github.com/8bit2qubit/XboxFullScreenExperienceTool/releases"><img src="https://img.shields.io/github/downloads/8bit2qubit/XboxFullScreenExperienceTool/total" alt="Total Downloads"></a>
<a href="#"><img src="https://img.shields.io/badge/tech-C%23%20%26%20.NET%208-blueviolet.svg?style=flat-square" alt="Tech"></a>
<a href="https://github.com/8bit2qubit/XboxFullScreenExperienceTool/blob/main/LICENSE"><img src="https://img.shields.io/github/license/8bit2qubit/XboxFullScreenExperienceTool" alt="License"></a>
</p>

A lightweight and secure one-click utility designed to enable **Windows 11’s hidden Xbox full screen gaming experience**.
This tool automates all underlying configurations, providing a seamless, console-like interface optimized for gamepads.

## ⚠️ **Warning: Please Read Before Proceeding**

By using this tool, you acknowledge and agree to the following:

* **System Modification** – This tool performs deep modifications to Windows and may cause instability, crashes, data loss, or require OS reinstallation.
* **Use at Your Own Risk** – You are fully responsible for any consequences. The developer provides no warranty, support, or liability for any damages.
* **No Guarantees** – The tool is provided *as is* with no guarantee of stability, compatibility, or functionality. It may not work correctly on your specific configuration.
* **Backup Required** – Always back up your important data and create a system restore point before use.
* **Unofficial Tool** – This project is not affiliated with, endorsed by, or supported by Microsoft or Xbox.

-----

## 💡 Screen Override for PCs & Laptops: Choosing Your Method

The Xbox Full Screen Experience is designed for handheld-sized screens. If your device is not a handheld, a screen dimension override is required. This tool offers two distinct methods, and now automatically guides you to the appropriate choice based on your device type.

### Task Scheduler Mode: `PhysPanelCS`
This is the default method. It is easy to use and requires no additional manual setup. It schedules a task to apply the override at boot. However, this can create a race condition on fast systems. If you log in too quickly, the Windows Shell may initialize before the override is applied, causing it to fall back to the standard desktop for that session.

### Driver Mode: `PhysPanelDrv`
This advanced mode uses a custom kernel driver to apply the override at the earliest stage of system boot, which completely eliminates the race condition. This is the most robust and reliable solution for desktops.

### Which Mode Should You Use?

*   **For Desktop PCs**: You can choose between **`PhysPanelCS`** and `PhysPanelDrv`. For the most reliable experience, `PhysPanelDrv` is the recommended choice.
*   **For Laptops**: The tool will automatically restrict you to **`PhysPanelCS`**. This is a safety measure to ensure maximum compatibility. The driver mode option will be disabled.
*   **For Handheld Devices**: No override is needed! The mode selection UI will be disabled entirely.

> #### **Prerequisites for `PhysPanelDrv` Mode**
>
> ⚠️ **Important:** These steps are only necessary for **desktop PC users** who wish to use the `PhysPanelDrv` mode.
>
> Installing this **test-signed driver** requires you to manually disable Secure Boot and enable Windows Test Signing Mode.
>
> **Step 1: Enter BIOS/UEFI Settings**
> 1.  Restart your computer and press the designated key during boot (usually `Del`, `F2`, `F10`, or `Esc`) to enter the BIOS/UEFI setup.
> 2.  Find and **disable** the **Secure Boot** option.
> 3.  Save your changes and exit.
>
> **Step 2: Enable Test Signing in Windows**
> 1.  Once your computer has restarted into Windows, open Terminal (PowerShell or Command Prompt) **as an administrator**.
> 2.  Enter the following command and press Enter:
>     ```
>     bcdedit /set testsigning on
>     ```
> 3.  Restart your computer one more time to apply the change.
>
> After completing these steps, you can select **`PhysPanelDrv`** in the tool.

-----

## ⚙️ System Requirements

This tool is compatible with **Windows 11 25H2 builds `26200.7015` or later**. If your system does not meet this requirement, the tool will display an error and exit.

> ### **How to Read Build Numbers (Important!)**
>
> When checking the version, **please look at the main build number (before the dot)**. The number *after* the dot is just a minor update revision.
>
> *   **INCOMPATIBLE:** `26100.xxxx` (Release Build 24H2)
> *   **COMPATIBLE:** `26200.7019` or later (Release Build 25H2)
> *   **COMPATIBLE:** `26200.7015` or later (Release Preview Build 25H2)
> *   **COMPATIBLE:** `26220.6972` or later (Dev Build 25H2)
>
> **Example:** A build like `26200.6899` is **NOT** compatible because its revision `.6899` is lower than the required `.7015`.

For a detailed walkthrough on upgrading to the correct build, refer to the following guide:
* **[English Guide](https://github.com/8bit2qubit/xbox-fullscreen-experience-guide/blob/main/README.md)**
* **[Traditional Chinese Guide (繁體中文指南)](https://github.com/8bit2qubit/xbox-fullscreen-experience-guide/blob/main/README.zh-TW.md)**
* **[Simplified Chinese Guide (简体中文指南)](https://github.com/8bit2qubit/xbox-fullscreen-experience-guide/blob/main/README.zh-CN.md)**

Please verify your Windows build version before downloading.

**[➡️ Download the Latest Release](https://github.com/8bit2qubit/XboxFullScreenExperienceTool/releases/latest)**

-----

## ❤️ Support This Project

If you find this tool helpful, your support would be a great motivation for me to continue maintaining and developing more open-source projects!

<a href="https://www.patreon.com/cw/u57025610/membership"><img src="https://img.shields.io/badge/Patreon-F96854?style=for-the-badge&logo=patreon&logoColor=white" alt="Support me on Patreon"></a>

-----

## ✨ Features

* **One-Click Toggle** – Simple interface to enable or disable the Xbox full screen experience.
* **Automatic System Check** – Verifies your Windows build for compatibility at startup.
* **Automatic Keyboard Fix** – Fixes the missing on-screen keyboard on non-touch PCs by automatically preparing it at logon, ensuring seamless text input with a controller.
* **Device Type Emulation** – Automatically simulates a handheld device type for activation on desktop or laptop systems.
* **Automatic Mode Selection** – Detects your device type (Desktop, Laptop, Handheld) and provides the appropriate override options.
* **Safe and Reversible** – All changes are fully reversible. Backups of original settings are created to ensure safe restoration.
* **Standard Installation** – Distributed as a `.msi` installer for clean installation, management, and removal.

-----

## 🚀 Quick Start

This tool prepares your system for the new mode. Final activation is done in Windows Settings after following these steps.

### 1. Prepare Your System
1.  Download the latest `.msi` package from the [**Releases Page**](https://github.com/8bit2qubit/XboxFullScreenExperienceTool/releases/latest).
2.  Run the installer (administrator privileges required).
3.  Launch the tool from the desktop shortcut. If using a PC or laptop, select your preferred override mode.
    > **Note:** If you choose **`PhysPanelDrv`**, ensure you have completed the prerequisites listed above first.
4.  Click **“Enable Xbox Full Screen Experience”**.
5.  **Restart your PC** for the changes to take effect.

### 2. Update Core Apps
1.  After restarting, open the **Microsoft Store**.
2.  Go to the **Downloads** section (or **Library** in older versions of the Store).
3.  Click **"Check for updates"** to refresh all apps. Make sure **Xbox** and **Xbox Game Bar** are fully updated.
    > 🔄 **Tip:** You may need to run "Check for updates" **twice** to ensure everything is fully installed.

### 3. Activate Full Screen Experience
1.  Navigate to **Start → Settings → Gaming → Full screen experience**.
2.  Set "Choose Home app" to **Xbox**.
    - If this option is missing, return to the previous step and ensure the apps are fully updated.
3.  Enable **"Enter full screen experience on startup"**.

### **How to Revert**
1.  Run the tool again and click **“Disable & Restore”**.
2.  **Restart your PC** to complete the process.

-----

## 💻 Tech Stack

* **Runtime**: .NET 8
* **Language**: C#
* **UI Framework**: Windows Forms (WinForms)
* **Dependencies**:
  * **ViVeLib (ViVeTool)** – A native API wrapper for managing Windows Feature Flags. Integrated as a Git submodule from [thebookisclosed/ViVe](https://github.com/thebookisclosed/ViVe).
  * **PhysPanelLib** – A custom library for reading and writing physical panel size information via undocumented `ntdll.dll` APIs. Concept adapted from [riverar/physpanel](https://github.com/riverar/physpanel).
  * **PhysPanelDrv** – A lightweight kernel driver for `PhysPanelDrv` (Driver Mode) that reliably overrides physical display dimensions, ensuring the system consistently simulates a handheld screen size. Integrated as a Git submodule from [8bit2qubit/PhysPanelDrv](https://github.com/8bit2qubit/PhysPanelDrv).
* **Installer**: Visual Studio Installer Projects (MSI)

-----

## 🙏 Acknowledgements

This project was made possible by these incredible open-source tools:

* **[ViVeTool](https://github.com/thebookisclosed/ViVe)** by **@thebookisclosed**
* **[physpanel](https://github.com/riverar/physpanel)** by **@riverar**

A huge thank you for their contributions to the community.

-----

## 🛠️ Local Development

Follow these steps to run this project on your own machine.

1.  **Clone the Repository**

    ```bash
    git clone https://github.com/8bit2qubit/XboxFullScreenExperienceTool.git
    cd XboxFullScreenExperienceTool
    ```

2.  **Initialize Submodules**
    This project uses Git Submodules to manage dependencies.

    ```bash
    git submodule update --init --recursive
    ```

3.  **Open in Visual Studio**
    Open the `XboxFullScreenExperienceTool.sln` solution file with Visual Studio.

4.  **Run for Development**
    In Visual Studio, set the build configuration to `Debug` and press `F5` to build and run the application.

5.  **Build for Production**
    When you are ready to deploy, switch the build configuration to `Release` and build the solution. The output will be generated in the `XboxFullScreenExperienceTool/bin/Release` folder.

-----

## 📄 License

This project is licensed under the [GNU General Public License v3.0 (GPL-3.0)](https://github.com/8bit2qubit/XboxFullScreenExperienceTool/blob/main/LICENSE).

This means you are free to use, modify, and distribute this software, but any derivative works based on this project must also be distributed under the **same GPL-3.0 license and provide the complete source code**. For more details, please see the [official GPL-3.0 terms](https://www.gnu.org/licenses/gpl-3.0.html).