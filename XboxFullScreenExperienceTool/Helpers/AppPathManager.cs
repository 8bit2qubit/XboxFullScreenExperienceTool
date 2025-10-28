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

using System.Reflection;

namespace XboxFullScreenExperienceTool.Helpers
{
    /// <summary>
    /// 提供一個可靠的全域應用程式安裝路徑。
    /// 這個路徑應由程式的進入點 (Program.cs) 在啟動時根據命令列參數設定。
    /// </summary>
    public static class AppPathManager
    {
        /// <summary>
        /// 取得或設定應用程式的安裝目錄。
        /// 預設值是基於執行中組件的位置，適用於開發或非安裝環境。
        /// 在靜默解除安裝模式下，這個值必須由外部傳入的命令列參數覆寫。
        /// </summary>
        public static string InstallPath { get; set; } =
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
    }
}