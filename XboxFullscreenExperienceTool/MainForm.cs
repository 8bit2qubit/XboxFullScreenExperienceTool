// Xbox Fullscreen Experience Tool
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
using XboxFullscreenExperienceTool.Helpers;

namespace XboxFullscreenExperienceTool
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
        // --- 版本要求 ---
        /// <summary>
        /// 啟用此功能所需的最低 Windows 主要組建版本號。
        /// </summary>
        private const int REQUIRED_BUILD = 26220;
        /// <summary>
        /// 在最低主要組建版本下，所需的最低修訂 (UBR) 號。
        /// </summary>
        private const int REQUIRED_REVISION = 6690;
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
        #endregion

        //======================================================================
        // 狀態旗標 (State Flags)
        //======================================================================

        /// <summary>
        /// 追蹤是否已套用變更但使用者尚未重新啟動電腦。
        /// 在此狀態下，UI 應被鎖定以防止進一步的操作。
        /// </summary>
        private bool _restartPending = false;

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
        /// 檢查所有相關設定 (ViVe 功能、登錄檔、螢幕尺寸、排程工作) 以確定功能的目前啟用狀態，並相應地更新 UI。
        /// 綜合性的檢查，處理多種中間狀態（例如，需要修正）。
        /// </summary>
        private void CheckCurrentStatus()
        {
            try
            {
                // 步驟 1: 基礎核心檢查 (Core Prerequisite Checks)
                // 1a. 檢查 ViVe 功能：確保所有必要的功能 ID 都已啟用。
                // 使用 LINQ 的 .All() 方法來驗證陣列中的每一個 ID。
                bool allFeaturesEnabled = FEATURE_IDS.All(id =>
                    FeatureManager.QueryFeatureConfiguration(id, RTL_FEATURE_CONFIGURATION_TYPE.Runtime) is RTL_FEATURE_CONFIGURATION config &&
                    config.EnabledState == RTL_FEATURE_ENABLED_STATE.Enabled);

                // 1b. 檢查登錄檔：確認 DeviceForm 值是否已設定為 0x2E (46)。
                object? regValue = Registry.GetValue($"HKEY_LOCAL_MACHINE\\{REG_PATH}", REG_VALUE, null);
                bool isRegistrySet = regValue is int intValue && intValue == 0x2E;

                // 為了提供更詳細的日誌，建立一個描述登錄檔狀態的字串。
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

                // 步驟 2: 硬體相依性檢查 (Hardware-Dependent Checks)
                // 2a. 判斷是否需要排程工作來覆寫螢幕尺寸。
                // 在以下兩種情況下需要：
                //   a) 系統無法偵測到實體尺寸 (回傳 0x0mm)。
                //   b) 偵測到的尺寸過大 (例如，平板、筆電)，超出了此功能預期的掌機尺寸範圍。
                bool isTaskRequired = false;
                var (success, size) = PanelManager.GetDisplaySize();
                if (success)
                {
                    // 情況 a: 尺寸未定義
                    bool isUndefined = size.WidthMm == 0 && size.HeightMm == 0;
                    if (isUndefined)
                    {
                        isTaskRequired = true;
                    }
                    // 情況 b: 尺寸過大
                    else
                    {
                        // 假設 INCHES_TO_MM 和 MAX_DIAGONAL_INCHES 是已定義的常數
                        double diagonalMm = Math.Sqrt((size.WidthMm * size.WidthMm) + (size.HeightMm * size.HeightMm));
                        isTaskRequired = (diagonalMm / INCHES_TO_MM) > MAX_DIAGONAL_INCHES;
                    }
                }
                else
                {
                    LogError(Resources.Strings.LogErrorReadingScreenSize);
                }

                // 2b. 檢查排程工作是否實際存在。
                bool isTaskPresent = TaskSchedulerManager.TaskExists();

                // 步驟 3: 綜合判斷與 UI 更新 (Final Logic & UI Update)
                // isCoreEnabled: 核心的功能開關是否都已打開 (ViVe 功能 + 登錄檔)。
                bool isCoreEnabled = allFeaturesEnabled && isRegistrySet;

                // isFullyConfigured: 是否達到了完美的啟用狀態。
                // 必須滿足核心啟用，且 (如果需要排程工作，則該工作必須存在)。
                bool isFullyConfigured = isCoreEnabled && (!isTaskRequired || isTaskPresent);

                Log(string.Format(Resources.Strings.LogStatusCheck, allFeaturesEnabled, registryStatusString, isTaskRequired, isTaskPresent));

                // 根據最終狀態更新 UI
                if (isFullyConfigured && isCoreEnabled)
                {
                    // 狀態一：完全啟用。所有設定都正確。
                    lblStatus.Text = Resources.Strings.StatusEnabled;
                    lblStatus.ForeColor = Color.LimeGreen;
                    btnEnable.Text = Resources.Strings.btnEnable_Text; // 總是設定文字
                    btnEnable.Enabled = false;
                    btnDisable.Enabled = true;
                }
                else if (isCoreEnabled && isTaskRequired && !isTaskPresent)
                {
                    // 狀態二：需要修正。核心功能已啟用，但缺少必要的硬體修正 (排程工作)。
                    lblStatus.Text = Resources.Strings.StatusNeedsFix;
                    lblStatus.ForeColor = Color.Orange;
                    btnEnable.Text = Resources.Strings.btnEnable_Text_Fix; // 改變按鈕文字以提示使用者
                    btnEnable.Enabled = true; // 允許使用者點選「修正」
                    btnDisable.Enabled = false; // 不允許使用者點選「停用」
                }
                else
                {
                    // 狀態三：未啟用。任何核心設定未完成。
                    lblStatus.Text = Resources.Strings.StatusDisabled;
                    lblStatus.ForeColor = Color.Tomato;
                    btnEnable.Text = Resources.Strings.btnEnable_Text; // 重設按鈕文字
                    btnEnable.Enabled = true;
                    btnDisable.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = Resources.Strings.StatusUnknownError;
                lblStatus.ForeColor = Color.OrangeRed;
                LogError(string.Format(Resources.Strings.ErrorCheckStatus, ex.Message));
            }
        }

        //======================================================================
        // 核心邏輯 - 啟用/停用 (Core Logic - Enable/Disable)
        //======================================================================

        /// <summary>
        /// 處理「啟用」按鈕的點選事件，執行所有啟用功能的必要步驟。
        /// </summary>
        private void btnEnable_Click(object sender, EventArgs e)
        {
            this.Cursor = Cursors.WaitCursor;
            try
            {
                Log(Resources.Strings.LogBeginEnable);

                // 步驟 1: 處理螢幕實體尺寸。如果尺寸未定義 (0x0) 或大於 9.5 吋，則建立開機工作來設定它。
                Log(Resources.Strings.LogReadingScreenSize);
                var (success, size) = PanelManager.GetDisplaySize();
                if (success)
                {
                    Log(string.Format(Resources.Strings.LogScreenSizeSuccess, size.WidthMm, size.HeightMm));

                    bool isUndefined = size.WidthMm == 0 && size.HeightMm == 0;
                    bool isTooLarge = false;
                    double diagonalInches = 0;

                    if (!isUndefined)
                    {
                        // 使用畢氏定理計算對角線長度 (mm)
                        double diagonalMm = Math.Sqrt((size.WidthMm * size.WidthMm) + (size.HeightMm * size.HeightMm));
                        // 將毫米轉換為英吋
                        diagonalInches = diagonalMm / INCHES_TO_MM;
                        // 判斷是否大於 9.5 吋門檻
                        isTooLarge = diagonalInches > MAX_DIAGONAL_INCHES;
                    }

                    // 如果尺寸未定義，或尺寸大於 9.5 吋，則建立工作排程
                    if (isUndefined || isTooLarge)
                    {
                        if (isUndefined)
                        {
                            Log(Resources.Strings.LogScreenSizeUndefined);
                        }
                        else
                        {
                            Log(string.Format(Resources.Strings.LogScreenSizeTooLarge, diagonalInches, MAX_DIAGONAL_INCHES));
                        }
                        TaskSchedulerManager.CreateSetPanelDimensionsTask();
                        Log(Resources.Strings.LogTaskCreated);
                    }
                    else
                    {
                        // 說明尺寸在範圍內，無需操作
                        Log(string.Format(Resources.Strings.LogTaskNotNeeded, diagonalInches, MAX_DIAGONAL_INCHES));
                    }
                }
                else
                {
                    LogError(Resources.Strings.LogErrorReadingScreenSizeEnable);
                }

                // 步驟 2: 備份現有的登錄檔值，然後設定新的值。
                Log(Resources.Strings.LogBackupAndSetRegistry);
                // 只在備份檔案不存在時才執行備份，確保只備份一次最原始的狀態
                if (!File.Exists(BackupFilePath))
                {
                    object? currentValue = Registry.GetValue($"HKEY_LOCAL_MACHINE\\{REG_PATH}", REG_VALUE, null);
                    if (currentValue != null)
                    {
                        // 如果值存在，將其內容寫入備份檔
                        File.WriteAllText(BackupFilePath, currentValue.ToString() ?? "");
                        Log(string.Format(Resources.Strings.LogRegistryBackupSuccess, currentValue, BackupFilePath));
                    }
                    else
                    {
                        // 如果值不存在，建立一個特殊的標記檔案
                        // 這將告訴還原程序，原始狀態是「不存在」，因此需要刪除此鍵值。
                        File.WriteAllText(BackupFilePath, "DELETE_ON_RESTORE");
                        Log(Resources.Strings.LogRegistryBackupMarkedForDelete);
                    }
                }
                Registry.SetValue($"HKEY_LOCAL_MACHINE\\{REG_PATH}", REG_VALUE, 0x2E, RegistryValueKind.DWord); // 0x2E = 46
                Log(Resources.Strings.LogRegistrySetSuccess);

                // 步驟 3: 使用 ViVe Manager 啟用所需的功能 ID。
                Log(string.Format(Resources.Strings.LogEnablingFeatures, string.Join(", ", FEATURE_IDS)));
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

                // 流程結束
                btnEnable.Enabled = false;
                btnDisable.Enabled = false;
                Log(Resources.Strings.LogEnableComplete, true);
                PromptForRestart(Resources.Strings.PromptRestartCaptionSuccess);
            }
            catch (Exception ex)
            {
                HandleException(ex); // 統一的例外處理
            }
            finally
            {
                this.Cursor = Cursors.Default; // 確保游標總是還原
                if (!_restartPending)
                {
                    CheckCurrentStatus(); // 如果不需要重啟，則重新整理狀態
                }
            }
        }

        /// <summary>
        /// 處理「停用」按鈕的點選事件。
        /// </summary>
        private void btnDisable_Click(object sender, EventArgs e)
        {
            this.Cursor = Cursors.WaitCursor;
            try
            {
                PerformDisableActions(); // 呼叫共用的停用邏輯

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
        /// 執行停用功能的所有核心步驟，此方法被 UI 和靜默模式共用。
        /// </summary>
        private void PerformDisableActions()
        {
            Log(Resources.Strings.LogBeginDisable);

            // 步驟 1: 明確地將 ViVe 功能設定為停用狀態。
            // 這一步操作是安全的，因為使用者意圖就是停用該功能體驗。
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

            // 步驟 2: 極度安全地還原登錄檔值。
            Log(Resources.Strings.LogCheckingRegistryRestore);
            try
            {
                if (File.Exists(BackupFilePath))
                {
                    Log(Resources.Strings.LogBackupFound);
                    string backupContent = File.ReadAllText(BackupFilePath);

                    using RegistryKey? key = Registry.LocalMachine.OpenSubKey(REG_PATH, true);
                    if (key != null)
                    {
                        if (backupContent == "DELETE_ON_RESTORE")
                        {
                            // 如果備份內容是我們的特殊標記，則刪除該值
                            key.DeleteValue(REG_VALUE, false); // false 表示如果值不存在也不會拋出例外
                            Log(string.Format(Resources.Strings.LogRegistryValueRemoved, REG_VALUE));
                        }
                        else if (int.TryParse(backupContent, out int backupValue))
                        {
                            // 如果是數字，則還原該數值
                            key.SetValue(REG_VALUE, backupValue, RegistryValueKind.DWord);
                            Log(string.Format(Resources.Strings.LogRegistryValueRestored, backupValue));
                        }
                        else
                        {
                            // 備份檔內容未知，為安全起見，同樣刪除該值
                            key.DeleteValue(REG_VALUE, false);
                            Log(string.Format(Resources.Strings.LogRegistryBackupInvalid, REG_VALUE));
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

            // 步驟 3: 如果存在，則刪除相關的工作排程。
            if (TaskSchedulerManager.TaskExists())
            {
                Log(Resources.Strings.LogDeletingTask);
                TaskSchedulerManager.DeleteSetPanelDimensionsTask();
                Log(Resources.Strings.LogTaskDeleted);
            }
        }

        //======================================================================
        // UI 流程控制 (UI Flow Control)
        //======================================================================

        /// <summary>
        /// 向使用者顯示需要重新啟動的提示。
        /// </summary>
        /// <param name="caption">訊息方塊的標題。</param>
        private void PromptForRestart(string caption)
        {
            var result = MessageBox.Show(
                Resources.Strings.PromptRestartMessage,
                caption, MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
            {
                Log(Resources.Strings.LogUserRestartNow);
                try
                {
                    Process.Start("shutdown.exe", "/r /t 5"); // 5 秒後重新啟動
                    Application.Exit();
                }
                catch (Exception ex)
                {
                    LogError(string.Format(Resources.Strings.ErrorRestartFailed, ex.Message));
                    MessageBox.Show(Resources.Strings.MessageBoxRestartFailed, Resources.Strings.HandleExceptionTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
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
            btnEnable.Enabled = false; // 停用「啟用」按鈕
            btnDisable.Enabled = false; // 停用「停用」按鈕
            cboLanguage.Enabled = false; // 停用「語言」選單
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

            string currentCultureName = Thread.CurrentThread.CurrentUICulture.Name;
            if (currentCultureName.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase) || currentCultureName.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase))
            {
                cboLanguage.SelectedIndex = 1;
            }
            else
            {
                cboLanguage.SelectedIndex = 0; // Default to English
            }
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
            // this.Text 的初始值是在設計工具中設定的 "Xbox 全螢幕體驗啟用工具"
            this.Text = $"{Resources.Strings.MainFormTitle} v{versionString}";

            grpActions.Text = Resources.Strings.grpActions_Text;
            grpOutput.Text = Resources.Strings.grpOutput_Text;
            btnDisable.Text = Resources.Strings.btnDisable_Text;
        }

        /// <summary>
        /// 重新執行所有檢查並更新日誌，通常在語言切換後呼叫。
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
            string culture = "";
            switch (cboLanguage.SelectedIndex)
            {
                case 0:
                    culture = "en-US";
                    break;
                case 1:
                    culture = "zh-TW";
                    break;
            }

            if (!string.IsNullOrEmpty(culture) && Thread.CurrentThread.CurrentUICulture.Name != culture)
            {
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);
                UpdateUIForLanguage();
                RerunChecksAndLog();
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
                mainFormInstance.PerformDisableActions();
            }
            catch (Exception)
            {
                // 在靜默模式下，不處理任何錯誤。
            }
        }

        #region Logging and Error Handling
        private void Log(string message, bool isSuccess = false)
        {
            string timestamp = $"[{DateTime.Now:HH:mm:ss}] ";
            txtOutput.SelectionColor = isSuccess ? Color.LimeGreen : Color.Gainsboro;
            txtOutput.AppendText(timestamp + message + Environment.NewLine);
            txtOutput.ScrollToCaret();
        }

        private void LogError(string message)
        {
            string timestamp = $"[{DateTime.Now:HH:mm:ss}] [{Resources.Strings.HandleExceptionTitle}] ";
            txtOutput.SelectionColor = Color.Tomato;
            txtOutput.AppendText(timestamp + message + Environment.NewLine);
            txtOutput.ScrollToCaret();
        }

        private void HandleException(Exception ex)
        {
            LogError(string.Format(Resources.Strings.LogErrorUnexpected, ex.Message));
            LogError(Resources.Strings.LogErrorAdminRights);
            MessageBox.Show(string.Format(Resources.Strings.HandleExceptionMessage, ex.Message), Resources.Strings.HandleExceptionTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        #endregion
    }
}