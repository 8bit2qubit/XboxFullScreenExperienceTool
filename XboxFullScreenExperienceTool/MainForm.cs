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
        // 常數與設定 (Constants & Configuration)
        //======================================================================

        #region Constants
        /// <summary>
        /// 功能旗標：是否要在螢幕尺寸大於 9.5" 的裝置上強制停用驅動程式 (Drv) 模式。
        /// 設定為 true: 啟用限制 (預設，更安全)。
        /// 設定為 false: 取消限制，允許大螢幕裝置也嘗試使用 Drv 模式 (用於測試或進階使用者)。
        /// </summary>
        private const bool RESTRICT_DRV_MODE_ON_LARGE_SCREEN = true;

        // --- 版本要求 ---
        /// <summary>
        /// 啟用此功能所需的最低 Windows 主要組建版本號。
        /// </summary>
        private const int REQUIRED_BUILD = 26200;
        /// <summary>
        /// 在最低主要組建版本下，所需的最低修訂 (UBR) 號。
        /// </summary>
        private const int REQUIRED_REVISION = 7015;
        /// <summary>
        /// 需要透過 ViVe 工具啟用的功能 ID 陣列。
        /// </summary>
        private readonly uint[] FEATURE_IDS = { 52580392, 50902630 };

        // --- 螢幕尺寸限制 ---
        /// <summary>
        /// 觸發自動設定的螢幕對角線尺寸門檻 (英吋)。
        /// 根據測試，尺寸大於此值 (例如 > 9.5") 的裝置需要建立工作排程來強制覆寫尺寸。
        /// </summary>
        private const double MAX_DIAGONAL_INCHES = 9.5;
        /// <summary>
        /// 英吋與毫米的轉換率。
        /// </summary>
        private const double INCHES_TO_MM = 25.4;

        // --- 登錄檔相關常數 ---
        private const string REG_PATH_PARENT = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        private const string REG_PATH = REG_PATH_PARENT + @"\OEM";
        private const string REG_VALUE = "DeviceForm";
        /// <summary>
        /// 用於備份原始登錄檔值的檔案路徑。
        /// </summary>
        private readonly string BackupFilePath = Path.Combine(Application.StartupPath, "DeviceForm.bak");

        /// <summary>
        // 日誌檔案路徑。
        private readonly string _logFilePath = Path.Combine(Application.StartupPath, "XboxFullScreenExperienceTool.log");
        /// </summary>
        #endregion

        #region Silent Action Handler
        /// <summary>
        /// 提供一組完全靜態的方法，用於在非互動式環境 (如靜默解除安裝) 中執行核心操作。
        /// 這個類別絕對不能參考 MainForm 的任何實例成員或 UI 控制項。
        /// </summary>
        public static class SilentActionHandler
        {
            // 執行所需的核心常數
            private static readonly uint[] FEATURE_IDS = { 52580392, 50902630 };
            private const string REG_PATH_PARENT = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            private const string REG_PATH = REG_PATH_PARENT + @"\OEM";
            private const string REG_VALUE = "DeviceForm";
            private static string BackupFilePath => Path.Combine(Helpers.AppPathManager.InstallPath, "DeviceForm.bak");

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

                    if (TaskSchedulerManager.TaskExists())
                    {
                        logger("Deleting SetPanelDimensions task...");
                        TaskSchedulerManager.DeleteSetPanelDimensionsTask();
                    }
                    if (TaskSchedulerManager.StartKeyboardTaskExists())
                    {
                        logger("Deleting StartTouchKeyboardOnLogon task...");
                        TaskSchedulerManager.DeleteStartKeyboardTask();
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

            private static bool DisableViveFeatures(Action<string> logger)
            {
                try
                {
                    logger($"Disabling features: {string.Join(", ", FEATURE_IDS)}");
                    var updates = Array.ConvertAll(FEATURE_IDS, id => new RTL_FEATURE_CONFIGURATION_UPDATE
                    {
                        FeatureId = id,
                        EnabledState = RTL_FEATURE_ENABLED_STATE.Disabled,
                        Operation = RTL_FEATURE_CONFIGURATION_OPERATION.FeatureState,
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
        }
        #endregion

        //======================================================================
        // 狀態旗標 (State Flags)
        //======================================================================

        /// <summary>
        /// 追蹤是否已套用變更但使用者尚未重新啟動電腦。
        /// 在此狀態下，UI 應被鎖定以防止進一步的操作。
        /// </summary>
        private bool _restartPending = false;
        private bool _isInitializing = true;

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
            UpdateUIForLanguage(); // 根據選定的語言更新 UI 文字
            RerunChecksAndLog();   // 執行所有初始檢查並記錄結果
            _isInitializing = false; // 初始化完成
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
                if (currentBuild < REQUIRED_BUILD || (currentBuild == REQUIRED_BUILD && currentRevision < REQUIRED_REVISION))
                {
                    string requirementString = $"{REQUIRED_BUILD}.{REQUIRED_REVISION}";
                    Log(string.Format(Resources.Strings.ErrorBuildTooLow, versionString));
                    Log(string.Format(Resources.Strings.RequiredBuild, requirementString));
                    MessageBox.Show(string.Format(Resources.Strings.RequiredBuild, requirementString), Resources.Strings.VersionIncompatible, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        /// 檢查所有相關設定 (ViVe 功能、登錄檔、螢幕尺寸、排程工作/驅動程式) 以確定功能的目前啟用狀態，並相應地更新 UI。
        /// 綜合性的檢查，處理多種中間狀態（例如，需要修正）。
        /// </summary>
        private void CheckCurrentStatus()
        {
            _isInitializing = true;
            try
            {
                // 步驟 1: 基礎核心檢查 (ViVe 功能 & 登錄檔)
                bool allFeaturesEnabled = FEATURE_IDS.All(id =>
                    FeatureManager.QueryFeatureConfiguration(id, RTL_FEATURE_CONFIGURATION_TYPE.Runtime) is RTL_FEATURE_CONFIGURATION config &&
                    config.EnabledState == RTL_FEATURE_ENABLED_STATE.Enabled);

                // 檢查登錄檔：確認 DeviceForm 值是否已設定為 0x2E (46)
                object? regValue = Registry.GetValue($"HKEY_LOCAL_MACHINE\\{REG_PATH}", REG_VALUE, null);
                bool isRegistrySet = regValue is int intValue && intValue == 0x2E;

                // 描述登錄檔狀態的邏輯
                string registryStatusString;
                if (regValue == null)
                {
                    registryStatusString = Resources.Strings.LogRegStatusFalseNotExist;
                }
                else if (isRegistrySet)
                {
                    registryStatusString = Resources.Strings.LogRegStatusTrue;
                }
                else
                {
                    registryStatusString = string.Format(Resources.Strings.LogRegStatusFalseWrongValue, regValue);
                }

                bool isCoreEnabled = allFeaturesEnabled && isRegistrySet;

                // 步驟 2: 硬體相依性檢查 (螢幕尺寸需求)
                // 判斷是否仍需要覆寫螢幕尺寸 (0x0 或 > 9.5")
                // 在以下兩種情況下需要：
                //   a) 系統無法偵測到實體尺寸 (回傳 0x0mm)。
                //   b) 偵測到的尺寸過大 (例如，桌機、筆電)，超出了此功能預期的掌機尺寸範圍。
                bool isScreenOverrideRequired = false;
                bool isScreenTooLarge = false; // 用於判斷螢幕是否過大
                var (success, size) = PanelManager.GetDisplaySize();
                if (success)
                {
                    // 情況 a: 尺寸未定義
                    bool isUndefined = size.WidthMm == 0 && size.HeightMm == 0;
                    if (isUndefined) // 這是 0" 的情況
                    {
                        isScreenOverrideRequired = true;
                    }
                    // 情況 b: 尺寸有定義，現在來判斷是否過大
                    else
                    {
                        // 使用畢氏定理 (a^2 + b^2 = c^2) 計算螢幕對角線的長度 (單位：毫米 mm)
                        double diagonalMm = Math.Sqrt((size.WidthMm * size.WidthMm) + (size.HeightMm * size.HeightMm));

                        // 將毫米 (mm) 轉換為英寸 (inches)，並檢查是否超過最大限制 (9.5")
                        if ((diagonalMm / INCHES_TO_MM) > MAX_DIAGONAL_INCHES)
                        {
                            isScreenOverrideRequired = true;
                            isScreenTooLarge = true; // 在這裡才將 isScreenTooLarge 設為 true
                        }
                    }
                }
                else
                {
                    LogError(Resources.Strings.LogErrorReadingScreenSize);
                }

                // 步驟 3: 檢查覆寫方法是否存在 (CS Task & Drv Service)
                bool isPhysPanelCSActive = TaskSchedulerManager.TaskExists();
                bool isPhysPanelDrvActive = DriverManager.IsDriverServiceInstalled();
                bool isScreenOverridePresent = isPhysPanelCSActive || isPhysPanelDrvActive;

                // 步驟 3.5: 設定鍵盤啟動選項的可用性
                bool hasTouchSupport = HardwareHelper.IsTouchScreenAvailable();
                chkStartKeyboardOnLogon.Enabled = !hasTouchSupport; // 如果沒有觸控，則啟用此選項
                chkStartKeyboardOnLogon.Checked = TaskSchedulerManager.StartKeyboardTaskExists();

                // 設定 ToolTip 提示，向使用者解釋為何選項被停用
                if (hasTouchSupport)
                {
                    toolTip.SetToolTip(chkStartKeyboardOnLogon, Resources.Strings.TooltipTouchEnabled);
                }
                else
                {
                    toolTip.SetToolTip(chkStartKeyboardOnLogon, Resources.Strings.TooltipTouchDisabled);
                }
                Log(string.Format(Resources.Strings.LogTouchSupportStatus, hasTouchSupport));

                // 步驟 4: 檢查並設定覆寫模式的可用性 (檢查驅動程式模式的先決條件 (Test Signing))

                // 條件 A: 檢查測試簽章模式是否啟用
                bool isTestSigningOn = DriverManager.IsTestSigningEnabled();

                // 條件 B: 根據功能旗標和螢幕尺寸，判斷是否存在螢幕尺寸限制
                // 只有在「旗標為 true」且「螢幕需要覆寫 (即 > 9.5")」時，限制才生效
                bool isScreenSizeRestricted = RESTRICT_DRV_MODE_ON_LARGE_SCREEN && isScreenTooLarge;

                // 綜合判斷：Drv 模式只有在「測試簽章已啟用」且「沒有螢幕尺寸限制」時才可用
                bool isDrvModeAvailable = isTestSigningOn && !isScreenSizeRestricted;

                radPhysPanelDrv.Enabled = isDrvModeAvailable;

                // 根據條件記錄日誌
                if (!isTestSigningOn)
                {
                    Log(Resources.Strings.LogTestSigningDisabled);
                }
                // 只有在限制實際生效時才記錄日誌
                if (isScreenSizeRestricted)
                {
                    Log(Resources.Strings.LogLargeScreenForceCS);
                }

                // 安全機制：如果 Drv 模式因任何限制而變為不可用，則強制切換回 CS 模式
                if (!isDrvModeAvailable)
                {
                    radPhysPanelCS.Checked = true;
                }

                // 步驟 5: 綜合判斷與 UI 更新
                string statusText;
                Color statusColor;

                // 核心邏輯判斷：
                // 1. isCoreEnabled: ViVe 功能 + 登錄檔是否設定。
                // 2. isScreenOverrideRequired: 呼叫 GetDisplaySize() 後，螢幕尺寸是否仍不符需求。(如果覆寫成功，此值應為 False)
                if (isCoreEnabled)
                {
                    if (isScreenOverrideRequired)
                    {
                        // 狀態 2: 需要修正
                        // 核心已啟用，但螢幕尺寸仍不符
                        // 根據使用者回報：當驅動程式安裝後未正常工作時，應用程式會進入此「需要修正」狀態
                        // 此時若讓使用者點選「修正」，會導致重複安裝驅動，且無法解決問題
                        // 正確的處理方式是引導使用者先「停用」功能，將系統還原到乾淨狀態再重試
                        if (isPhysPanelDrvActive)
                        {
                            // 偵測到是驅動程式模式 (Drv) 處於無效狀態
                            statusText = Resources.Strings.StatusDriverErrorNeedsDisable;
                            statusColor = Color.Red; // 使用更醒目的紅色來表示嚴重錯誤

                            btnEnable.Enabled = false;      // 停用「修正」按鈕
                            btnDisable.Enabled = true;      // 啟用「停用」按鈕，這是唯一允許的操作
                            grpPhysPanel.Enabled = false;   // 鎖定覆寫選項，防止使用者切換模式
                            radPhysPanelDrv.Checked = true; // 保持顯示當前是驅動模式
                        }
                        else
                        {
                            // 非驅動程式模式下的「需要修正」狀態 (例如排程工作損壞或從未安裝覆寫)
                            statusText = Resources.Strings.StatusNeedsFix;
                            statusColor = Color.Orange;
                            btnEnable.Text = Resources.Strings.btnEnable_Text_Fix;
                            btnEnable.Enabled = true;

                            // 停用按鈕必須是 false，否則使用者會有兩個可選按鈕
                            btnDisable.Enabled = false;

                            grpPhysPanel.Enabled = true; // 允許選擇修正方式

                            // 顯示目前安裝但"無效"的選項
                            radPhysPanelDrv.Checked = isPhysPanelDrvActive;
                            radPhysPanelCS.Checked = isPhysPanelCSActive;
                            if (!isScreenOverridePresent) radPhysPanelCS.Checked = true; // 預設 PhysPanelCS
                        }
                    }
                    else
                    {
                        // 狀態 1: 已啟用
                        // 核心已啟用，且螢幕尺寸已符合需求。(無論是天生符合，或是覆寫已成功生效)
                        statusText = isPhysPanelDrvActive ? Resources.Strings.StatusEnabledDriverMode : (isPhysPanelCSActive ? Resources.Strings.StatusEnabledSchedulerMode : Resources.Strings.StatusEnabled);
                        statusColor = Color.LimeGreen;

                        radPhysPanelDrv.Checked = isPhysPanelDrvActive;
                        radPhysPanelCS.Checked = isPhysPanelCSActive;
                        if (!isScreenOverridePresent) radPhysPanelCS.Checked = true; // 螢幕尺寸正常，預設CS

                        grpPhysPanel.Enabled = false; // 已啟用時鎖定選項
                        btnEnable.Enabled = false;
                        btnDisable.Enabled = true;
                    }
                }
                else // !isCoreEnabled
                {
                    // 狀態 3: 未啟用
                    // 核心功能 (ViVe 功能 & 登錄檔) 未設定。
                    statusText = Resources.Strings.StatusDisabled;
                    statusColor = Color.Tomato;
                    btnEnable.Text = Resources.Strings.btnEnable_Text;
                    btnEnable.Enabled = true;
                    btnDisable.Enabled = false;
                    grpPhysPanel.Enabled = isScreenOverrideRequired; // 只有在螢幕尺寸確實需要覆寫時，才啟用此選項群組

                    // 檢查是否殘留設定
                    radPhysPanelDrv.Checked = isPhysPanelDrvActive;
                    radPhysPanelCS.Checked = isPhysPanelCSActive;
                    if (!isScreenOverridePresent) radPhysPanelCS.Checked = true; // 預設 PhysPanelCS
                }

                lblStatus.Text = statusText;
                lblStatus.ForeColor = statusColor;

                Log(string.Format(Resources.Strings.LogStatusCheckSummary, isCoreEnabled, allFeaturesEnabled, registryStatusString, isScreenOverrideRequired, isScreenOverridePresent, isPhysPanelCSActive, isPhysPanelDrvActive, isTestSigningOn));
            }
            catch (Exception ex)
            {
                lblStatus.Text = Resources.Strings.StatusUnknownError;
                lblStatus.ForeColor = Color.OrangeRed;
                LogError(string.Format(Resources.Strings.ErrorCheckStatus, ex.Message));
            }
            finally
            {
                _isInitializing = false;
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

            try
            {
                Log(Resources.Strings.LogBeginEnable);

                // --- 通用步驟：設定核心功能 ---
                // 1. 備份並設定登錄檔
                Log(Resources.Strings.LogBackupAndSetRegistry);
                BackupAndSetRegistry();

                // 2. 啟用 ViVe 功能
                Log(string.Format(Resources.Strings.LogEnablingFeatures, string.Join(", ", FEATURE_IDS)));
                EnableViveFeatures();

                // --- 步驟 3: 條件步驟：設定螢幕尺寸覆寫 ---
                bool operationSuccess = true; // 預設為成功

                // --- 條件步驟：根據選擇的模式設定螢幕尺寸覆寫 ---
                if (radPhysPanelDrv.Checked)
                {
                    Log(Resources.Strings.LogChoosingDriverMode);
                    // 執行前先移除 CS 工作，避免衝突
                    if (TaskSchedulerManager.TaskExists())
                    {
                        Log(Resources.Strings.LogRemovingOldTask);
                        TaskSchedulerManager.DeleteSetPanelDimensionsTask();
                    }

                    // 呼叫安裝方法並接收其回傳值
                    bool installSuccess = await Task.Run(() => DriverManager.InstallDriver(msg => Log(msg)));

                    // 檢查安裝是否成功。如果使用者點選了 "不安裝"，此處會收到 false
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
                    HandleTaskSchedulerCreation();
                }

                // --- 步驟 4: 流程總結與處理 ---
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
                    CheckCurrentStatus(); // 如果不需要重新開機，則重新整理狀態。無論成功、失敗還原，最後都重新整理一次狀態。
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
                    CheckCurrentStatus();
                }
            }
        }

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
            if (TaskSchedulerManager.StartKeyboardTaskExists())
            {
                Log(Resources.Strings.LogDeletingKeyboardTask);
                TaskSchedulerManager.DeleteStartKeyboardTask();
                Log(Resources.Strings.LogKeyboardTaskDeleted, true);
            }

            // 步驟 4: 移除所有可能的覆寫方法 (包括 CS 和 Drv)
            if (TaskSchedulerManager.TaskExists())
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

        #region Helper Methods for Enable/Disable
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
        private void EnableViveFeatures()
        {
            var updates = Array.ConvertAll(FEATURE_IDS, id => new RTL_FEATURE_CONFIGURATION_UPDATE
            {
                FeatureId = id,
                EnabledState = RTL_FEATURE_ENABLED_STATE.Enabled,
                Operation = RTL_FEATURE_CONFIGURATION_OPERATION.FeatureState,
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
            Log(string.Format(Resources.Strings.LogDisablingFeatures, string.Join(", ", FEATURE_IDS)));
            var updates = Array.ConvertAll(FEATURE_IDS, id => new RTL_FEATURE_CONFIGURATION_UPDATE
            {
                FeatureId = id,
                EnabledState = RTL_FEATURE_ENABLED_STATE.Disabled,
                Operation = RTL_FEATURE_CONFIGURATION_OPERATION.FeatureState,
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
                Process.Start("shutdown.exe", shutdownArgs);
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

            string currentCultureName = Thread.CurrentThread.CurrentUICulture.Name;
            if (currentCultureName.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) || currentCultureName.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase)) cboLanguage.SelectedIndex = 1; // 繁體中文
            else if (currentCultureName.StartsWith("zh-CN", StringComparison.OrdinalIgnoreCase) || currentCultureName.StartsWith("zh-Hans", StringComparison.OrdinalIgnoreCase)) cboLanguage.SelectedIndex = 2; // 简体中文
            else if (currentCultureName.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) cboLanguage.SelectedIndex = 3; // 日本語
            else if (currentCultureName.StartsWith("ko", StringComparison.OrdinalIgnoreCase)) cboLanguage.SelectedIndex = 4; // 한국어
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
            // 將標題設定為 "程式名稱 v主版號.次版號.組建編號"
            // this.Text 的初始值是在設計工具中設定的 "Xbox 全螢幕體驗工具"
            this.Text = $"{Resources.Strings.MainFormTitle} v{versionString}";

            grpPhysPanel.Text = Resources.Strings.grpPhysPanel_Text;
            radPhysPanelCS.Text = Resources.Strings.radPhysPanelCS_Text;
            radPhysPanelDrv.Text = Resources.Strings.radPhysPanelDrv_Text;
            grpActions.Text = Resources.Strings.grpActions_Text;
            grpOutput.Text = Resources.Strings.grpOutput_Text;
            btnDisable.Text = Resources.Strings.btnDisable_Text;
            btnEnable.Text = Resources.Strings.btnEnable_Text;
            chkStartKeyboardOnLogon.Text = Resources.Strings.chkStartKeyboardOnLogon_Text;
        }

        /// <summary>
        /// 重新執行檢查並清除 UI 上的舊訊息。日誌會繼續附加到現有檔案。
        /// </summary>
        private void RerunChecksAndLog()
        {
            txtOutput.Clear();

            // 步驟 1: 進行關鍵的前置檢查，確保 OS 版本符合要求。
            if (!CheckWindowsBuild())
            {
                // 如果版本不符，CheckWindowsBuild 內部已顯示錯誤訊息，此處直接關閉應用程式。
                Application.Exit();
                return;
            }
            Log(Resources.Strings.WelcomeMessage);
            // 步驟 2: 檢查目前系統狀態並更新 UI。
            CheckCurrentStatus();
        }

        /// <summary>
        /// 處理語言下拉選單的選擇變更事件。
        /// </summary>
        private void cboLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            string culture = cboLanguage.SelectedIndex switch
            {
                1 => "zh-TW",
                2 => "zh-CN",
                3 => "ja-JP",
                4 => "ko-KR",
                _ => "en-US",
            };

            if (Thread.CurrentThread.CurrentUICulture.Name != culture)
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);
                UpdateUIForLanguage();
                RerunChecksAndLog();
            }
        }

        /// <summary>
        /// 當「在登入時啟動觸控鍵盤」核取方塊的狀態變更時觸發。
        /// </summary>
        private void chkStartKeyboardOnLogon_CheckedChanged(object sender, EventArgs e)
        {
            // 如果是在程式初始化期間設定狀態，則不執行任何動作
            if (_isInitializing) return;

            // 暫時停用控制項並顯示等待游標
            chkStartKeyboardOnLogon.Enabled = false;
            this.Cursor = Cursors.WaitCursor;

            try
            {
                if (chkStartKeyboardOnLogon.Checked)
                {
                    Log(Resources.Strings.LogCreatingKeyboardTask);
                    TaskSchedulerManager.CreateStartKeyboardTask();
                    Log(Resources.Strings.LogKeyboardTaskCreated, true);
                }
                else
                {
                    Log(Resources.Strings.LogDeletingKeyboardTask);
                    TaskSchedulerManager.DeleteStartKeyboardTask();
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

        //======================================================================
        // 靜默模式 (Silent Mode)
        //======================================================================

        /// <summary>
        /// 供解除安裝程式在背景呼叫的靜默停用方法。
        /// 它執行與 UI 停用相同的核心邏輯，但不提供任何輸出或錯誤處理。
        /// </summary>
        public static void PerformSilentDisable()
        {
            try
            {
                var mainFormInstance = new MainForm(); // 建立一個實例以存取非靜態成員和常數
                mainFormInstance.PerformDisableActions().GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // 在靜默模式下，不處理任何錯誤
            }
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

            string timestamp = $"[{DateTime.Now:HH:mm:ss}] ";
            string fullMessage = timestamp + message;

            // 更新 UI
            txtOutput.SelectionColor = isSuccess ? Color.LimeGreen : Color.Gainsboro;
            txtOutput.AppendText(fullMessage + Environment.NewLine);
            txtOutput.ScrollToCaret();

            // 將日誌寫入檔案
            WriteToFile(fullMessage);
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

            string timestamp = $"[{DateTime.Now:HH:mm:ss}] [{Resources.Strings.HandleExceptionTitle}] ";
            string fullMessage = timestamp + message;

            // 更新 UI
            txtOutput.SelectionColor = Color.Tomato;
            txtOutput.AppendText(fullMessage + Environment.NewLine);
            txtOutput.ScrollToCaret();

            // 將日誌寫入檔案
            WriteToFile(fullMessage);
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
                LogError($"無法封存舊的日誌檔案：{ex.Message}");
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