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

using System.Runtime.InteropServices;

namespace XboxFullScreenExperienceTool.Helpers
{
    /// <summary>
    /// 提供用於偵測硬體功能的靜態輔助方法。
    /// </summary>
    public static class HardwareHelper
    {
        // 透過 P/Invoke 從 user32.dll 匯入 GetSystemMetrics 函式
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        /// <summary>
        /// GetSystemMetrics 的參數：查詢系統支援的最大觸控點數。
        /// </summary>
        private const int SM_MAXIMUMTOUCHES = 95;

        /// <summary>
        /// 檢查系統是否具備觸控螢幕功能。
        /// </summary>
        /// <returns>如果裝置支援觸控，則返回 <c>true</c>；否則返回 <c>false</c>。</returns>
        public static bool IsTouchScreenAvailable()
        {
            // GetSystemMetrics(SM_MAXIMUMTOUCHES) 會回傳硬體支援的觸控點數。
            // 如果回傳值大於 0，表示至少有一個觸控點，即存在觸控螢幕。
            return GetSystemMetrics(SM_MAXIMUMTOUCHES) > 0;
        }
    }
}