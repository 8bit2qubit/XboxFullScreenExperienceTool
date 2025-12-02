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

using Albacore.ViVe;
using Albacore.ViVe.NativeEnums;
using Albacore.ViVe.NativeStructs;
using Microsoft.Win32;
using PhysPanelLib;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using XboxFullScreenExperienceTool.Helpers;

namespace XboxFullScreenExperienceTool
{
    /// <summary>
    /// 代表 Xbox 全螢幕體驗工具的主視窗。
    /// 這個類別包含了所有用於檢查系統狀態、套用變更以及處理使用者互動的核心邏輯。
    /// </summary>
    public partial class MainForm : Form
    {
        //======================================================================
        // 共用常數與設定 (Shared Constants & Configuration)
        //======================================================================

        #region Shared Configuration
        // 需要透過 ViVe 啟用的功能 ID 陣列。
        // 定義所有功能 ID 的集合。
        private static readonly uint[] ALL_FEATURE_IDS = { 52580392, 50902630, 59765208 };
        // 定義基礎 (BASIC) 功能 ID 的集合 (適用於 Legacy Build 系統與掌機)。
        private static readonly uint[] BASIC_FEATURE_IDS = { 52580392, 50902630 };

        // 登錄檔相關常數。
        private const string REG_PATH_PARENT = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        private const string REG_PATH = REG_PATH_PARENT + @"\OEM";
        private const string REG_VALUE = "DeviceForm";

        /// <summary>
        /// 用於備份原始登錄檔值的檔案路徑。
        /// </summary>
        private static string BackupFilePath => Path.Combine(AppPathManager.InstallPath, "DeviceForm.bak");
        #endregion

        //======================================================================
        // 實例常數 (Instance Constants)
        //======================================================================

        #region Instance Constants
        /// <summary>
        /// 功能旗標：是否要在螢幕尺寸大於 9.5" 的裝置上強制停用驅動程式 (Drv) 模式。
        /// </summary>
        private const bool RESTRICT_DRV_MODE_ON_LARGE_SCREEN = false;

        // 螢幕尺寸限制。
        private const double MAX_DIAGONAL_INCHES = 9.5;
        private const double INCHES_TO_MM = 25.4;

        /// <summary>
        /// 日誌檔案路徑 (僅 UI 模式使用，靜默模式有自己的 Logger)。
        /// </summary>
        private readonly string _logFilePath = Path.Combine(Application.StartupPath, "XboxFullScreenExperienceTool.log");
        #endregion

        //======================================================================
        // 靜默動作處理 (Silent Action Handler)
        //======================================================================

        #region Silent Action Handler
        /// <summary>
        /// 提供一組完全靜態的方法，用於在非互動式環境 (如靜默解除安裝) 中執行核心操作。
        /// 這個類別絕對不能參考 MainForm 的任何實例成員或 UI 控制項，但可以參考外部的靜態成員。
        /// </summary>
        public static class SilentActionHandler
        {
            /// <summary>
            /// 執行完整的靜默停用和清理流程。
            /// </summary>
            /// <param name="logger">一個用於記錄進度的委派。</param>
            /// <returns>如果所有操作都成功，則為 true；否則為 false。</returns>
            public static async Task<bool> PerformUninstallCleanup(Action<string> logger)
            {
                logger("Starting silent cleanup process.");
                bool allSucceeded = true;

                try
                {
                    allSucceeded &= DisableViveFeatures(logger);
                    allSucceeded &= RestoreRegistry(logger);

                    if (TaskSchedulerManager.SetPanelDimensionsTaskExists())
                    {
                        logger("Deleting SetPanelDimensions task...");
                        TaskSchedulerManager.DeleteSetPanelDimensionsTask();
                    }
                    if (TaskSchedulerManager.StartGamepadKeyboardOnLogonTaskExists())
                    {
                        logger("Deleting StartGamepadKeyboardOnLogon task...");
                        TaskSchedulerManager.DeleteStartGamepadKeyboardOnLogonTask();
                    }

                    if (DriverManager.IsDriverServiceInstalled())
                    {
                        logger("Uninstalling PhysPanelDrv driver...");
                        bool driverSuccess = await Task.Run(() => DriverManager.UninstallDriver(logger, isSilent: true));
                        if (!driverSuccess) { allSucceeded = false; }
                        logger(driverSuccess ? "Driver uninstall command executed." : "Driver uninstall command FAILED.");
                    }
                }
                catch (Exception ex)
                {
                    logger($"FATAL EXCEPTION during cleanup: {ex.Message}");
                    allSucceeded = false;
                }

                logger($"Silent cleanup process finished. Overall success: {allSucceeded}");
                return allSucceeded;
            }

            /// <summary>
            /// 停用功能。必須清除所有已知的 ID。
            /// 確保無論使用者之前處於何種狀態 (Legacy Build、Native Build、掌機、非掌機)，都能徹底還原。
            /// </summary>
            private static bool DisableViveFeatures(Action<string> logger)
            {
                try
                {
                    // 取得應停用的 ID 清單
                    uint[] idsToDisable = GetIdsToDisable();
                    logger($"Disabling features based on build ver (Native={IsNativeSupportBuild()}): {string.Join(", ", idsToDisable)}");
                    var updates = Array.ConvertAll(idsToDisable, id => new RTL_FEATURE_CONFIGURATION_UPDATE
                    {
                        FeatureId = id,
                        EnabledState = RTL_FEATURE_ENABLED_STATE.Disabled,
                        Operation = RTL_FEATURE_CONFIGURATION_OPERATION.FeatureState | RTL_FEATURE_CONFIGURATION_OPERATION.VariantState,
                        Priority = RTL_FEATURE_CONFIGURATION_PRIORITY.User
                    });
                    FeatureManager.SetFeatureConfigurations(updates, RTL_FEATURE_CONFIGURATION_TYPE.Runtime);
                    FeatureManager.SetFeatureConfigurations(updates, RTL_FEATURE_CONFIGURATION_TYPE.Boot);
                    logger("Features disabled successfully.");
                    return true;
                }
                catch (Exception ex)
                {
                    logger($"ERROR disabling ViVe features: {ex.Message}");
                    return false;
                }
            }

            private static bool RestoreRegistry(Action<string> logger)
            {
                try
                {
                    logger("Attempting to restore registry...");
                    if (File.Exists(BackupFilePath))
                    {
                        logger($"Backup file found at '{BackupFilePath}'.");
                        string backupContent = File.ReadAllText(BackupFilePath);
                        using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(REG_PATH, true))
                        {
                            if (key != null)
                            {
                                if (backupContent == "DELETE_ON_RESTORE")
                                {
                                    key.DeleteValue(REG_VALUE, false);
                                    logger($"Registry value '{REG_VALUE}' removed.");
                                }
                                else if (int.TryParse(backupContent, out int val))
                                {
                                    key.SetValue(REG_VALUE, val, RegistryValueKind.DWord);
                                    logger($"Registry value restored to '{val}'.");
                                }
                            }
                        }
                        File.Delete(BackupFilePath);
                        logger("Backup file deleted.");
                    }
                    else
                    {
                        logger("No backup file found. No registry action taken.");
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    logger($"ERROR restoring registry: {ex.Message}");
                    return false;
                }
            }

            private static void EnableAllFeatures(Action<string> logger)
            {
                try
                {
                    logger($"Enabling features: {string.Join(", ", ALL_FEATURE_IDS)}");
                    var updates = Array.ConvertAll(ALL_FEATURE_IDS, id => new RTL_FEATURE_CONFIGURATION_UPDATE
                    {
                        FeatureId = id,
                        EnabledState = RTL_FEATURE_ENABLED_STATE.Enabled,
                        Operation = RTL_FEATURE_CONFIGURATION_OPERATION.FeatureState | RTL_FEATURE_CONFIGURATION_OPERATION.VariantState,
                        Priority = RTL_FEATURE_CONFIGURATION_PRIORITY.User
                    });
                    FeatureManager.SetFeatureConfigurations(updates, RTL_FEATURE_CONFIGURATION_TYPE.Runtime);
                    FeatureManager.SetFeatureConfigurations(updates, RTL_FEATURE_CONFIGURATION_TYPE.Boot);
                    logger("All features enabled successfully.");
                }
                catch (Exception ex)
                {
                    logger($"ERROR enabling features: {ex.Message}");
                }
            }

