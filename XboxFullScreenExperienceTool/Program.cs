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

using System.Diagnostics;
using System.Globalization;
using XboxFullScreenExperienceTool.Helpers;

namespace XboxFullScreenExperienceTool
{
    /// <summary>
    /// 應用程式的主進入點與初始化邏輯。
    /// 這個類別負責處理單一執行個體檢查、命令列參數解析以及啟動主視窗。
    /// </summary>
    internal static class Program
    {
        //======================================================================
        // 常數與靜態欄位 (Constants & Static Fields)
        //======================================================================

        /// <summary>
        /// 用於確保應用程式單一執行個體的 Mutex (互斥鎖) 全域名稱。
        /// 這個名稱在作業系統中必須是唯一的，以防止與其他應用程式衝突。
        /// </summary>
        private const string AppMutexName = "XboxFullScreenExperienceTool-SingleInstanceMutex-8bit2qubit";

        /// <summary>
        /// 持有的 Mutex 實例，用於在應用程式生命週期內鎖定，並在結束時釋放。
        /// </summary>
        private static Mutex? _mutex = null;

        /// <summary>
        /// 應用程式的主要進入點。
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            // 設定預設 UI 語言 ---
            // 根據作業系統語言設定目前執行緒的 UI 在地化
            // 這將決定應用程式啟動時預設載入的資源檔 (例如，Strings.zh-TW.resx)
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InstalledUICulture;

            // ================================================================
            // 安裝/維護模式偵測 (Install/Maintenance Mode)
            // ================================================================

            if (args.Length > 0)
            {
                string mode = args[0].ToLowerInvariant();

                // 檢查是否為維護指令 (遷移或移除)
                if (mode == "/migrate" || mode == "/silentdisable")
                {
                    return HandleMaintenanceMode(mode, args);
                }
            }

            // ================================================================
            // 正常應用程式啟動 (Normal App Launch)
            // ================================================================

            // --- 如果不是靜默解除安裝，則執行正常的 UI 應用程式流程 ---

            // --- 1. 單一執行個體檢查 (Single-Instance Check) ---

            // 嘗試取得一個全域 Mutex。
            // - true: 表示呼叫執行緒初始時就想擁有 Mutex。
            // - AppMutexName: Mutex 的唯一名稱。
            // - createdNew: 如果成功建立新的 Mutex (表示是第一個實例)，則為 true；
            //              如果 Mutex 已存在 (表示已有實例在執行)，則為 false。
            _mutex = new Mutex(true, AppMutexName, out bool createdNew);

            if (!createdNew)
            {
                // 如果 Mutex 已存在，顯示錯誤訊息並直接退出。
                MessageBox.Show(Resources.Strings.ErrorAppRunning, Resources.Strings.HandleExceptionTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }

            // --- 2. 啟動主應用程式 (Launch Main Application) ---

            // 初始化應用程式設定並啟動主表單的訊息迴圈。
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());

            // --- 3. 資源釋放 (Resource Cleanup) ---

            // 應用程式正常關閉時，釋放 Mutex 鎖，以便其他實例可以啟動。
            // 注意：如果程式崩潰，Mutex 可能不會被釋放，但作業系統最終會清理它。
            _mutex.ReleaseMutex();
            return 0;
        }

        /// <summary>
        /// 處理安裝期間的特殊操作 (無介面模式)
        /// </summary>
        private static int HandleMaintenanceMode(string mode, string[] args)
        {
            Action<string> logger;

            // 確保 Release 編譯時完全不包含日誌路徑與寫入邏輯
#if DEBUG
            logger = (msg) =>
            {
                try
                {
                    string logPath = Path.Combine(Path.GetTempPath(), "XFSET_Install.log");
                    File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{mode}] {msg}{Environment.NewLine}");
                }
                catch { /* 忽略日誌寫入失敗 */ }
            };
#else
            // 在 Release 模式下，編譯為空委派，不含任何 IO 程式碼
            logger = (msg) => { };
#endif

