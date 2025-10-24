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
    /// 提供與 Windows 觸控鍵盤 (TabTip.exe) 互動的功能。
    /// 優先使用 COM。如果失敗，則啟動處理程序（如果需要），
    /// 然後在逾時時間內持續輪詢，直到 COM 服務恢復可用，最後再發送顯示命令。
    /// </summary>
    internal static class KeyboardManager
    {
        #region COM Definitions
        private const string CLSID_UIHostNoLaunch = "4CE576FA-83DC-4F88-951C-9D0782B4E376";
        [ComImport, Guid("37c994e7-432b-4834-a2f7-dce1f13b834b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITipInvocation { void Toggle(IntPtr hwnd); }
        [ComImport, Guid(CLSID_UIHostNoLaunch)]
        private class TipInvocation { }
        #endregion

        private static readonly Lazy<string?> TabTipPath = new Lazy<string?>(() =>
        {
            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
            string path = Path.Combine(progFiles, @"microsoft shared\ink\TabTip.exe");
            return File.Exists(path) ? path : null;
        });

        /// <summary>
        /// 觸發 Windows 軟體鍵盤的顯示/隱藏，包含完整的恢復邏輯。
        /// </summary>
        public static void ShowKeyboard()
        {
            ITipInvocation? tipInvocation = null;
            try
            {
                // 主要策略：嘗試直接透過 COM 啟用並 Toggle
                tipInvocation = (ITipInvocation)new TipInvocation();
                tipInvocation.Toggle(IntPtr.Zero);
            }
            catch (COMException)
            {
                // 後備恢復策略：當 COM 服務無響應時 (例如 TabTip.exe 被結束工作後)
                try
                {
                    // 步驟 1: 確保處理程序正在執行 (使用 ShellExecute 以取得正確權限)
                    EnsureTabTipProcessIsRunning();

                    // 步驟 2: 輪詢 COM 服務，直到它準備就緒
                    tipInvocation = PollForComService(TimeSpan.FromSeconds(5));

                    // 步驟 3: 如果成功取得 COM 物件，發送命令
                    if (tipInvocation != null)
                    {
                        tipInvocation.Toggle(IntPtr.Zero);
                    }
                }
                catch
                {
                    // 恢復模式中的任何錯誤都靜默處理，以防主程式崩潰
                }
            }
            finally
            {
                // 無論成功或失敗，如果 COM 物件被成功建立，就必須釋放它
                if (tipInvocation != null)
                {
                    Marshal.ReleaseComObject(tipInvocation);
                }
            }
        }

        /// <summary>
        /// 確保 TabTip.exe 處理程序正在執行。如果沒有，則使用 ShellExecute 啟動它。
        /// </summary>
        private static void EnsureTabTipProcessIsRunning()
        {
            if (Process.GetProcessesByName("TabTip").Any())
            {
                return; // 已經在執行
            }

            if (!string.IsNullOrEmpty(TabTipPath.Value))
            {
                // 必須使用 ShellExecute 來請求 Windows Shell 啟動這個受保護的元件，
                // 這樣才能繞過 "需要提升權限" 的錯誤。
                var startInfo = new ProcessStartInfo(TabTipPath.Value)
                {
                    UseShellExecute = true
                };
                Process.Start(startInfo);
            }
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
                    // 反覆嘗試建立 COM 物件，直到成功
                    return (ITipInvocation)new TipInvocation();
                }
                catch (COMException)
                {
                    // 如果失敗，等待一小段時間再重試
                    Thread.Sleep(250);
                }
            }
            return null; // 逾時
        }
    }
}