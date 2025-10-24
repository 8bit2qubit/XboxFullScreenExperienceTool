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
        /// 執行以下工作：
        /// 1. 檢查並確保只有一個應用程式實例在執行。
        /// 2. 處理特殊的命令列參數 (如 /silentdisable)。
        /// 3. 初始化並執行主視窗。
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // --- 0. 設定預設 UI 語言 ---
            // 根據作業系統語言設定目前執行緒的 UI 在地化
            // 這將決定應用程式啟動時預設載入的資源檔 (例如，Strings.zh-TW.resx)
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InstalledUICulture;

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
                return;
            }

            // --- 2. 命令列參數處理 (Command-Line Argument Handling) ---

            // 處理特殊命令列參數，主要用於安裝程式在解除安裝過程中自動執行清理作業。
            if (args.Length > 0 && args[0].Equals("/silentdisable", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // 呼叫 MainForm 中的靜態方法來執行靜默停用，無需建立表單實例。
                    MainForm.PerformSilentDisable();
                }
                catch
                {
                    // 在靜默模式下，不應顯示任何 UI 或錯誤訊息，因此靜默地忽略所有潛在的例外狀況。
                }

                // 執行完畢後直接退出，不啟動主視窗。
                return;
            }

            // --- 3. 啟動主應用程式 (Launch Main Application) ---

            // 初始化應用程式設定並啟動主表單的訊息迴圈。
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());

            // --- 4. 資源釋放 (Resource Cleanup) ---

            // 應用程式正常關閉時，釋放 Mutex 鎖，以便其他實例可以啟動。
            // 注意：如果程式崩潰，Mutex 可能不會被釋放，但作業系統最終會清理它。
            _mutex.ReleaseMutex();
        }
    }
}