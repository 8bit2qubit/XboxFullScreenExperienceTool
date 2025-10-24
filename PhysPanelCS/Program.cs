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
using System.Reflection;
using PhysPanelLib;

namespace PhysPanelCS
{
    /// <summary>
    /// 應用程式的主類別，作為 PhysPanelLib 的命令列介面前端。
    /// 負責解析命令列參數並呼叫對應的函式庫功能。
    /// </summary>
    class Program
    {
        /// <summary>
        /// 應用程式的主要進入點。
        /// 負責解析命令列參數並分派到相應的處理函式。
        /// </summary>
        /// <param name="args">命令列傳入的參數陣列。</param>
        /// <returns>傳回 0 代表成功，非 0 代表失敗。</returns>
        static int Main(string[] args)
        {
            // 設定目前執行緒的 UI 文化為作業系統的安裝語言。
            // 這確保了從資源檔 (Resources.Strings) 讀取的字串會是正確的語言。
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InstalledUICulture;

            // 檢查是否提供了至少一個參數 (get/set)
            if (args.Length < 1)
            {
                PrintUsage();
                return 1; // 1 代表用法錯誤
            }

            string action = args[0].ToLowerInvariant(); // 將命令轉為小寫以方便比較
            switch (action)
            {
                case "get":
                    return HandleGet();
                case "set":
                    return HandleSet(args);
                case "startkeyboard":
                    return HandleStartKeyboard();
                default:
                    Console.Error.WriteLine(string.Format(Resources.Strings.ErrorUnknownCommand, args[0]));
                    PrintUsage();
                    return 1; // 1 代表用法錯誤
            }
        }

        //======================================================================
        // 命令處理函式 (Command Handlers)
        //======================================================================

        /// <summary>
        /// 處理 'get' 命令：取得並顯示目前的顯示器實體尺寸。
        /// </summary>
        /// <returns>傳回 0 代表成功，-1 代表失敗。</returns>
        private static int HandleGet()
        {
            var (success, size) = PanelManager.GetDisplaySize();
            if (success)
            {
                // 使用畢氏定理 (a²+b²=c²) 計算對角線長度 (mm)
                double diagonalMm = Math.Sqrt(Math.Pow(size.WidthMm, 2) + Math.Pow(size.HeightMm, 2));
                // 將公釐轉換為英寸 (1 英寸 = 25.4 公釐)
                double diagonalInches = diagonalMm / 25.4;

                Console.WriteLine(string.Format(Resources.Strings.CurrentSize, size.WidthMm, size.HeightMm, diagonalInches));
                return 0; // 0 代表成功
            }
            else
            {
                Console.Error.WriteLine(Resources.Strings.ErrorGetFailed);
                return -1; // -1 代表執行期間發生錯誤
            }
        }

        /// <summary>
        /// 處理 'set' 命令：根據使用者輸入設定新的顯示器實體尺寸。
        /// </summary>
        /// <param name="args">完整的命令列參數陣列。</param>
        /// <returns>傳回 0 代表成功，1 代表參數錯誤，-1 代表執行失敗。</returns>
        private static int HandleSet(string[] args)
        {
            // 驗證參數：必須是 "set" 加上兩個可成功解析為無正負號整數的參數。
            if (args.Length != 3 ||
                !uint.TryParse(args[1], out uint width) ||
                !uint.TryParse(args[2], out uint height))
            {
                Console.Error.WriteLine(Resources.Strings.ErrorSetInvalid);
                PrintUsage();
                return 1; // 1 代表用法錯誤
            }

            var newSize = new Dimensions { WidthMm = width, HeightMm = height };

            // 接收 uint 狀態碼
            uint status = PanelManager.SetDisplaySize(newSize);

            if (status == 0) // 0 代表 STATUS_SUCCESS
            {
                Console.WriteLine(Resources.Strings.SetSuccess);
                return 0; // 0 代表成功
            }
            else
            {
                // 這是關鍵提示，因為修改 WNF 狀態需要更高的 SYSTEM 權限。
                Console.Error.WriteLine(Resources.Strings.ErrorSetFailed);
                // 顯示詳細的 NTSTATUS 錯誤碼
                Console.Error.WriteLine(string.Format(Resources.Strings.ErrorNtStatusErrorCode, status));
                return -1; // -1 代表執行期間發生錯誤
            }
        }

        /// <summary>
        /// 處理 'startkeyboard' 命令：啟動軟體鍵盤。
        /// </summary>
        private static int HandleStartKeyboard()
        {
            try
            {
                Console.WriteLine(Resources.Strings.AttemptingStartKeyboard);
                PanelManager.StartTouchKeyboard();
                Console.WriteLine(Resources.Strings.KeyboardStartCommandSuccess);
                return 0; // 成功
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(string.Format(Resources.Strings.ErrorFailedToStartKeyboard, ex.Message));
                return -1; // 執行失敗
            }
        }

        //======================================================================
        // 輔助方法 (Helper Methods)
        //======================================================================

        /// <summary>
        /// 將程式的用法說明顯示到主控台，所有文字均來自多國語言資源檔。
        /// </summary>
        private static void PrintUsage()
        {
            // 動態從組件中取得版本號，格式為 "Major.Minor.Build"。
            // ?. (null-conditional operator) 確保如果 GetName() 或 Version 為 null 也不會拋出例外。
            // ?? (null-coalescing operator) 在版本號無法取得時提供一個來自資源檔的預設值。
            string versionString = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? Resources.Strings.UnknownVersion;

            Console.WriteLine();
            Console.WriteLine(string.Format(Resources.Strings.Description, versionString)); // 使用 string.Format 將版本號插入到從資源檔讀取的在地化描述字串中
            Console.WriteLine("-----------------------------------------------------");
            Console.WriteLine(Resources.Strings.Usage);
            Console.WriteLine();
            Console.WriteLine(Resources.Strings.Commands);
            Console.WriteLine(Resources.Strings.GetDescription);
            Console.WriteLine(Resources.Strings.SetDescription);
            Console.WriteLine(Resources.Strings.StartKeyboardDescription);
            Console.WriteLine();
            Console.WriteLine(Resources.Strings.Examples);
            Console.WriteLine(Resources.Strings.GetExample);
            Console.WriteLine(Resources.Strings.SetExample);
            Console.WriteLine(Resources.Strings.StartKeyboardExample);
            Console.WriteLine();
        }
    }
}