            /// <summary>
            /// 智慧啟用功能，取代舊的 EnableAllFeatures。
            /// 考慮 Override 狀態，以正確區分「被覆寫的主機」與「真掌機」。
            /// </summary>
            private static void EnableSmartFeatures(Action<string> logger)
            {
                try
                {
                    // 1. 取得環境資訊
                    bool isNative = IsNativeSupportBuild();

                    // 2. 檢查覆寫狀態 (PhysPanelCS 或 PhysPanelDrv 是否存在)
                    bool isOverridePresent = TaskSchedulerManager.SetPanelDimensionsTaskExists() ||
                                             DriverManager.IsDriverServiceInstalled();

                    // 3. 取得螢幕尺寸
                    var (success, size) = PanelManager.GetDisplaySize();
                    double diagonalInches = (success && (size.WidthMm > 0 || size.HeightMm > 0))
                        ? Math.Sqrt((size.WidthMm * size.WidthMm) + (size.HeightMm * size.HeightMm)) / INCHES_TO_MM
                        : 0;

                    // 4. 決定 ID 清單 (傳入 isOverridePresent)
                    uint[] idsToEnable = GetRequiredFeatureIds(isNative, diagonalInches, isOverridePresent);
                    logger($"Enabling smart features (Native={isNative}, Override={isOverridePresent}, Size={diagonalInches:F2}\"): {string.Join(", ", idsToEnable)}");
                    var updates = Array.ConvertAll(idsToEnable, id => new RTL_FEATURE_CONFIGURATION_UPDATE
                    {
                        FeatureId = id,
                        EnabledState = RTL_FEATURE_ENABLED_STATE.Enabled,
                        Operation = RTL_FEATURE_CONFIGURATION_OPERATION.FeatureState | RTL_FEATURE_CONFIGURATION_OPERATION.VariantState,
                        Priority = RTL_FEATURE_CONFIGURATION_PRIORITY.User
                    });
                    FeatureManager.SetFeatureConfigurations(updates, RTL_FEATURE_CONFIGURATION_TYPE.Runtime);
                    FeatureManager.SetFeatureConfigurations(updates, RTL_FEATURE_CONFIGURATION_TYPE.Boot);
                    logger("Features enabled successfully.");
                }
                catch (Exception ex)
                {
                    logger($"ERROR enabling features: {ex.Message}");
                }
            }

            /// <summary>
            /// 供 /migrate 使用。
            /// 透過檢查備份檔案 (DeviceForm.bak) 是否存在來判斷功能是否啟用。
            /// 避免誤將全新安裝或原本停用的使用者強制啟用。
            /// </summary>
            public static void MigrateFeaturesIfEnabled(Action<string> logger)
            {
                try
                {
                    // 當備份檔案存在時，代表本工具介入過 (作為是否已啟用的標記)
                    if (File.Exists(BackupFilePath))
                    {
                        logger($"Detected active state (Backup file found). Applying SMART Feature ID updates...");
                        // 呼叫智慧啟用，而非 EnableAllFeatures，確保從 Legacy Build 升級時，在掌機上不會誤開 59765208
                        EnableSmartFeatures(logger);
                    }
                    else
                    {
                        logger("Backup file not found. Assuming features are disabled or clean install. Skipping Feature ID updates.");
                    }
                }
                catch (Exception ex)
                {
                    logger($"ERROR during feature migration check: {ex.Message}");
                }
            }
        }
        #endregion

        //======================================================================
        // 狀態旗標 (State Flags)
        //======================================================================

        #region State Flags
        /// <summary>
        /// 追蹤是否已套用變更但使用者尚未重新啟動電腦。
        /// 在此狀態下，UI 應被鎖定以防止進一步的操作。
        /// </summary>
        private bool _restartPending = false;

        /// <summary>
        /// 指示表單是否正在進行初始化或重大狀態重置（例如切換語言）。
        /// </summary>
        private bool _isInitializing = true;

        /// <summary>
        /// 指示 UI 控制項的狀態是否正在由程式碼自動更新（例如 CheckCurrentStatus 正在將系統狀態同步到 UI）。
        /// </summary>
        private bool _isUpdatingStatus = false;
        #endregion

        //======================================================================
        // 表單事件 (Form Events)
        //======================================================================

        public MainForm()
        {
            InitializeComponent();
            InitializeLanguage(); // 初始化語言下拉選單
        }

        /// <summary>
        /// 表單載入時的初始化邏輯。
        /// </summary>
        private void MainForm_Load(object sender, EventArgs e)
        {
            // 步驟 1: 立即鎖定所有 UI 互動，作為初始狀態
            this.Cursor = Cursors.WaitCursor;
            btnEnable.Enabled = false;
            btnDisable.Enabled = false;
            grpPhysPanel.Enabled = false;
            chkStartKeyboardOnLogon.Enabled = false;
            cboLanguage.Enabled = false;
            btnCheckUpdates.Enabled = false;

            // 步驟 2: 根據選定的語言更新 UI 文字
            UpdateUIForLanguage();
        }

        /// <summary>
        /// 表單顯示後才執行的非同步初始化邏輯。
        /// </summary>
        private async void MainForm_Shown(object sender, EventArgs e)
        {
            // 啟動時檢查日誌大小，若過大則自動輪替
            // 確保長期使用下來，使用者每次開啟程式都是從乾淨或較小的日誌開始
            CheckAndArchiveLogFile();

            _isInitializing = true;
            await RerunChecksAndLog(); // 執行所有初始檢查並記錄結果
            _isInitializing = false; // 待所有檢查完成後，才算初始化完畢
        }

        //======================================================================
        // 核心邏輯 - 檢查 (Core Logic - Checks)
        //======================================================================

