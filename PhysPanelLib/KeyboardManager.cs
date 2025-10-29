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
        public TabTipActivationException(string message, Exception? innerException) : base(message, innerException) { }
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
        private const string ShellProcessName = "explorer";
        private static readonly TimeSpan ShellReadyTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ComServiceTimeout = TimeSpan.FromSeconds(10);

        // 在啟動鍵盤後加入的延遲，給予足夠的反應時間。
        private const int PostLaunchDelayMs = 5000;

        private static readonly Lazy<string?> TabTipPath = new Lazy<string?>(() =>
        {
            string commonProgramFiles = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
            string path = Path.Combine(commonProgramFiles, @"Microsoft Shared\ink", TabTipFileName);
            return File.Exists(path) ? path : null;
        });

        /// <summary>
        /// 確保觸控鍵盤處於「收起」狀態，並使其處理程序在背景待命。
        /// 此方法包含等待 explorer.exe 就緒的邏輯，適用於系統登入時執行。
        /// </summary>
        public static void StartTouchKeyboard()
        {
            // 如果處理程序已在執行，直接返回
            if (Process.GetProcessesByName(TabTipProcessName).Any())
            {
                return; // 處理程序已在執行，直接返回
            }

            // 如果找不到 TabTip.exe 的路徑，記錄錯誤並返回
            if (string.IsNullOrEmpty(TabTipPath.Value))
            {
                // 拋出一個明確的錯誤，而不是靜默失敗
                throw new TabTipNotFoundException($"{TabTipFileName} executable not found in its expected path.");
            }

            try
            {
                // 步驟 1: 等待 explorer.exe 準備就緒
                if (!WaitForExplorerProcess(ShellReadyTimeout))
                {
                    throw new TabTipActivationException($"Timed out waiting for the Windows Shell ({ShellProcessName}.exe) to start.", null);
                }

                // 步驟 2: 啟動 TabTip.exe 處理程序
                var startInfo = new ProcessStartInfo(TabTipPath.Value) { UseShellExecute = true };
                Process.Start(startInfo);

                // 步驟 3: 等待固定時間，讓系統的桌面環境完全準備好
                Thread.Sleep(PostLaunchDelayMs);

                // 步驟 4: 輪詢 COM 服務，直到它準備就緒
                ITipInvocation? tipInvocation = PollForComService(ComServiceTimeout);

                // 步驟 5: 如果成功取得 COM 物件，發送命令將其隱藏
                if (tipInvocation != null)
                {
                    try
                    {
                        tipInvocation.Toggle(IntPtr.Zero); // 執行 "切換" 來隱藏觸控鍵盤
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(tipInvocation);
                    }
                }
                else
                {
                    throw new TabTipActivationException($"Timed out waiting for the {TabTipFileName} COM service to become available.", null);
                }
            }
            catch (Exception ex) when (ex is not TabTipActivationException && ex is not TabTipNotFoundException)
            {
                throw new TabTipActivationException($"An unexpected error occurred while starting or hiding {TabTipFileName}.", ex);
            }
        }

        /// <summary>
        /// 等待 explorer.exe 處理程序啟動。
        /// 這是確保桌面環境已準備好接收 COM 命令的關鍵步驟。
        /// </summary>
        /// <param name="timeout">最長等待時間。</param>
        /// <returns>如果處理程序在超時前出現，則為 true；否則為 false。</returns>
        private static bool WaitForExplorerProcess(TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                if (Process.GetProcessesByName(ShellProcessName).Any())
                {
                    return true; // 找到了
                }
                Thread.Sleep(500); // 每半秒檢查一次
            }
            return false; // 超時
        }

        /// <summary>
        /// 在指定的時間內，持續輪詢 COM 服務，嘗試建立 TipInvocation 物件。
        /// 這是為了應對處理程序啟動後 COM 服務需要時間初始化的情況。
        /// </summary>
        /// <returns>成功時返回 ITipInvocation 物件，逾時則返回 null。</returns>
        private static ITipInvocation? PollForComService(TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                try
                {
                    return (ITipInvocation)new TipInvocation();
                }
                catch (COMException)
                {
                    Thread.Sleep(250); // 等待一小段時間再重試
                }
            }
            return null;
        }
    }
}