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

using Microsoft.Win32;

namespace XboxFullScreenExperienceTool.Helpers
{
    /// <summary>
    /// 處理 Windows 登入策略與使用者帳戶資訊的輔助類別。
    /// 提供檢查使用者密碼狀態與設定自動重新啟動登入 (ARSO) 的功能。
    /// </summary>
    public static class LogonPolicyHelper
    {
        private const string REG_PATH_POLICIES_SYSTEM = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
        private const string REG_VALUE_ARSO = "DisableAutomaticRestartSignOn";

        /// <summary>
        /// 檢查系統策略是否已停用「自動重新啟動登入」(ARSO)。
        /// 讀取登錄檔 DisableAutomaticRestartSignOn 的值。
        /// </summary>
        /// <returns>如果 ARSO 已被停用 (值為 1)，返回 true；否則返回 false。</returns>
        public static bool IsArsoDisabled()
        {
            try
            {
                // 讀取登錄機碼，預設值為 0 (未停用)
                int value = (int)(Registry.GetValue($"HKEY_LOCAL_MACHINE\\{REG_PATH_POLICIES_SYSTEM}", REG_VALUE_ARSO, 0) ?? 0);
                return value == 1;
            }
            catch (Exception)
            {
                // 讀取失敗時假設為預設狀態 (未停用)
                return false;
            }
        }

        /// <summary>
        /// 設定系統策略以啟用或停用「自動重新啟動登入」(ARSO)。
        /// 修改登錄檔 DisableAutomaticRestartSignOn 的值。
        /// </summary>
        /// <param name="disable">設定為 true 以停用 ARSO (寫入 1)；設定為 false 以啟用 ARSO (寫入 0)。</param>
        public static void SetArsoDisabled(bool disable)
        {
            // 1 代表停用功能，0 代表啟用功能 (預設)
            int value = disable ? 1 : 0;
            Registry.SetValue($"HKEY_LOCAL_MACHINE\\{REG_PATH_POLICIES_SYSTEM}", REG_VALUE_ARSO, value, RegistryValueKind.DWord);
        }
    }
}