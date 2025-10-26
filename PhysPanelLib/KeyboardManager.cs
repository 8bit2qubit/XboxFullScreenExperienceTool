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

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PhysPanelLib
{
    /// <summary>
    /// 表示找不到 TabTip.exe 執行檔時發生的錯誤。
    /// </summary>
    public class TabTipNotFoundException : Exception
    {
        // 提供一個標準的建構函式
        public TabTipNotFoundException(string message) : base(message) { }
    }

    /// <summary>
    /// 表示啟動或透過 COM 啟用觸控鍵盤失敗時發生的錯誤。
    /// </summary>
    public class TabTipActivationException : Exception
    {
        // 提供一個標準的建構函式，可以包含內部錯誤訊息
        public TabTipActivationException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// 提供與 Windows 觸控鍵盤 (TabTip.exe) 互動的功能。
    /// </summary>
    public static class KeyboardManager
    {
        #region Win32 & COM Definitions
        [ComImport, Guid("4CE576FA-83DC-4F88-951C-9D0782B4E376")]
        private class TipInvocation { }

        [ComImport, Guid("37c994e7-432b-4834-a2f7-dce1f13b834b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITipInvocation { void Toggle(IntPtr hwnd); }
        #endregion

        private const string TabTipProcessName = "TabTip";
        private const string TabTipFileName = "TabTip.exe";
        private const int KeyboardReadyDelayMs = 5000; // 鍵盤啟動後的準備延遲時間

        private static readonly Lazy<string?> TabTipPath = new Lazy<string?>(() =>
        {
            string commonProgramFiles = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
            string path = Path.Combine(commonProgramFiles, @"Microsoft Shared\ink", TabTipFileName);
            return File.Exists(path) ? path : null;
        });

        /// <summary>
        /// 確保觸控鍵盤處於「收起」狀態，並使其處理程序在背景待命。
        /// 如果鍵盤已在執行，此方法不會執行任何操作。
        /// </summary>
        /// <exception cref="FileNotFoundException">當找不到 TabTip.exe 時拋出。</exception>
        /// <exception cref="InvalidOperationException">當啟動程序或透過 COM 操作鍵盤失敗時拋出。</exception>
        public static void StartTouchKeyboard()
        {
            // 如果處理程序已在執行，直接返回
            if (Process.GetProcessesByName(TabTipProcessName).Any())
            {
                return; // 已經在執行，直接返回
            }

            // 如果找不到 TabTip.exe 的路徑，記錄錯誤並返回
            if (string.IsNullOrEmpty(TabTipPath.Value))
            {
                // 拋出一個明確的錯誤，而不是靜默失敗
                throw new TabTipNotFoundException($"{TabTipFileName} executable not found in its expected path.");
            }

            try
            {
                // 步驟 1: 啟動 TabTip.exe 處理程序
                var startInfo = new ProcessStartInfo(TabTipPath.Value) { UseShellExecute = true };
                Process.Start(startInfo);

                // 等待鍵盤視窗出現
                Thread.Sleep(KeyboardReadyDelayMs);

                // 步驟 2: 透過 COM 介面將其隱藏
                HideKeyboard();
            }
            catch (Exception ex)
            {
                // 將底層的錯誤包裝成一個新的例外並拋出
                throw new TabTipActivationException($"Failed to start or toggle {TabTipFileName}.", ex);
            }
        }

        /// <summary>
        /// 使用 COM 互通來切換觸控鍵盤的顯示狀態。
        /// </summary>
        private static void HideKeyboard()
        {
            ITipInvocation? tipInvocation = null;
            try
            {
                tipInvocation = (ITipInvocation)new TipInvocation();
                tipInvocation.Toggle(IntPtr.Zero); // 執行 "切換" 來隱藏它
            }
            finally // 即使 COM 呼叫失敗，也要確保釋放物件
            {
                if (tipInvocation != null)
                {
                    Marshal.ReleaseComObject(tipInvocation);
                }
            }
        }
    }
}