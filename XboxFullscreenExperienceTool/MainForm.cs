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
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using XboxFullscreenExperienceTool.Helpers;

namespace XboxFullscreenExperienceTool
{
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
        /// 根據測試，尺寸大於此值 (例如 > 9.5") 的裝置需要建立工作排程。
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
        private readonly string BackupFilePath = Path.Combine(Application.StartupPath, "deviceform.bak");
        #endregion

        //======================================================================
        // 狀態旗標 (State Flags)
        //======================================================================

        /// <summary>
        /// 追蹤是否已套用變更但使用者尚未重新啟動電腦。
        /// 在此狀態下，UI 應被鎖定。
        /// </summary>
        private bool _restartPending = false;

        //======================================================================
        // 表單事件 (Form Events)
        //======================================================================

        public MainForm()
        {
            InitializeComponent();
        }


        /// <summary>
        /// 表單載入時的初始化邏輯。
        /// </summary>
        private void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                // 取得目前版本資訊
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                // 將標題設定為 "程式名稱 v主版號.次版號.組建編號"
                // this.Text 的初始值是在設計工具中設定的 "Xbox 全螢幕體驗啟用工具"
                this.Text = $"{this.Text} v{version.Major}.{version.Minor}.{version.Build}";
            }
            catch (Exception ex)
            {
                // 如果發生錯誤，至少記錄下來，但不要中斷程式執行
                Log($"讀取版本號並設定標題時發生錯誤: {ex.Message}");
            }

            // 步驟 1: 進行關鍵的前置檢查，確保 OS 版本符合要求。
            if (!CheckWindowsBuild())
            {
                // 如果版本不符，CheckWindowsBuild 內部已顯示錯誤訊息，此處直接關閉應用程式。
                Application.Exit();
                return;
            }

            Log($"歡迎使用 Xbox 全螢幕體驗啟用工具！");

            // 步驟 2: 檢查目前系統狀態並更新 UI。
            CheckCurrentStatus();
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
                    string errorMsg = "錯誤: 無法從登錄檔讀取或解析目前 OS 的組建資訊 (CurrentBuild 或 UBR)。";
                    Log(errorMsg);
                    MessageBox.Show(errorMsg, "版本不相容", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                string versionString = $"{currentBuild}.{currentRevision}";
                Log($"您的 Windows 組建版本: {versionString}");

                // 版本比較邏輯
                if (currentBuild < REQUIRED_BUILD || (currentBuild == REQUIRED_BUILD && currentRevision < REQUIRED_REVISION))
                {
                    string requirementString = $"{REQUIRED_BUILD}.{REQUIRED_REVISION}";
                    Log($"錯誤：您的 Windows 組建版本 ({versionString}) 過低。");
                    Log($"此工具需要組建版本 {requirementString} 或更高版本。");
                    MessageBox.Show($"您的 Windows 組建版本 ({versionString}) 不符，此工具需要 {requirementString} 或更高版本。", "版本不相容", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                Log($"版本符合要求。");
                return true;
            }
            catch (Exception ex)
            {
                string errorMsg = $"檢查 Windows 組建時發生未預期錯誤: {ex.Message}";
                Log(errorMsg);
                MessageBox.Show(errorMsg, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        /// <summary>
        /// 檢查所有相關設定 (ViVe 功能、登錄檔) 以確定功能的目前啟用狀態，並更新 UI。
        /// </summary>
        private void CheckCurrentStatus()
        {
            try
            {
                // 步驟 1: 檢查所有 ViVe 功能是否都已啟用
                bool allFeaturesEnabled = true;
                foreach (uint featureId in FEATURE_IDS)
                {
                    var config = FeatureManager.QueryFeatureConfiguration(featureId, RTL_FEATURE_CONFIGURATION_TYPE.Runtime);
                    if (!config.HasValue || config.Value.EnabledState != RTL_FEATURE_ENABLED_STATE.Enabled)
                    {
                        allFeaturesEnabled = false;
                        break; // 只要有一個不滿足條件，就停止檢查
                    }
                }

                // 步驟 2: 檢查 DeviceForm 登錄檔值是否已設定
                object? regValue = Registry.GetValue($"HKEY_LOCAL_MACHINE\\{REG_PATH}", REG_VALUE, null);
                bool isRegistrySet = regValue is int intValue && intValue == 0x2E;

                // 最終判斷：必須兩者都滿足才算是「已啟用」
                bool isEnabled = allFeaturesEnabled && isRegistrySet;

                // 輸出一個簡潔的狀態總結日誌
                Log($"狀態檢查 -> ViVe 功能: {allFeaturesEnabled}, 登錄檔: {isRegistrySet}, 最終結果: {(isEnabled ? "已啟用" : "未啟用")}");

                if (isEnabled)
                {
                    lblStatus.Text = "狀態：已啟用";
                    lblStatus.ForeColor = Color.LimeGreen;
                    btnEnable.Enabled = false;
                    btnDisable.Enabled = true;
                }
                else
                {
                    lblStatus.Text = "狀態：未啟用";
                    lblStatus.ForeColor = Color.Tomato;
                    btnEnable.Enabled = true;
                    btnDisable.Enabled = false;
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "狀態：未知 (錯誤)";
                lblStatus.ForeColor = Color.OrangeRed;
                LogError($"檢查狀態時發生錯誤: {ex.Message}");
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
                Log("--- 開始啟用流程 ---");

                // 步驟 1: 處理螢幕實體尺寸。如果尺寸未定義 (0x0) 或大於 9.5 吋，則建立開機工作來設定它。
                Log("正在讀取目前螢幕尺寸...");
                var (success, size) = PanelManager.GetDisplaySize();
                if (success)
                {
                    Log($"成功讀取螢幕尺寸: {size.WidthMm}x{size.HeightMm}mm。");

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
                            Log("偵測到未定義的螢幕尺寸 (0x0mm)，正在建立開機工作排程...");
                        }
                        else
                        {
                            Log($"偵測到螢幕尺寸 ({diagonalInches:F2}\") 大於 {MAX_DIAGONAL_INCHES}\" 門檻，正在建立開機工作排程...");
                        }
                        TaskSchedulerManager.CreateSetPanelDimensionsTask();
                        Log("工作排程 'SetPanelDimensions' 已建立。");
                    }
                    else
                    {
                        // 說明尺寸在範圍內，無需操作
                        Log($"螢幕尺寸 ({diagonalInches:F2}\") 在 {MAX_DIAGONAL_INCHES}\" 範圍內，無需建立工作排程。");
                    }
                }
                else
                {
                    LogError("讀取螢幕尺寸失敗，將跳過此步驟。");
                }

                // 步驟 2: 備份現有的登錄檔值，然後設定新的值。
                Log($"正在備份並設定登錄檔...");
                object? currentValue = Registry.GetValue($"HKEY_LOCAL_MACHINE\\{REG_PATH}", REG_VALUE, null);
                if (currentValue != null)
                {
                    File.WriteAllText(BackupFilePath, currentValue.ToString() ?? "");
                    Log($"已備份原始 DeviceForm 數值 ({currentValue})。");
                }
                else if (File.Exists(BackupFilePath))
                {
                    File.Delete(BackupFilePath); // 如果原始值不存在，則刪除舊的備份
                }
                Registry.SetValue($"HKEY_LOCAL_MACHINE\\{REG_PATH}", REG_VALUE, 0x2E, RegistryValueKind.DWord); // 0x2E = 46
                Log("登錄檔已成功設定為 46 (0x2E)。");

                // 步驟 3: 使用 ViVe Manager 啟用所需的功能 ID。
                Log($"正在啟用功能 ID: {string.Join(", ", FEATURE_IDS)}...");
                var updates = Array.ConvertAll(FEATURE_IDS, id => new RTL_FEATURE_CONFIGURATION_UPDATE
                {
                    FeatureId = id,
                    EnabledState = RTL_FEATURE_ENABLED_STATE.Enabled,
                    Operation = RTL_FEATURE_CONFIGURATION_OPERATION.FeatureState,
                    Priority = RTL_FEATURE_CONFIGURATION_PRIORITY.User
                });
                FeatureManager.SetFeatureConfigurations(updates, RTL_FEATURE_CONFIGURATION_TYPE.Runtime);
                FeatureManager.SetFeatureConfigurations(updates, RTL_FEATURE_CONFIGURATION_TYPE.Boot);
                Log("功能已成功啟用。");

                // 流程結束
                btnEnable.Enabled = false;
                btnDisable.Enabled = false;
                Log("--- 啟用流程完成 ---", true);
                PromptForRestart("啟用成功");
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
                Log("--- 停用流程完成 ---", true);
                PromptForRestart("停用成功");
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
            Log("--- 開始停用流程 ---");

            // 步驟 1: 明確地將 ViVe 功能設定為停用狀態。
            // 這一步操作是安全的，因為使用者意圖就是停用該功能體驗。
            Log($"正在明確停用功能 ID: {string.Join(", ", FEATURE_IDS)}...");
            var updates = Array.ConvertAll(FEATURE_IDS, id => new RTL_FEATURE_CONFIGURATION_UPDATE
            {
                FeatureId = id,
                EnabledState = RTL_FEATURE_ENABLED_STATE.Disabled,
                Operation = RTL_FEATURE_CONFIGURATION_OPERATION.FeatureState,
                Priority = RTL_FEATURE_CONFIGURATION_PRIORITY.User
            });
            FeatureManager.SetFeatureConfigurations(updates, RTL_FEATURE_CONFIGURATION_TYPE.Runtime);
            FeatureManager.SetFeatureConfigurations(updates, RTL_FEATURE_CONFIGURATION_TYPE.Boot);
            Log("功能已成功設定為停用狀態。");

            // 步驟 2: 極度安全地還原登錄檔值。
            Log("正在檢查是否需要還原 DeviceForm 登錄檔...");
            try
            {
                // 核心邏輯：只有在備份檔存在時，才對登錄檔進行操作。
                if (File.Exists(BackupFilePath))
                {
                    Log("偵測到備份檔，表示登錄檔是由本工具修改的，將進行還原操作。");
                    using RegistryKey? key = Registry.LocalMachine.OpenSubKey(REG_PATH, true);
                    if (key != null)
                    {
                        if (int.TryParse(File.ReadAllText(BackupFilePath), out int backupValue))
                        {
                            key.SetValue(REG_VALUE, backupValue, RegistryValueKind.DWord);
                            Log($"已從備份檔還原數值: {backupValue}");
                        }
                        else
                        {
                            // 備份檔內容無效，為安全起見，刪除該值。
                            key.DeleteValue(REG_VALUE, false);
                            Log($"備份檔內容無效，已直接移除登錄檔值 '{REG_VALUE}'。");
                        }
                    }
                    File.Delete(BackupFilePath);
                    Log("備份檔已刪除。");
                }
                else
                {
                    // 如果備份檔不存在，即使當前值是 46，也絕不碰它。
                    Log("未找到備份檔。這可能是系統原生設定，為確保系統穩定性，將不會修改登錄檔。");
                }
            }
            catch (Exception ex)
            {
                LogError($"還原登錄檔時發生錯誤: {ex.Message}");
            }

            // 步驟 3: 如果存在，則刪除相關的工作排程。
            if (TaskSchedulerManager.TaskExists())
            {
                Log("正在刪除工作排程 'SetPanelDimensions'...");
                TaskSchedulerManager.DeleteSetPanelDimensionsTask();
                Log("工作排程已刪除。");
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
                "所有變更已套用，需要重新啟動電腦才能完全生效。\n\n您要現在重新啟動嗎？",
                caption, MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (result == DialogResult.Yes)
            {
                Log("使用者選擇立即重新啟動電腦。");
                try
                {
                    Process.Start("shutdown.exe", "/r /t 5"); // 5 秒後重新啟動
                    Application.Exit();
                }
                catch (Exception ex)
                {
                    LogError($"無法執行重新啟動命令: {ex.Message}");
                    MessageBox.Show("無法自動執行重新啟動命令，請手動重新啟動您的電腦。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            lblStatus.Text = "狀態：等待重新啟動...";
            lblStatus.ForeColor = Color.Orange;
            btnEnable.Enabled = false;
            btnDisable.Enabled = false;
            Log("使用者選擇稍後重新啟動。在重新啟動前，無法進行其他操作。");
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
            string timestamp = $"[{DateTime.Now:HH:mm:ss}] [錯誤] ";
            txtOutput.SelectionColor = Color.Tomato;
            txtOutput.AppendText(timestamp + message + Environment.NewLine);
            txtOutput.ScrollToCaret();
        }

        private void HandleException(Exception ex)
        {
            LogError($"發生未預期的錯誤: {ex.Message}");
            LogError("請確認本程式是否以【系統管理員權限】執行。");
            MessageBox.Show($"發生錯誤:\n{ex.Message}\n\n請確認您是以系統管理員權限執行本程式。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        #endregion
    }
}