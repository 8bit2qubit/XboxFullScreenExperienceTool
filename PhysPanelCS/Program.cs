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

using System;
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
                    Console.Error.WriteLine($"錯誤：未知的命令 '{args[0]}'");
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

                Console.WriteLine($"目前的尺寸: {size.WidthMm}x{size.HeightMm}mm ({diagonalInches:F2}\")");
                return 0; // 0 代表成功
            }
            else
            {
                Console.Error.WriteLine("錯誤：無法取得顯示器尺寸。");
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
                Console.Error.WriteLine("錯誤：'set' 命令的參數格式不正確。");
                PrintUsage();
                return 1; // 1 代表用法錯誤
            }

            var newSize = new Dimensions { WidthMm = width, HeightMm = height };
            if (PanelManager.SetDisplaySize(newSize))
            {
                Console.WriteLine("設定成功。");
                return 0; // 0 代表成功
            }
            else
            {
                // 這是關鍵提示，因為修改 WNF 狀態通常需要更高的權限。
                Console.Error.WriteLine("錯誤：無法設定顯示器尺寸。此操作可能需要以 SYSTEM 使用者權限執行。");
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
            Console.WriteLine("PhysPanelCS 0.0.1 - Windows 內部顯示器實體尺寸工具");
            Console.WriteLine("-----------------------------------------------------");
            Console.WriteLine("用法: PhysPanelCS <命令> [參數...]");
            Console.WriteLine();
            Console.WriteLine("命令:");
            Console.WriteLine("  get               取得目前的實體尺寸設定。");
            Console.WriteLine("  set <寬> <高>     設定新的實體尺寸 (單位為公釐)。");
            Console.WriteLine();
            Console.WriteLine("範例:");
            Console.WriteLine("  PhysPanelCS get");
            Console.WriteLine("  PhysPanelCS set 155 87");
            Console.WriteLine();
        }
    }
}