            logger($"Starting maintenance mode: {mode}");

            // 解析安裝路徑參數 /installpath="..."
            string installPath = "";
            foreach (var arg in args)
            {
                if (arg.StartsWith("/installpath=", StringComparison.OrdinalIgnoreCase))
                {
                    installPath = arg.Substring("/installpath=".Length).Trim('"');
                    break;
                }
            }

            // 驗證安裝路徑
            if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
            {
                logger($"ERROR: Invalid install path: '{installPath}'");
                return 1;
            }

            // 設定全域路徑
            AppPathManager.InstallPath = installPath;
            logger($"Install path set to: {installPath}");

            try
            {
                if (mode == "/migrate")
                {
                    // Commit 階段：
                    // 1. 執行工作排程遷移/更新
                    logger("Executing Task Migration...");
                    TaskSchedulerManager.RunTaskMigration(logger);
                    // 2. 執行功能 ID 更新 (當偵測到備份檔案 DeviceForm.bak 時)
                    logger("Checking and Updating Feature IDs...");
                    MainForm.SilentActionHandler.MigrateFeaturesIfEnabled(logger);
                    logger("Migration completed.");
                }
                else if (mode == "/silentdisable")
                {
                    // Uninstall 階段：執行清理
                    logger("Executing Silent Disable...");
                    // 呼叫 MainForm 中的靜默清理邏輯
                    bool success = MainForm.SilentActionHandler.PerformUninstallCleanup(logger).GetAwaiter().GetResult();

                    if (success)
                    {
                        // 詢問使用者是否刪除日誌檔案
                        try
                        {
                            string logPath = Path.Combine(AppPathManager.InstallPath, "XboxFullScreenExperienceTool.log");
                            string bakPath = logPath + ".bak";

                            // 只有當檔案存在時才詢問
                            if (File.Exists(logPath) || File.Exists(bakPath))
                            {
                                // 顯示詢問視窗
                                DialogResult result = MessageBox.Show(
                                    Resources.Strings.MsgDeleteLogFiles,
                                    Resources.Strings.MsgUninstallTitle,
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Question,
                                    MessageBoxDefaultButton.Button2); // Button2 表示預設選 "否"，避免誤刪

                                if (result == DialogResult.Yes)
                                {
                                    if (File.Exists(logPath)) File.Delete(logPath);
                                    if (File.Exists(bakPath)) File.Delete(bakPath);
                                    logger("Log files deleted by user request.");
                                }
                                else
                                {
                                    logger("User chose to keep log files.");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // 刪除檔案失敗不應中斷解除安裝流程，僅記錄錯誤
                            logger($"WARNING: Failed to delete log files: {ex.Message}");
                        }

                        // 非 Debug 模式才執行重開機
                        if (!IsDebugBuild())
                        {
                            logger("Cleanup success. Requesting reboot.");
                            // 使用 shutdown.exe 命令
                            // /r: 重新開機
                            // /t 5: 等待 5 秒 (給使用者一點緩衝時間，也可以設為 0)
                            // /c "..." : 在關機對話方塊中顯示的註解
                            // /d p:4:1 : 記錄關機原因為「應用程式：維護 (計畫內)」
                            Process.Start("shutdown.exe", $"/r /t 5 /c \"{Resources.Strings.ShutdownReasonUninstall}\" /d p:4:1");
                        }
                        else
                        {
                            logger("Cleanup success. Reboot skipped (DEBUG mode).");
                        }
                    }
                }
                return 0; // 成功
            }
            catch (Exception ex)
            {
                logger($"FATAL ERROR: {ex.Message}");
                return 1; // 失敗
            }
        }

        /// <summary>
        /// 輔助方法，用於在日誌中判斷目前的建置模式。
        /// </summary>
        private static bool IsDebugBuild()
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }
}