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
using PhysPanelLib; // 引入自訂的函式庫

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
            // 這將決定應用程式啟動時預設載入的資源檔 (例如，Strings.zh-TW.resx)
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InstalledUICulture;

            // 檢查是否提供了至少一個參數 (get/set)
            if (args.Length < 1)
            {
                PrintUsage();
                return 1; // 1 通常代表用法錯誤
            }

            string action = args[0].ToLowerInvariant(); // 將命令轉為小寫以方便比較
            switch (action)
            {
                case "get":
                    return HandleGet();
                case "set":
                    return HandleSet(args);
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
            if (PanelManager.SetDisplaySize(newSize))
            {
                Console.WriteLine(Resources.Strings.SetSuccess);
                return 0; // 0 代表成功
            }
            else
            {
                // 這是關鍵提示，因為修改 WNF 狀態通常需要更高的權限。
                Console.Error.WriteLine(Resources.Strings.ErrorSetFailed);
                return -1; // -1 代表執行期間發生錯誤
            }
        }

        //======================================================================
        // 輔助方法 (Helper Methods)
        //======================================================================

        /// <summary>
        /// 將程式的用法說明顯示到主控台。
        /// </summary>
        private static void PrintUsage()
        {
            Console.WriteLine();
            Console.WriteLine(Resources.Strings.Description);
            Console.WriteLine("-----------------------------------------------------");
            Console.WriteLine(Resources.Strings.Usage);
            Console.WriteLine();
            Console.WriteLine(Resources.Strings.Commands);
            Console.WriteLine(Resources.Strings.GetDescription);
            Console.WriteLine(Resources.Strings.SetDescription);
            Console.WriteLine();
            Console.WriteLine(Resources.Strings.Examples);
            Console.WriteLine(Resources.Strings.GetExample);
            Console.WriteLine(Resources.Strings.SetExample);
            Console.WriteLine();
        }
    }
}