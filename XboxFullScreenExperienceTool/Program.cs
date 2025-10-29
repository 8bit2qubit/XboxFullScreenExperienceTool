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

using System.Globalization;
using System.Diagnostics;
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

            // --- 步驟 0: 處理靜默解除安裝命令 ---
            if (args.Length > 0 && args[0].Equals("/silentdisable", StringComparison.OrdinalIgnoreCase))
            {
                #region Uninstall Logger Configuration
#if DEBUG
                // 建立一個簡單的檔案記錄器，用於在靜默模式下偵錯
                Action<string> silentLogger = (message) => {
                    try
                    {
                        // 將日誌寫入一個固定的、可預測的位置
                        Directory.CreateDirectory(@"C:\temp");
                        File.AppendAllText(@"C:\temp\XFSET_uninstall_log.txt", $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
                    }
                    catch { /* 忽略日誌寫入失敗 */ }
                };
#else
                // 在 Release 模式下，日誌記錄器什麼也不做。
                Action<string> silentLogger = (message) => { };
#endif
                #endregion

                silentLogger("Silent disable command detected.");
                silentLogger($"Build configuration: {(IsDebugBuild() ? "DEBUG" : "RELEASE")}");

                // 從參數中解析安裝路徑
                string installPath = "";
                foreach (var arg in args)
                {
                    if (arg.StartsWith("/installpath=", StringComparison.OrdinalIgnoreCase))
                    {
                        installPath = arg.Substring("/installpath=".Length).Trim('"');
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                {
                    // 設定全域路徑，供所有輔助類別使用
                    AppPathManager.InstallPath = installPath;
                    silentLogger($"Install path successfully set to: '{installPath}'");

                    // 呼叫完全靜態的清理方法，並同步等待其完成
                    bool cleanupSuccess = false; // 預設為失敗
                    try
                    {
                        // 直接使用方法的布林回傳值
                        cleanupSuccess = MainForm.SilentActionHandler.PerformUninstallCleanup(silentLogger).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        silentLogger($"FATAL ERROR during silent cleanup: {ex.Message}");
                        // 發生未處理的例外時，cleanupSuccess 保持為 false
                    }

                    // 只有在清理成功後才觸發重新開機
                    if (cleanupSuccess)
                    {
                        silentLogger("Cleanup successful. Initiating system reboot in 5 seconds.");
                        try
                        {
                            // 使用 shutdown.exe 命令
                            // /r: 重新開機
                            // /t 5: 等待 5 秒 (給使用者一點緩衝時間，也可以設為 0)
                            // /c "..." : 在關機對話方塊中顯示的註解
                            // /d p:4:1 : 記錄關機原因為「應用程式：維護 (計畫內)」
                            string shutdownArgs = $"/r /t 5 /c \"{Resources.Strings.ShutdownReasonUninstall}\" /d p:4:1";
                            Process.Start("shutdown.exe", shutdownArgs);
                        }
                        catch (Exception ex)
                        {
                            silentLogger($"ERROR initiating reboot: {ex.Message}");
                        }
                        return 0; // 成功
                    }
                    else
                    {
                        silentLogger("Cleanup failed. Reboot is cancelled. Exiting with error code 1.");
                        return 1; // 失敗
                    }
                }
                else
                {
                    silentLogger($"ERROR: Install path not found or is invalid. Exiting with error code 1. Args: {string.Join(" ", args)}");
                    return 1; // 失敗
                }
            }

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