        /// <summary>
        /// 從登錄檔讀取並驗證 Windows 組建版本是否符合最低要求。
        /// </summary>
        /// <returns>如果 OS 版本符合或更高，則返回 <c>true</c>。</returns>
        private bool CheckWindowsBuild()
        {
            try
            {
                // 從登錄檔讀取 CurrentBuild (主組建號) 和 UBR (修訂號)
                string? currentBuildStr = Registry.GetValue($@"HKEY_LOCAL_MACHINE\{REG_PATH_PARENT}", "CurrentBuild", null)?.ToString();
                string? currentRevisionStr = Registry.GetValue($@"HKEY_LOCAL_MACHINE\{REG_PATH_PARENT}", "UBR", null)?.ToString();

                if (string.IsNullOrEmpty(currentBuildStr) || string.IsNullOrEmpty(currentRevisionStr) ||
                    !int.TryParse(currentBuildStr, out int currentBuild) ||
                    !int.TryParse(currentRevisionStr, out int currentRevision))
                {
                    string errorMsg = Resources.Strings.ErrorReadRegistry;
                    Log(errorMsg);
                    MessageBox.Show(errorMsg, Resources.Strings.VersionIncompatible, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                string versionString = $"{currentBuild}.{currentRevision}";
                Log(string.Format(Resources.Strings.YourBuildVersion, versionString));

                // 版本比較邏輯
                bool isCompatible = false;

                if (currentBuild > 26220) // 允許未來高於 26220 的任何版本
                {
                    isCompatible = true;
                }
                else if (currentBuild == 26220)
                {
                    isCompatible = (currentRevision >= 6972); // 26220.6972+
                }
                else if (currentBuild == 26200)
                {
                    isCompatible = (currentRevision >= 7015); // 26200.7015+
                }
                else if (currentBuild == 26100)
                {
                    isCompatible = (currentRevision >= 7019); // 26100.7019+
                }

                if (!isCompatible)
                {
                    string requirementString = "26100.7019+ / 26200.7015+ / 26220.6972+";
                    Log(string.Format(Resources.Strings.ErrorBuildTooLow, versionString));
                    Log(string.Format(Resources.Strings.RequiredBuild, requirementString));

                    string message = string.Format(Resources.Strings.ErrorBuildTooLow, versionString) +
                                     "\n\n" +
                                     string.Format(Resources.Strings.RequiredBuild, requirementString) +
                                     "\n\n" +
                                     Resources.Strings.UpdateWindowsPrompt;

                    MessageBox.Show(message,
                                    Resources.Strings.VersionIncompatible,
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Warning);

                    return false;
                }

                Log(Resources.Strings.VersionOK);
                return true;
            }
            catch (Exception ex)
            {
                string errorMsg = string.Format(Resources.Strings.ErrorCheckBuild, ex.Message);
                Log(errorMsg);
                MessageBox.Show(errorMsg, Resources.Strings.HandleExceptionTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// 判斷是否為 26220.7271 或更新的原生支援版本 (Native Build)。
        /// </summary>
        private static bool IsNativeSupportBuild()
        {
            try
            {
                string? currentBuildStr = Registry.GetValue($@"HKEY_LOCAL_MACHINE\{REG_PATH_PARENT}", "CurrentBuild", null)?.ToString();
                string? currentRevisionStr = Registry.GetValue($@"HKEY_LOCAL_MACHINE\{REG_PATH_PARENT}", "UBR", null)?.ToString();

                if (int.TryParse(currentBuildStr, out int build) && int.TryParse(currentRevisionStr, out int revision))
                {
                    // Build > 26220 或 Build = 26220 且 Revision >= 7271
                    if (build > 26220) return true;
                    if (build == 26220 && revision >= 7271) return true;
                }
            }
            catch { /* 忽略錯誤 */ }
            return false;
        }

        /// <summary>
        /// 取得「停用」操作時應針對的功能 ID 清單。
        /// 1. 26220.7271+ (Native Build) -> 停用 3 個 ID (含 59765208)。
        /// 2. Legacy Build -> 停用 2 個 ID (排除 59765208)。
        /// 此邏輯不考慮螢幕尺寸。
        /// </summary>
        private static uint[] GetIdsToDisable()
        {
            return IsNativeSupportBuild() ? ALL_FEATURE_IDS : BASIC_FEATURE_IDS;
        }

        /// <summary>
        /// 判斷是否為掌機尺寸 (0 < 尺寸 <= 9.5")。
        /// </summary>
        private static bool IsHandheldDevice(double diagonalInches)
        {
            return diagonalInches > 0 && diagonalInches <= MAX_DIAGONAL_INCHES;
        }

        /// <summary>
        /// 根據系統版本、螢幕尺寸以及「是否存在覆寫」來取得功能 ID 清單。
        /// </summary>
        /// <param name="isNativeSupport">是否為 Native Build 26220.7271+ 版本</param>
        /// <param name="diagonalInches">偵測到的螢幕對角線尺寸</param>
        /// <param name="isOverridePresent">是否偵測到 CS 或 Drv 正在運作</param>
        private static uint[] GetRequiredFeatureIds(bool isNativeSupport, double diagonalInches, bool isOverridePresent)
        {
            // Legacy Build：一律只支援 BASIC
            if (!isNativeSupport) return BASIC_FEATURE_IDS;

            // 原生支援版本 (Native Build 26220.7271+)：
            // 判斷是否需要啟用 59765208 (非掌機模式)
            // 條件 A: 偵測到覆寫 (CS/Drv)。這意味著尺寸是假的 (如 7")，且使用者之前刻意安裝了覆寫，
            //         因此假設這是非掌機 (Desktop/Laptop)，需要完整功能。
            // 條件 B: 無覆寫，且真實尺寸大於掌機範圍 (> 9.5" 或 0)。
            if (isOverridePresent || !IsHandheldDevice(diagonalInches))
            {
                return ALL_FEATURE_IDS; // { 52580392, 50902630, 59765208 }
            }

            // 其他情況 ("無覆寫"且"尺寸符合掌機")：
            // 真掌機 -> 排除 59765208
            return BASIC_FEATURE_IDS; // { 52580392, 50902630 }
        }

        /// <summary>
        /// 檢查所有相關設定 (ViVe 功能、登錄檔、螢幕尺寸、排程工作/驅動程式) 以確定功能的目前啟用狀態，並相應地更新 UI。
        /// 綜合性的檢查，處理多種中間狀態（例如，需要修正）。
        /// 此方法現在被設計為在背景執行緒上執行。
        /// </summary>
        private void CheckCurrentStatus()
        {
            try
            {
                // ======================================================================
                // 步驟 1: 基礎環境與核心檢查
                // ======================================================================

                // 1.1 取得螢幕實體尺寸
                var (success, size) = PanelManager.GetDisplaySize();
                // 若無法讀取尺寸或為 0，視為未定義
                double diagonalInches = (success && (size.WidthMm > 0 || size.HeightMm > 0))
                    ? Math.Sqrt((size.WidthMm * size.WidthMm) + (size.HeightMm * size.HeightMm)) / INCHES_TO_MM
                    : 0;

                // 1.2 判斷版本
                bool isNativeSupport = IsNativeSupportBuild(); // 26220.7271+

                // ======================================================================
                // 步驟 2: 檢查覆寫機制狀態 (個別檢查 CS 與 Drv)
                // ======================================================================

                bool isPhysPanelCSActive = TaskSchedulerManager.SetPanelDimensionsTaskExists();
                bool isPhysPanelDrvActive = DriverManager.IsDriverServiceInstalled();
                bool isScreenOverridePresent = isPhysPanelCSActive || isPhysPanelDrvActive;

                // 1.3 判斷功能 ID 需求
                uint[] requiredIds = GetRequiredFeatureIds(isNativeSupport, diagonalInches, isScreenOverridePresent);

                // 1.4 檢查功能 ID 狀態
#if EXPERIMENTAL
                Log("--- [EXPERIMENTAL] Feature ID Status ---");
                bool allRequiredEnabled = true;
                foreach (uint id in requiredIds)
                {
                    var configObj = FeatureManager.QueryFeatureConfiguration(id, RTL_FEATURE_CONFIGURATION_TYPE.Runtime);
                    bool isEnabled = configObj is RTL_FEATURE_CONFIGURATION config && config.EnabledState == RTL_FEATURE_ENABLED_STATE.Enabled;
                    Log($"ID {id}: {(isEnabled ? "Enabled (OK)" : "Disabled/Inactive")}");
                    if (!isEnabled) allRequiredEnabled = false;
                }
                Log("----------------------------------------");
#else
                bool allRequiredEnabled = requiredIds.All(id =>
                    FeatureManager.QueryFeatureConfiguration(id, RTL_FEATURE_CONFIGURATION_TYPE.Runtime) is RTL_FEATURE_CONFIGURATION config &&
                    config.EnabledState == RTL_FEATURE_ENABLED_STATE.Enabled);
#endif
                // 1.5 檢查登錄檔
                object? regValue = Registry.GetValue($"HKEY_LOCAL_MACHINE\\{REG_PATH}", REG_VALUE, null);
                bool isRegistrySet = regValue is int intValue && intValue == 0x2E;
                string registryStatusString = (regValue == null) ? Resources.Strings.LogRegStatusFalseNotExist :
                                              (isRegistrySet ? Resources.Strings.LogRegStatusTrue : string.Format(Resources.Strings.LogRegStatusFalseWrongValue, regValue));
                bool isCoreEnabled = allRequiredEnabled && isRegistrySet;

                // ======================================================================
                // 步驟 3: 判斷是否「需要」修正 (ScreenOverrideRequired)
                // ======================================================================

                // 修正判斷邏輯：
                // 1. Native Build：系統原生支援，不需要工具介入修正尺寸 -> Required = False
                // 2. Legacy Build：若尺寸為 0 或 > 9.5" (例如桌機、筆電，超出了此功能預期的掌機尺寸範圍)，則需要修正 -> Required = True
                //    (注意：若 CS 已生效將尺寸覆寫為 7"，此處 diagonalInches=7，判斷為 False，這代表「已修正/狀態良好」)
                bool isScreenOverrideRequired = false;
                if (!isNativeSupport)
                {
                    bool isUndefined = diagonalInches == 0;
                    if (isUndefined || diagonalInches > MAX_DIAGONAL_INCHES)
                    {
                        isScreenOverrideRequired = true;
                    }
                }

                // ======================================================================
                // 步驟 4: 驅動模式可用性檢查
                // ======================================================================
                bool isTestSigningOn = DriverManager.IsTestSigningEnabled(); // 檢查測試簽章模式是否啟用
                bool isArchSupported = HardwareHelper.IsDriverSupportedArchitecture(); // 檢查架構是否相容
                bool isScreenSizeRestricted = RESTRICT_DRV_MODE_ON_LARGE_SCREEN && (diagonalInches > MAX_DIAGONAL_INCHES); // 根據 Drv 功能旗標和螢幕尺寸，判斷是否存在螢幕尺寸限制
                bool isDrvModeAvailable = !isNativeSupport && isTestSigningOn && !isScreenSizeRestricted && isArchSupported; // Native 模式下停用 Drv

                // ======================================================================
                // 步驟 5: 決定 UI 狀態
                // ======================================================================

                string statusText;
                Color statusColor;
                string btnEnableText = Resources.Strings.btnEnable_Text;
                bool btnEnableEnabled, btnDisableEnabled, grpPhysPanelEnabled;
                bool enableOpenSettings = false, radPhysPanelDrvChecked = false, radPhysPanelCSChecked = true; // 預設 PhysPanelCS

                if (isCoreEnabled) // 功能 ID 和登錄檔是否已設定
                {
                    if (isNativeSupport)
                    {
                        // Native 模式，已啟用
                        statusText = Resources.Strings.StatusEnabled;
                        statusColor = Color.LimeGreen;
                        btnEnableEnabled = false; // 停用「啟用」按鈕
                        btnDisableEnabled = true; // 啟用「停用」按鈕
                        grpPhysPanelEnabled = false; // 鎖定覆寫選項，防止使用者切換模式
                        enableOpenSettings = true; // 開放 FSE 設定按鈕

                        // Native 模式下預設選中 CS (代表使用工作排程機制，即便是 Reg Only)
                        radPhysPanelCSChecked = true;
                        radPhysPanelDrvChecked = false;
                    }
                    else
                    {
                        // Legacy 模式，需要覆寫但未生效
                        if (isScreenOverrideRequired)
                        {
                            enableOpenSettings = false; // 需要修正 (Error 或 Needs Fix) -> 不算啟用，故不開放 FSE 設定按鈕

                            // 偵測到是驅動程式模式 (Drv) 處於無效狀態
                            if (isPhysPanelDrvActive)
                            {
                                statusText = Resources.Strings.StatusDriverErrorNeedsDisable;
                                statusColor = Color.Red; // 使用更醒目的紅色來表示嚴重錯誤
                                btnEnableEnabled = false; // 停用「啟用」按鈕
                                btnDisableEnabled = true; // 啟用「停用」按鈕，這是唯一允許的操作
                                grpPhysPanelEnabled = false; // 鎖定覆寫選項，防止使用者切換模式
                                radPhysPanelDrvChecked = true; // 保持顯示目前是驅動模式
                            }
                            // 非驅動程式模式下的「需要修正」狀態 (例如排程工作損壞或從未安裝覆寫)
                            else
                            {
                                statusText = Resources.Strings.StatusNeedsFix;
                                statusColor = Color.Orange; // 使用更醒目的橘色來表示修正操作
                                btnEnableText = Resources.Strings.btnEnable_Text_Fix;
                                btnEnableEnabled = true; // 啟用「修正」按鈕
                                btnDisableEnabled = true; // 啟用「停用」按鈕，同時提供完全停用的操作
                                grpPhysPanelEnabled = true; // 啟用覆寫選項，允許選擇修正方式
                                radPhysPanelDrvChecked = false;
                                radPhysPanelCSChecked = true; // 預設 PhysPanelCS
                            }
                        }
                        else
                        {
                            // 狀態良好，已啟用且尺寸正確 (無論是天生符合，或是覆寫已成功生效)
                            statusText = isPhysPanelDrvActive ? Resources.Strings.StatusEnabledDriverMode : (isPhysPanelCSActive ? Resources.Strings.StatusEnabledSchedulerMode : Resources.Strings.StatusEnabled);
                            statusColor = Color.LimeGreen;
                            btnEnableEnabled = false; // 停用「啟用」按鈕
                            btnDisableEnabled = true; // 啟用「停用」按鈕
                            grpPhysPanelEnabled = false; // 停用覆寫選項
                            enableOpenSettings = true; // 開放 FSE 設定按鈕
                            radPhysPanelDrvChecked = isPhysPanelDrvActive;
                            radPhysPanelCSChecked = isPhysPanelCSActive || !isPhysPanelDrvActive;
                        }
                    }
                }
                else
                {
                    enableOpenSettings = false; // 不開放 FSE 設定按鈕

                    // 未啟用 (功能 ID 和登錄檔未設定)
                    statusText = Resources.Strings.StatusDisabled;
                    statusColor = Color.Tomato;
                    btnEnableText = Resources.Strings.btnEnable_Text;
                    btnEnableEnabled = true; // 啟用「啟用」按鈕
                    btnDisableEnabled = false; // 停用「停用」按鈕

                    if (isNativeSupport)
                    {
                        grpPhysPanelEnabled = false;
                        radPhysPanelCSChecked = true;
                    }
                    else
                    {
                        grpPhysPanelEnabled = isScreenOverrideRequired; // 只有在螢幕尺寸確實需要覆寫時，才啟用此選項群組
                        radPhysPanelDrvChecked = isPhysPanelDrvActive;
                        radPhysPanelCSChecked = isPhysPanelCSActive || !isPhysPanelDrvActive;
                    }
                }

                // ======================================================================
                // 步驟 6: 更新 UI
                // ======================================================================
                this.Invoke((Action)(() =>
                {
                    _isUpdatingStatus = true; // 開始更新 UI

                    // 設定 FSE 設定按鈕的狀態
                    btnOpenSettings.Enabled = enableOpenSettings;
                    // 開放 MS Store 按鈕
                    btnCheckUpdates.Enabled = true;

                    // 設定遊戲控制器鍵盤啟動選項的可用性
                    bool hasTouchSupport = HardwareHelper.IsTouchScreenAvailable();
                    bool isStartKeyboardTaskActive = TaskSchedulerManager.StartGamepadKeyboardOnLogonTaskExists();

                    chkStartKeyboardOnLogon.Enabled = !hasTouchSupport; // 讓桌電與筆電使用原本僅支援觸控裝置的遊戲控制器鍵盤
                    chkStartKeyboardOnLogon.Checked = isStartKeyboardTaskActive;
                    toolTip.SetToolTip(chkStartKeyboardOnLogon, hasTouchSupport ? Resources.Strings.TooltipTouchEnabled : Resources.Strings.TooltipTouchDisabled); // 設定 ToolTip 提示，向使用者解釋選項

                    // 安全機制：如果 Drv 模式因任何限制而變為不可用，則強制切換回 CS 模式
                    radPhysPanelDrv.Enabled = isDrvModeAvailable;
                    if (!isDrvModeAvailable && radPhysPanelDrv.Checked) radPhysPanelCS.Checked = true;

                    // 設定從步驟 5 邏輯中計算出來的值
                    lblStatus.Text = statusText;
                    lblStatus.ForeColor = statusColor;
                    btnEnable.Text = btnEnableText;
                    btnEnable.Enabled = btnEnableEnabled;
                    btnDisable.Enabled = btnDisableEnabled;
                    grpPhysPanel.Enabled = grpPhysPanelEnabled;
                    radPhysPanelDrv.Checked = radPhysPanelDrvChecked;
                    radPhysPanelCS.Checked = radPhysPanelCSChecked;
                }));

                // ======================================================================
                // 步驟 7: 寫入日誌 (顯示 CS 與 Drv 狀態)
                // ======================================================================
                Log(string.Format(Resources.Strings.LogTouchSupportStatus, HardwareHelper.IsTouchScreenAvailable()));

                if (!isNativeSupport && !isTestSigningOn) Log(Resources.Strings.LogTestSigningDisabled); // 如果測試簽章模式未開啟
                if (!isNativeSupport && !isArchSupported) Log(Resources.Strings.LogArchNotSupported); // 如果架構不支援
                if (!isNativeSupport && isScreenSizeRestricted) Log(Resources.Strings.LogLargeScreenForceCS); // 只有在螢幕尺寸限制生效時

                // 明確顯示 CS 與 Drv 的狀態，並保留 ScreenOverrideRequired 供判斷
                // 這樣可以清楚看到：雖然是 Native 模式，但可能 CS=True (Reg 工作)
                Log(string.Format(Resources.Strings.LogStatusCheckSummaryDetail,
                    isCoreEnabled,
                    isNativeSupport,
                    registryStatusString,
                    isScreenOverrideRequired,
                    isPhysPanelCSActive,
                    isPhysPanelDrvActive,
                    diagonalInches,
                    isTestSigningOn));
                Log(statusText); // 最終判斷的狀態文字
            }
            catch (Exception ex)
            {
                // 發生例外時，也要使用 Invoke 更新 UI
                this.Invoke((Action)(() =>
                {
                    lblStatus.Text = Resources.Strings.StatusUnknownError;
                    lblStatus.ForeColor = Color.OrangeRed;
                }));
                LogError(string.Format(Resources.Strings.ErrorCheckStatus, ex.Message));
            }
            finally
            {
                _isUpdatingStatus = false; // 結束更新 UI
            }
        }

        //======================================================================
        // 核心邏輯 - 啟用/停用 (Core Logic - Enable/Disable)
        //======================================================================

        /// <summary>
        /// 處理「啟用」按鈕的點選事件，執行所有啟用功能的必要步驟。
        /// </summary>
        private async void btnEnable_Click(object sender, EventArgs e)
        {
            // 在執行新操作前，先將目前的日誌封存備份
            ArchiveLogFile();

            this.Cursor = Cursors.WaitCursor;

            // 在開始長時間操作之前，立即鎖定所有 UI 互動
            btnEnable.Enabled = false;
            btnDisable.Enabled = false;
            grpPhysPanel.Enabled = false;
            cboLanguage.Enabled = false;
            chkStartKeyboardOnLogon.Enabled = false;
            btnOpenSettings.Enabled = false;
            btnCheckUpdates.Enabled = false;

            try
            {
                Log(Resources.Strings.LogBeginEnable);

                // 1. 備份並設定登錄檔
                Log(Resources.Strings.LogBackupAndSetRegistry);
                BackupAndSetRegistry();

                // 準備環境判斷參數
                bool isNativeSupport = IsNativeSupportBuild();
                var (success, size) = PanelManager.GetDisplaySize();
                // 若無法讀取尺寸，視為 0 (未定義)
                double diagonalInches = (success && (size.WidthMm > 0 || size.HeightMm > 0))
                    ? Math.Sqrt((size.WidthMm * size.WidthMm) + (size.HeightMm * size.HeightMm)) / INCHES_TO_MM
                    : 0;

                // 檢查目前是否有覆寫 (這會影響判斷裝置類型)
                // 如果偵測到 CS 或 Drv 正在運作，代表目前的 diagonalInches 可能是被覆寫過的 (例如 7")
                // 此時應將其視為「非掌機」來處理
                bool isOverridePresent = TaskSchedulerManager.SetPanelDimensionsTaskExists() ||
                                         DriverManager.IsDriverServiceInstalled();

                // 2. 啟用 ViVe 功能 (使用動態計算的 ID，考慮系統版本與覆寫狀態)
                uint[] idsToEnable = GetRequiredFeatureIds(isNativeSupport, diagonalInches, isOverridePresent);
                EnableViveFeatures(idsToEnable);

                // 3: 設定螢幕尺寸覆寫或維護排程
                bool operationSuccess = true; // 預設為成功

                if (isNativeSupport)
                {
                    Log(string.Format(Resources.Strings.LogNativeSupportDetected, diagonalInches, isOverridePresent));

                    // --- Native 支援模式：清理舊機制 ---

                    // 移除舊的驅動程式 (如果存在)，避免衝突
                    if (DriverManager.IsDriverServiceInstalled())
                    {
                        Log(Resources.Strings.LogRemovingOldDriver);
                        await Task.Run(() => DriverManager.UninstallDriver(msg => Log(msg)));
                    }

                    // 移除舊的工作排程 (如果存在)，稍後會根據需求重建
                    if (TaskSchedulerManager.SetPanelDimensionsTaskExists())
                    {
                        Log(Resources.Strings.LogRemovingOldTask);
                        TaskSchedulerManager.DeleteSetPanelDimensionsTask();
                    }

                    // --- Native 支援模式：判斷是否需要 Reg 工作 ---

                    // 判斷是否為「非掌機」(需要 Reg 工作)
                    // 條件：偵測到覆寫 (代表是桌機/筆電但被改過) OR 真實尺寸 > 9.5" (或未定義)
                    if (isOverridePresent || !IsHandheldDevice(diagonalInches))
                    {
                        Log(Resources.Strings.LogNonHandheldDetected);
                        // 建立僅執行 'reg' 指令的工作排程，確保開機時 DeviceForm 維持為 46
                        TaskSchedulerManager.CreateSetPanelDimensionsTask(regOnly: true);
                        Log(Resources.Strings.LogPanelTaskCreated);
                    }
                    else
                    {
                        // 真掌機 (無覆寫 且 尺寸 <= 9.5")：不需要任何工作排程
                        Log(Resources.Strings.LogHandheldDetected);
                    }
                }
                else
                {
                    // --- Legacy 相容模式：根據使用者選擇安裝 Drv 或 CS ---

                    if (radPhysPanelDrv.Checked)
                    {
                        Log(Resources.Strings.LogChoosingDriverMode);
                        // 執行前先移除 CS 工作，避免衝突
                        if (TaskSchedulerManager.SetPanelDimensionsTaskExists())
                        {
                            Log(Resources.Strings.LogRemovingOldTask);
                            TaskSchedulerManager.DeleteSetPanelDimensionsTask();
                        }

                        // 呼叫安裝方法並接收其回傳值
                        bool installSuccess = await Task.Run(() => DriverManager.InstallDriver(msg => Log(msg)));

                        // 檢查安裝是否成功
                        if (!installSuccess)
                        {
                            operationSuccess = false; // 標記操作失敗
                        }
                    }
                    else // radPhysPanelCS.Checked
                    {
                        Log(Resources.Strings.LogChoosingSchedulerMode);
                        // 執行前先移除 Drv 服務，避免衝突
                        if (DriverManager.IsDriverServiceInstalled())
                        {
                            Log(Resources.Strings.LogRemovingOldDriver);
                            await Task.Run(() => DriverManager.UninstallDriver(msg => Log(msg)));
                        }

                        // 執行標準的排程工作建立 (set 155 87 reg)
                        HandleTaskSchedulerCreation();
                    }
                }

                // 4. 流程總結與處理
                if (operationSuccess)
                {
                    // --- 成功路徑 ---
                    btnEnable.Enabled = false;
                    btnDisable.Enabled = false;
                    Log(Resources.Strings.LogEnableComplete, true);
                    PromptForRestart(Resources.Strings.PromptRestartCaptionSuccess);
                }
                else
                {
                    // --- 失敗路徑：執行還原 (Rollback) ---
                    LogError(Resources.Strings.LogOperationFailedRollingBack);

                    // 以相反的順序還原變更
                    // 步驟 1: 如果是驅動程式安裝失敗，則執行移除程序以清除殘留的裝置節點
                    if (radPhysPanelDrv.Checked)
                    {
                        await Task.Run(() => DriverManager.UninstallDriver(msg => Log(msg)));
                    }

                    // 步驟 2: 停用 ViVe 功能
                    DisableViveFeatures();

                    // 步驟 3: 還原登錄檔
                    RestoreRegistry();

                    LogError(Resources.Strings.LogRollbackComplete);
                }
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
            finally
            {
                this.Cursor = Cursors.Default;
                if (!_restartPending)
                {
                    await RerunChecksAndLog(); // 如果不需要重新開機，則重新整理狀態。無論成功、失敗還原，最後都重新整理一次狀態。
                }
            }
        }

        /// <summary>
        /// 處理「停用」按鈕的點選事件。
        /// </summary>
        private async void btnDisable_Click(object sender, EventArgs e)
        {
            // 在執行新操作前，先將目前的日誌封存備份
            ArchiveLogFile();

            this.Cursor = Cursors.WaitCursor;

            // 在開始長時間操作之前，立即鎖定所有 UI 互動
            btnEnable.Enabled = false;
            btnDisable.Enabled = false;
            grpPhysPanel.Enabled = false;
            cboLanguage.Enabled = false;
            chkStartKeyboardOnLogon.Enabled = false;
            btnOpenSettings.Enabled = false;
            btnCheckUpdates.Enabled = false;

            try
            {
                await PerformDisableActions(); // 呼叫共用的停用邏輯

                btnEnable.Enabled = false;
                btnDisable.Enabled = false;
                Log(Resources.Strings.LogDisableComplete, true);
                PromptForRestart(Resources.Strings.PromptRestartCaptionDisableSuccess);
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
            finally
            {
                this.Cursor = Cursors.Default;
                if (!_restartPending)
                {
                    await RerunChecksAndLog();
                }
            }
        }

        /// <summary>
        /// 開啟 Windows 設定中的「全螢幕體驗」頁面。
        /// URI: ms-settings:gaming-fullscreen
        /// </summary>
        private void btnOpenSettings_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:gaming-fullscreen") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                LogError(string.Format(Resources.Strings.ErrorOpenSettings, ex.Message));
                MessageBox.Show(
                    Resources.Strings.MsgOpenSettingsManual,
                    Resources.Strings.HandleExceptionTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 開啟 Microsoft Store 並跳轉至「更新與下載」頁面。
        /// URI: ms-windows-store://downloadsandupdates
        /// </summary>
        private void btnCheckUpdates_Click(object sender, EventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("ms-windows-store://downloadsandupdates") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                LogError(string.Format(Resources.Strings.ErrorOpenStore, ex.Message));
                MessageBox.Show(
                    Resources.Strings.MsgOpenStoreManual,
                    Resources.Strings.HandleExceptionTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        #region Helper Methods for Enable/Disable
        /// <summary>
        /// 執行停用功能的所有核心步驟。
        /// </summary>
        private async Task PerformDisableActions()
        {
            Log(Resources.Strings.LogBeginDisable);

            // 步驟 1: 停用 ViVe 功能
            DisableViveFeatures();

            // 步驟 2: 還原登錄檔值
            RestoreRegistry();

            // 步驟 3: 移除鍵盤啟動工作
            if (TaskSchedulerManager.StartGamepadKeyboardOnLogonTaskExists())
            {
                Log(Resources.Strings.LogDeletingKeyboardTask);
                TaskSchedulerManager.DeleteStartGamepadKeyboardOnLogonTask();
                Log(Resources.Strings.LogKeyboardTaskDeleted, true);
            }

            // 步驟 4: 移除所有可能的覆寫方法 (包括 CS 和 Drv)
            if (TaskSchedulerManager.SetPanelDimensionsTaskExists())
            {
                Log(Resources.Strings.LogDeletingTask);
                TaskSchedulerManager.DeleteSetPanelDimensionsTask();
                Log(Resources.Strings.LogPanelTaskDeleted, true);
            }

            if (DriverManager.IsDriverServiceInstalled())
            {
                Log(Resources.Strings.LogRemovingPhysPanelDrv);
                // 檢查 Uninstall 的結果
                bool uninstallSuccess = await Task.Run(() => DriverManager.UninstallDriver(msg => Log(msg)));
                if (!uninstallSuccess)
                {
                    // 如果移除失敗，只記錄錯誤，不停用流程，因為其他清理步驟 (如還原登錄檔) 更重要
                    LogError(Resources.Strings.LogErrorDriverRemoveFailedGeneral);
                }
            }
        }

        /// <summary>
        /// 備份現有的登錄檔值，然後設定新的值。
        /// </summary>
        private void BackupAndSetRegistry()
        {
            if (!File.Exists(BackupFilePath))
            {
                object? currentValue = Registry.GetValue($"HKEY_LOCAL_MACHINE\\{REG_PATH}", REG_VALUE, null);
                if (currentValue != null)
                {
                    File.WriteAllText(BackupFilePath, currentValue.ToString() ?? "");
                    Log(string.Format(Resources.Strings.LogRegistryBackupSuccess, currentValue, BackupFilePath));
                }
                else
                {
                    File.WriteAllText(BackupFilePath, "DELETE_ON_RESTORE");
                    Log(Resources.Strings.LogRegistryBackupMarkedForDelete);
                }
            }
            Registry.SetValue($"HKEY_LOCAL_MACHINE\\{REG_PATH}", REG_VALUE, 0x2E, RegistryValueKind.DWord); // 0x2E = 46
            Log(Resources.Strings.LogRegistrySetSuccess);
        }

        /// <summary>
        /// 啟用所需的 ViVe 功能。
        /// </summary>
        private void EnableViveFeatures(uint[] featureIds)
        {
            Log(string.Format(Resources.Strings.LogEnablingFeatures, string.Join(", ", featureIds)));
            var updates = Array.ConvertAll(featureIds, id => new RTL_FEATURE_CONFIGURATION_UPDATE
            {
                FeatureId = id,
                EnabledState = RTL_FEATURE_ENABLED_STATE.Enabled,
                Operation = RTL_FEATURE_CONFIGURATION_OPERATION.FeatureState | RTL_FEATURE_CONFIGURATION_OPERATION.VariantState,
                Priority = RTL_FEATURE_CONFIGURATION_PRIORITY.User
            });
            FeatureManager.SetFeatureConfigurations(updates, RTL_FEATURE_CONFIGURATION_TYPE.Runtime);
            FeatureManager.SetFeatureConfigurations(updates, RTL_FEATURE_CONFIGURATION_TYPE.Boot);
            Log(Resources.Strings.LogFeaturesEnabledSuccess);
        }

        /// <summary>
        /// 處理工作排程的建立邏輯。
        /// </summary>
        private void HandleTaskSchedulerCreation()
        {
            Log(Resources.Strings.LogReadingScreenSize);
            var (success, size) = PanelManager.GetDisplaySize();
            if (success)
            {
                Log(string.Format(Resources.Strings.LogScreenSizeSuccess, size.WidthMm, size.HeightMm));
                // 檢查尺寸是否未定義 (即寬度和高度是否同時為 0)
                bool isUndefined = size.WidthMm == 0 && size.HeightMm == 0;
                // 計算螢幕對角線英寸
                // 如果 isUndefined 為 true (尺寸未定義)，則 diagonalInches 為 0
                // 如果 isUndefined 為 false (尺寸已定義)，則使用畢氏定理計算對角線 (mm)，再除以 INCHES_TO_MM (25.4) 轉換為英寸
                double diagonalInches = isUndefined ? 0 : Math.Sqrt((size.WidthMm * size.WidthMm) + (size.HeightMm * size.HeightMm)) / INCHES_TO_MM;
                // 判斷螢幕尺寸是否過大 (9.5")
                // 必須同時滿足兩個條件才算過大：
                // 1. 尺寸必須是已定義的 (!isUndefined)
                // 2. 且 (&&) 計算出來的對角線英寸大於最大允許值 (9.5")
                bool isTooLarge = !isUndefined && diagonalInches > MAX_DIAGONAL_INCHES;

                // 判斷螢幕尺寸是否「未定義」(isUndefined) 或「過大」(isTooLarge)
                if (isUndefined || isTooLarge)
                {
                    // 如果是 (即尺寸有問題)，則需要建立排程工作來修正：
                    // 1. 如果 isUndefined 為 true，顯示 "尺寸未定義" (LogScreenSizeUndefined)
                    // 2. 否則 (表示 isTooLarge 為 true)，顯示 "尺寸過大" (LogScreenSizeTooLarge)，並傳入當前英寸和最大英寸
                    Log(isUndefined ? Resources.Strings.LogScreenSizeUndefined : string.Format(Resources.Strings.LogScreenSizeTooLarge, diagonalInches, MAX_DIAGONAL_INCHES));
                    // 呼叫 TaskSchedulerManager 來建立一個 Windows 排程工作，用於設定/修正螢幕尺寸
                    TaskSchedulerManager.CreateSetPanelDimensionsTask();
                    Log(Resources.Strings.LogPanelTaskCreated);
                }
                else
                {
                    Log(string.Format(Resources.Strings.LogTaskNotNeeded, diagonalInches, MAX_DIAGONAL_INCHES));
                }
            }
            else
            {
                LogError(Resources.Strings.LogErrorReadingScreenSizeEnable);
            }
        }

        /// <summary>
        /// 從備份還原登錄檔。
        /// </summary>
        private void RestoreRegistry()
        {
            Log(Resources.Strings.LogCheckingRegistryRestore);
            try
            {
                if (File.Exists(BackupFilePath))
                {
                    Log(Resources.Strings.LogBackupFound);
                    string backupContent = File.ReadAllText(BackupFilePath);

                    using (RegistryKey? key = Registry.LocalMachine.OpenSubKey(REG_PATH, true))
                    {
                        if (key != null)
                        {
                            if (backupContent == "DELETE_ON_RESTORE") // 如果備份內容是特殊標記，則刪除該值
                            {
                                key.DeleteValue(REG_VALUE, false); // false 表示如果值不存在也不會拋出例外
                                Log(string.Format(Resources.Strings.LogRegistryValueRemoved, REG_VALUE));
                            }
                            else if (int.TryParse(backupContent, out int backupValue)) // 如果是數字，則還原該數值
                            {
                                key.SetValue(REG_VALUE, backupValue, RegistryValueKind.DWord);
                                Log(string.Format(Resources.Strings.LogRegistryValueRestored, backupValue));
                            }
                            else // 備份檔內容未知，為安全起見，同樣刪除該值
                            {
                                key.DeleteValue(REG_VALUE, false);
                                Log(string.Format(Resources.Strings.LogRegistryBackupInvalid, REG_VALUE));
                            }
                        }
                    }
                    File.Delete(BackupFilePath);
                    Log(Resources.Strings.LogBackupDeleted);
                }
                else
                {
                    // 如果從未建立過備份檔，表示使用者從未點選過「啟用 Xbox 全螢幕體驗」。
                    // 在這種情況下，工具從未修改過登錄檔，因此最安全的作法是什麼都不做。
                    Log(Resources.Strings.LogNoBackupFound);
                }
            }
            catch (Exception ex)
            {
                LogError(string.Format(Resources.Strings.ErrorRestoringRegistry, ex.Message));
            }
        }

        /// <summary>
        /// 停用所需的 ViVe 功能。此方法從 PerformDisableActions 中提取出來以便重複使用。
        /// </summary>
        private void DisableViveFeatures()
        {
            // 取得應停用的 ID 清單
            uint[] idsToDisable = GetIdsToDisable();
            Log(string.Format(Resources.Strings.LogDisablingFeatures, string.Join(", ", idsToDisable)));
            var updates = Array.ConvertAll(idsToDisable, id => new RTL_FEATURE_CONFIGURATION_UPDATE
            {
                FeatureId = id,
                EnabledState = RTL_FEATURE_ENABLED_STATE.Disabled,
                Operation = RTL_FEATURE_CONFIGURATION_OPERATION.FeatureState | RTL_FEATURE_CONFIGURATION_OPERATION.VariantState,
                Priority = RTL_FEATURE_CONFIGURATION_PRIORITY.User
            });
            FeatureManager.SetFeatureConfigurations(updates, RTL_FEATURE_CONFIGURATION_TYPE.Runtime);
            FeatureManager.SetFeatureConfigurations(updates, RTL_FEATURE_CONFIGURATION_TYPE.Boot);
            Log(Resources.Strings.LogFeaturesDisabledSuccess);
        }
        #endregion

        //======================================================================
        // UI 流程控制 (UI Flow Control)
        //======================================================================

        /// <summary>
        /// 向使用者顯示必須重新開機的通知。
        /// 無論使用者點選「確定」還是關閉視窗，都會執行重新開機。
        /// </summary>
        /// <param name="caption">訊息方塊的標題。</param>
        private void PromptForRestart(string caption)
        {
            // 步驟 1: 顯示通知視窗。程式會在這裡暫停，直到視窗被關閉。
            MessageBox.Show(
                Resources.Strings.PromptRestartMessage, // 訊息為陳述句，例如：「設定已完成，電腦需要重新開機。」
                caption,
                MessageBoxButtons.OK,                   // 只提供「確定」按鈕
                MessageBoxIcon.Information);

            // 步驟 2: 當使用者關閉視窗後 (無論是按 OK 還是 X)，程式繼續執行到這裡。
            Log(Resources.Strings.LogUserRestartNow);
            try
            {
                // 5 秒後重新開機
                string shutdownArgs = $"/r /t 5 /c \"{Resources.Strings.ShutdownReasonEnable}\" /d p:4:1";

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "shutdown.exe",               // 要執行的檔案
                    Arguments = shutdownArgs,                // 傳遞給檔案的參數
                    CreateNoWindow = true,                   // 不要建立視窗
                    UseShellExecute = false,                 // 必須設為 false 才能讓 CreateNoWindow 生效
                    WindowStyle = ProcessWindowStyle.Hidden  // 再次確保隱藏
                };

                Process.Start(startInfo); // 使用設定好的 startInfo 啟動
                Application.Exit();
            }
            catch (Exception ex)
            {
                LogError(string.Format(Resources.Strings.ErrorRestartFailed, ex.Message));
                MessageBox.Show(Resources.Strings.MessageBoxRestartFailed, Resources.Strings.HandleExceptionTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                // 如果失敗，才鎖定 UI
                LockdownUIForRestart();
            }
        }

        /// <summary>
        /// 當使用者選擇稍後重啟時，鎖定 UI 以防止進一步操作。
        /// </summary>
        private void LockdownUIForRestart()
        {
            _restartPending = true;
            lblStatus.Text = Resources.Strings.StatusRestartPending;
            lblStatus.ForeColor = Color.Orange;
            btnEnable.Enabled = false;
            btnDisable.Enabled = false;
            cboLanguage.Enabled = false;
            grpPhysPanel.Enabled = false;
            chkStartKeyboardOnLogon.Enabled = false;
            Log(Resources.Strings.LogUserRestartLater);
        }

        //======================================================================
        // 多國語言 (Localization)
        //======================================================================

        /// <summary>
        /// 初始化語言下拉選單，並根據目前系統語言自動選取。
        /// </summary>
        private void InitializeLanguage()
        {
            cboLanguage.Items.Add("English");
            cboLanguage.Items.Add("繁體中文");
            cboLanguage.Items.Add("简体中文");
            cboLanguage.Items.Add("日本語");
            cboLanguage.Items.Add("한국어");
            cboLanguage.Items.Add("Deutsch");
            cboLanguage.Items.Add("Français");
            cboLanguage.Items.Add("Русский");

            string currentCultureName = Thread.CurrentThread.CurrentUICulture.Name;

            if (currentCultureName.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) ||
                currentCultureName.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase) ||
                currentCultureName.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase) ||
                currentCultureName.StartsWith("zh-MO", StringComparison.OrdinalIgnoreCase))
            {
                cboLanguage.SelectedIndex = 1; // 繁體中文
            }
            else if (currentCultureName.StartsWith("zh-CN", StringComparison.OrdinalIgnoreCase) ||
                     currentCultureName.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase) ||
                     currentCultureName.StartsWith("zh-SG", StringComparison.OrdinalIgnoreCase) ||
                     currentCultureName.StartsWith("zh-MY", StringComparison.OrdinalIgnoreCase))
            {
                cboLanguage.SelectedIndex = 2; // 简体中文
            }
            else if (currentCultureName.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) cboLanguage.SelectedIndex = 3; // 日本語
            else if (currentCultureName.StartsWith("ko", StringComparison.OrdinalIgnoreCase)) cboLanguage.SelectedIndex = 4; // 한국어
            else if (currentCultureName.StartsWith("de", StringComparison.OrdinalIgnoreCase)) cboLanguage.SelectedIndex = 5; // Deutsch
            else if (currentCultureName.StartsWith("fr", StringComparison.OrdinalIgnoreCase)) cboLanguage.SelectedIndex = 6; // Français
            else if (currentCultureName.StartsWith("ru", StringComparison.OrdinalIgnoreCase)) cboLanguage.SelectedIndex = 7; // Русский
            else cboLanguage.SelectedIndex = 0; // Default to English
        }

        /// <summary>
        /// 根據目前的在地化設定，更新所有 UI 控制項的文字。
        /// </summary>
        private void UpdateUIForLanguage()
        {
            // 取得版本號字串，如果版本資訊不存在，使用一個預設值 (未知版本)
            // Version.ToString(3) 的格式是 "Major.Minor.Build"
            string versionString = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? Resources.Strings.UnknownVersion;

            // 建立基礎標題："程式名稱 v主版號.次版號.組建編號"
            string windowTitle = $"{Resources.Strings.MainFormTitle} v{versionString}";

#if EXPERIMENTAL
            // 只有在編譯為實驗版 (定義了 EXPERIMENTAL 常數) 時才會執行此區塊。
            // 讀取 Git Hash 並將其附加到視窗標題後方，讓使用者或測試者能一眼識別目前版本。
            string gitHash = GetGitHash();
            if (gitHash != "N/A")
            {
                windowTitle += $" (Preview: {gitHash})";
            }
#endif
            this.Text = windowTitle;

            lblStatus.Text = Resources.Strings.StatusChecking; // "狀態：偵測中...";
            grpPhysPanel.Text = Resources.Strings.grpPhysPanel_Text;
            radPhysPanelCS.Text = Resources.Strings.radPhysPanelCS_Text;
            radPhysPanelDrv.Text = Resources.Strings.radPhysPanelDrv_Text;
            grpActions.Text = Resources.Strings.grpActions_Text;
            grpOutput.Text = Resources.Strings.grpOutput_Text;
            btnDisable.Text = Resources.Strings.btnDisable_Text;
            btnEnable.Text = Resources.Strings.btnEnable_Text;
            chkStartKeyboardOnLogon.Text = Resources.Strings.chkStartKeyboardOnLogon_Text;
            btnOpenSettings.Text = Resources.Strings.btnOpenSettings_Text;
            btnCheckUpdates.Text = Resources.Strings.btnCheckUpdates_Text;
        }

        /// <summary>
        /// 重新執行檢查並清除 UI 上的舊訊息。日誌會繼續附加到現有檔案。
        /// </summary>
        private async Task RerunChecksAndLog()
        {
            // 步驟 1: (再次)鎖定所有 UI 互動 (確保切換語言時也會鎖定)
            this.Cursor = Cursors.WaitCursor;
            btnEnable.Enabled = false;
            btnDisable.Enabled = false;
            grpPhysPanel.Enabled = false;
            chkStartKeyboardOnLogon.Enabled = false;
            cboLanguage.Enabled = false;

            try
            {
                txtOutput.Clear();

                // 步驟 2: 進行關鍵的前置檢查，確保 OS 版本符合要求 (在 UI 執行緒上)。
                if (!CheckWindowsBuild())
                {
                    // 如果版本不符，CheckWindowsBuild 內部已顯示錯誤訊息，此處直接關閉應用程式。
                    Application.Exit();
                    return;
                }

                // 取得版本號 (與 UpdateUIForLanguage 中的邏輯相同)
                string versionString = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? Resources.Strings.UnknownVersion;

                // 顯示歡迎訊息
                Log(Resources.Strings.WelcomeMessage);

                // 顯示程式標題與版本號 (與視窗標題列的格式一致)
                // 記錄版本資訊，並在此處記錄 Git Hash 以供偵錯 (無論是正式版或實驗版都記錄，方便排查)
                string logVersion = $"{Resources.Strings.MainFormTitle} v{versionString}";

                // 嘗試讀取 Git Hash
                string gitHash = GetGitHash();
                // 無論是「正式版」還是「實驗版」，只要能取得 Hash，都應記錄到日誌檔案中。
                // 這樣當用戶回報問題並提供 Log 檔時，開發者能精確定位是哪一次 Commit 的程式碼。
                if (gitHash != "N/A")
                {
                    // 將 Hash 加入日誌
                    logVersion += $" (Commit: {gitHash})";

#if EXPERIMENTAL
                    // 如果是實驗版，在日誌中額外標註 [EXPERIMENTAL BUILD] 以便區分
                    logVersion += " [EXPERIMENTAL BUILD]";
#endif
                }

                Log(logVersion);

                // 步驟 3: 執行耗時的非同步檢查 (使用 await Task.Run 等待背景執行緒的 CheckCurrentStatus 完成)
                await Task.Run(() => CheckCurrentStatus());
            }
            catch (Exception ex)
            {
                // 處理 RerunChecksAndLog 執行期間的意外錯誤
                HandleException(ex);
            }
            finally
            {
                // 步驟 4: 無論成功或失敗，都恢復游標和語言選單
                this.Cursor = Cursors.Default;
                cboLanguage.Enabled = true; // 重新啟用語言選單

                // 注意：btnEnable, btnDisable, grpPhysPanel, chkStartKeyboardOnLogon, btnOpenSettings, btnCheckUpdates
                // 的 Enabled 狀態由 CheckCurrentStatus 內部的 Invoke 決定，
                // 所以這裡不需要也不應該將它們設為 true。
            }
        }

        /// <summary>
        /// 處理語言下拉選單的選擇變更事件。
        /// </summary>
        private async void cboLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isInitializing) return;

            string cultureName = cboLanguage.SelectedIndex switch
            {
                1 => "zh-TW",
                2 => "zh-CN",
                3 => "ja-JP",
                4 => "ko-KR",
                5 => "de-DE",
                6 => "fr-FR",
                7 => "ru-RU",
                _ => "en-US",
            };

            var newCulture = new CultureInfo(cultureName);

            // 1. 設定「目前」執行緒 (UI 執行緒) 的語言
            Thread.CurrentThread.CurrentUICulture = newCulture;

            // 2. 強制設定 Resources.Strings 類別的靜態 Culture 屬性
            // 這會覆寫所有執行緒的預設行為，確保 Log 和 MessageBox 也使用新語言
            Resources.Strings.Culture = newCulture;

            UpdateUIForLanguage();

            _isInitializing = true;
            await RerunChecksAndLog(); // 呼叫修改後的 async 版本
            _isInitializing = false;
        }

        /// <summary>
        /// 當「在登入時啟動觸控鍵盤」核取方塊的狀態變更時觸發。
        /// </summary>
        private void chkStartKeyboardOnLogon_CheckedChanged(object sender, EventArgs e)
        {
            if (_isInitializing || _isUpdatingStatus) return;

            // 暫時停用控制項並顯示等待游標
            chkStartKeyboardOnLogon.Enabled = false;
            this.Cursor = Cursors.WaitCursor;

            try
            {
                if (chkStartKeyboardOnLogon.Checked)
                {
                    Log(Resources.Strings.LogCreatingKeyboardTask);
                    TaskSchedulerManager.CreateStartGamepadKeyboardOnLogonTask();
                    Log(Resources.Strings.LogKeyboardTaskCreated, true);
                }
                else
                {
                    Log(Resources.Strings.LogDeletingKeyboardTask);
                    TaskSchedulerManager.DeleteStartGamepadKeyboardOnLogonTask();
                    Log(Resources.Strings.LogKeyboardTaskDeleted, true);
                }
            }
            catch (Exception ex)
            {
                LogError(string.Format(Resources.Strings.ErrorKeyboardTask, ex.Message));
                // 如果發生錯誤，將核取方塊還原到先前的狀態
                _isInitializing = true;
                chkStartKeyboardOnLogon.Checked = !chkStartKeyboardOnLogon.Checked;
                _isInitializing = false;
            }
            finally
            {
                // 無論成功或失敗，都恢復游標並重新啟用控制項
                this.Cursor = Cursors.Default;
                chkStartKeyboardOnLogon.Enabled = true;
            }
        }

        /// <summary>
        /// 當「狀態」被點選兩下時，顯示版權資訊。
        /// </summary>
        private void lblStatus_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                string base64Copyright = "Q29weXJpZ2h0IMKpIDIwMjUgOGJpdDJxdWJpdCAo6YeO55CGKSBhbmQgY29udHJpYnV0b3JzCmh0dHBzOi8vZ2l0aHViLmNvbS84Yml0MnF1Yml0L1hib3hGdWxsU2NyZWVuRXhwZXJpZW5jZVRvb2wKTGljZW5zZWQgdW5kZXIgdGhlIEdOVSBHUEwtMy4wIExpY2Vuc2Uu";

                byte[] bytes = Convert.FromBase64String(base64Copyright);
                string copyrightText = System.Text.Encoding.UTF8.GetString(bytes);

                MessageBox.Show(copyrightText,
                                "Copyright",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
            }
            catch (Exception)
            {
                MessageBox.Show("Error displaying copyright information.", "Error");
            }
        }

        /// <summary>
        /// 取得 Git Commit Hash。
        /// 此方法負責讀取目前執行組件 (Executing Assembly) 的中繼資料 (Metadata)。
        /// 該資料是在編譯時期由 .csproj 中的 <c>AddGitHashToAssembly</c> 目標自動注入的。
        /// </summary>
        /// <returns>
        /// 返回 Git 的短 Hash (例如 "15e887c")；
        /// 如果編譯環境無法取得 Hash (例如未安裝 git)，則返回 "N/A"。
        /// </returns>
        private string GetGitHash()
        {
            var attr = Assembly.GetExecutingAssembly()
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "GitHash");
            return attr?.Value ?? "N/A";
        }

        #region Logging and Error Handling
        /// <summary>
        /// 將一般訊息記錄到 UI 文字方塊和日誌檔案中。
        /// </summary>
        private void Log(string message, bool isSuccess = false)
        {
            // 處理跨執行緒呼叫 UI
            if (txtOutput.InvokeRequired)
            {
                txtOutput.Invoke(new Action(() => Log(message, isSuccess)));
                return;
            }

            // 取得當下時間
            DateTime now = DateTime.Now;

            // 1. 建立 GUI 顯示用的訊息 (僅時間)
            string guiMessage = $"[{now:HH:mm:ss}] " + message;

            // 2. 建立寫入檔案用的訊息 (包含日期與時間)
            string fileMessage = $"[{now:yyyy-MM-dd HH:mm:ss}] " + message;

            // 更新 UI (使用 guiMessage)
            txtOutput.SelectionColor = isSuccess ? Color.LimeGreen : Color.Gainsboro;
            txtOutput.AppendText(guiMessage + Environment.NewLine);
            txtOutput.ScrollToCaret();

            // 將日誌寫入檔案 (使用 fileMessage)
            WriteToFile(fileMessage);
        }

        /// <summary>
        /// 將錯誤訊息記錄到 UI 文字方塊和日誌檔案中。
        /// </summary>
        private void LogError(string message)
        {
            // 處理跨執行緒呼叫 UI
            if (txtOutput.InvokeRequired)
            {
                txtOutput.Invoke(new Action(() => LogError(message)));
                return;
            }

            // 取得當下時間
            DateTime now = DateTime.Now;
            string errorTitle = Resources.Strings.HandleExceptionTitle;

            // 1. 建立 GUI 顯示用的訊息 (僅時間)
            string guiMessage = $"[{now:HH:mm:ss}] [{errorTitle}] " + message;

            // 2. 建立寫入檔案用的訊息 (包含日期與時間)
            string fileMessage = $"[{now:yyyy-MM-dd HH:mm:ss}] [{errorTitle}] " + message;

            // 更新 UI (使用 guiMessage)
            txtOutput.SelectionColor = Color.Tomato;
            txtOutput.AppendText(guiMessage + Environment.NewLine);
            txtOutput.ScrollToCaret();

            // 將日誌寫入檔案 (使用 fileMessage)
            WriteToFile(fileMessage);
        }

        /// <summary>
        /// 將格式化後的訊息附加到日誌檔案中。
        /// </summary>
        /// <param name="message">要寫入的完整訊息行。</param>
        private void WriteToFile(string message)
        {
            try
            {
                // 使用 File.AppendAllText 簡潔地處理檔案不存在或需要附加內容的情況
                // 使用 UTF8 編碼以支援多國語言
                File.AppendAllText(_logFilePath, message + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // 如果寫入日誌失敗 (例如權限問題)，就在 UI 上顯示一個無法寫入的提示，但不要因此中斷程式的主要功能
                if (!txtOutput.IsDisposed)
                {
                    string errorTimestamp = $"[{DateTime.Now:HH:mm:ss}] [{Resources.Strings.LogLoggingErrorPrefix}] ";
                    txtOutput.SelectionColor = Color.OrangeRed;
                    txtOutput.AppendText(errorTimestamp + string.Format(Resources.Strings.ErrorWritingLogFile, ex.Message) + Environment.NewLine);
                }
            }
        }

        /// <summary>
        /// 檢查日誌檔案大小，如果超過 1MB 則進行封存。
        /// 在啟動時呼叫。
        /// </summary>
        private void CheckAndArchiveLogFile()
        {
            if (!File.Exists(_logFilePath)) return;

            try
            {
                FileInfo fi = new FileInfo(_logFilePath);

                // 設定門檻值：1MB (1024 * 1024 bytes)
                // 如果檔案小於 1MB，則不動作
                if (fi.Length < (1024 * 1024))
                {
                    return;
                }

                // 超過大小，執行封存
                ArchiveLogFile();
            }
            catch { /* 忽略錯誤 */ }
        }

        /// <summary>
        /// 將目前的日誌檔案重新命名為 .bak，以便為新的操作記錄騰出空間。
        /// </summary>
        private void ArchiveLogFile()
        {
            // 如果目前的日誌檔不存在，就沒有什麼可封存的
            if (!File.Exists(_logFilePath))
            {
                return;
            }

            try
            {
                string backupLogPath = _logFilePath + ".bak";

                // 如果已存在舊的備份檔，先將其刪除
                if (File.Exists(backupLogPath))
                {
                    File.Delete(backupLogPath);
                }

                // 將目前的日誌檔重新命名為備份檔
                File.Move(_logFilePath, backupLogPath);
            }
            catch (Exception ex)
            {
                // 如果封存失敗，在 UI 上提示，但不影響主要操作
                LogError(string.Format(Resources.Strings.LogErrorArchivingLog, ex.Message));
            }
        }

        /// <summary>
        /// 處理未預期的例外狀況。
        /// </summary>
        private void HandleException(Exception ex)
        {
            LogError(string.Format(Resources.Strings.LogErrorUnexpected, ex.Message));
            LogError(Resources.Strings.LogErrorAdminRights);
            MessageBox.Show(string.Format(Resources.Strings.HandleExceptionMessage, ex.Message), Resources.Strings.HandleExceptionTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        #endregion
    }
}