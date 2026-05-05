# 💡 Screen Dimensions Override for Desktop PCs & Laptops

> 🌐 **English** | [繁體中文](screen-dimensions-override.zh-TW.md)

> ℹ️ **This document only applies to Legacy builds.** Native builds (`26100.8328+` / `26200.8328+` / `26220.7271+` / `26300.7674+` / `28020.1362+`) no longer require any screen dimensions override on Desktop PCs and Laptops.

The Xbox Full Screen Experience (Xbox mode) is designed for handheld-sized screens. On Legacy builds, if your device is not a handheld, a screen dimensions override is required to satisfy the activation conditions. This tool offers two distinct methods, and automatically guides you to the appropriate choice based on your device type.

## Task Scheduler Mode: `PhysPanelCS` (Recommended)

This is the **default and recommended** method. It is easy to use, requires no additional manual setup, and provides high reliability for all devices, including desktops and laptops.

## Driver Mode: `PhysPanelDrv` (Alternative)

This is an **alternative advanced mode** that uses a custom kernel driver to apply the override at the earliest stage of system boot. This method is an option for users who may still encounter issues with the default `PhysPanelCS` mode, but it **requires disabling Secure Boot** and **enabling Test Signing** (see prerequisites below).

## Which Mode Should You Use?

- **For Desktops & Laptops**: Start with **`PhysPanelCS`**. This is the recommended, safest, and most reliable method for most users. If you experience any issues with this default mode, **`PhysPanelDrv`** is available as a fallback alternative (requires the prerequisites listed below).
- **For Handheld Devices**: Your device does not require `PhysPanelCS` or `PhysPanelDrv`. The mode selection UI will be automatically disabled.

---

## Prerequisites for `PhysPanelDrv` Mode (Alternative)

> ⚠️ **Important:** These steps are only necessary for **desktop and laptop users** who choose to use the alternative `PhysPanelDrv` mode.

Installing this **test-signed driver** requires you to manually disable Secure Boot and enable Windows Test Signing Mode.

### Step 1: Enter BIOS/UEFI Settings

1.  Restart your computer and press the designated key during boot (usually `Del`, `F2`, `F10`, or `Esc`) to enter the BIOS/UEFI setup.
2.  Find and **disable** the **Secure Boot** option.
3.  Save your changes and exit.

### Step 2: Enable Test Signing in Windows

1.  Once your computer has restarted into Windows, open Terminal (PowerShell or Command Prompt) **as an administrator**.
2.  Enter the following command and press Enter:
    ```
    bcdedit /set testsigning on
    ```
3.  Restart your computer one more time to apply the change.

After completing these steps, you can select **`PhysPanelDrv`** in the tool.
