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
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace XboxFullscreenExperienceTool.Helpers
{
    /// <summary>
    /// 提供用於管理特定 Windows 工作排程器工作的靜態輔助方法。
    /// 這個類別是 `schtasks.exe` 命令列工具的一個高階封裝。
    /// </summary>
    public static class TaskSchedulerManager
    {
        /// <summary>
        /// 定義要管理的工作排程的唯一名稱。
        /// </summary>
        private const string TASK_NAME = "SetPanelDimensions";

        /// <summary>
        /// 組合並傳回 `PhysPanelCS.exe` 工具的完整路徑。
        /// </summary>
        /// <returns>假設 `PhysPanelCS.exe` 與主程式位於同一目錄下的絕對路徑。</returns>
        /// <exception cref="DirectoryNotFoundException">如果無法確定應用程式的執行目錄，則擲出此例外狀況。</exception>
        private static string GetPhysPanelPath()
        {
            // 取得目前執行中組件 (此 DLL 或 EXE) 的完整路徑
            string assemblyLocation = Assembly.GetExecutingAssembly().Location;
            // 從完整路徑中提取目錄部分
            string? assemblyDirectory = Path.GetDirectoryName(assemblyLocation);

            // 健全性檢查，確保目錄路徑有效
            if (string.IsNullOrEmpty(assemblyDirectory))
            {
                throw new DirectoryNotFoundException("無法取得應用程式的安裝目錄。");
            }

            // 安全地組合目錄和檔名，形成一個跨平台相容的完整路徑
            return Path.Combine(assemblyDirectory, "PhysPanelCS.exe");
        }

        /// <summary>
        /// 檢查具有預定義名稱 (`TASK_NAME`) 的工作排程是否存在。
        /// </summary>
        /// <returns>如果工作存在，則為 <c>true</c>；否則為 <c>false</c>。</returns>
        public static bool TaskExists()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    // 參數：查詢 (/query) 特定名稱 (/tn) 的工作
                    Arguments = $"/query /tn \"{TASK_NAME}\"",
                    UseShellExecute = false,       // 不使用作業系統殼層啟動
                    RedirectStandardOutput = true, // 重新導向標準輸出，以便可以忽略它
                    CreateNoWindow = true          // 不建立可見的命令提示字元視窗
                }
            };
            process.Start();
            process.WaitForExit(); // 等待命令執行完成

            // `schtasks /query` 命令在成功找到工作時傳回結束代碼 0。
            // 任何非 0 的代碼都表示找不到工作或發生錯誤。
            return process.ExitCode == 0;
        }

        /// <summary>
        /// 建立或覆寫一個在系統啟動時執行的工作排程。
        /// 此工作會以 SYSTEM 權限執行 `PhysPanelCS.exe set 155 87`。
        /// </summary>
        /// <exception cref="FileNotFoundException">如果找不到 `PhysPanelCS.exe`，則擲出此例外狀況。</exception>
        /// <exception cref="Exception">如果 `schtasks.exe` 命令因任何其他原因失敗，則擲出此例外狀況。</exception>
        public static void CreateSetPanelDimensionsTask()
        {
            string physPanelPath = GetPhysPanelPath();
            // 執行前檢查：確保必要的工具程式存在，若不存在則提早失敗並提供清晰的錯誤訊息。
            if (!File.Exists(physPanelPath))
            {
                throw new FileNotFoundException($"找不到必要的檔案: {physPanelPath}。請確保 PhysPanelCS.exe 與主程式位於同一個目錄。");
            }

            // 使用 XML 定義工作排程。這種方法比使用一長串命令列參數更精確且可靠。
            string xmlContent = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Author>XboxFullscreenExperienceTool</Author>
    <URI>\{TASK_NAME}</URI>
  </RegistrationInfo>
  <Triggers>
    <!-- 觸發器：當系統啟動時執行 -->
    <BootTrigger>
      <Enabled>true</Enabled>
    </BootTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <!-- 執行身份：S-1-5-18 是 LOCAL SYSTEM 帳戶的通用 SID，提供最高權限 -->
      <UserId>S-1-5-18</UserId>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <!-- 設定：允許在電池供電模式下執行，這對掌上裝置很重要 -->
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <!-- 優先權設為 4 (高於正常) 以確保及時執行 -->
    <Priority>4</Priority>
  </Settings>
  <Actions Context=""Author"">
    <!-- 動作：要執行的命令及其參數 -->
    <Exec>
      <Command>""{physPanelPath}""</Command>
      <Arguments>set 155 87</Arguments>
    </Exec>
  </Actions>
</Task>";

            // `schtasks /create /xml` 需要一個 XML 檔案路徑，因此先建立一個暫存檔案。
            var tempXmlFile = Path.GetTempFileName();
            File.WriteAllText(tempXmlFile, xmlContent);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    // 參數：建立 (/create) 工作，從 XML 檔案 (/xml) 讀取設定，並在工作已存在時強制覆寫 (/f)。
                    Arguments = $"/create /tn \"{TASK_NAME}\" /xml \"{tempXmlFile}\" /f",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true, // 重新導向錯誤流以進行偵錯
                    CreateNoWindow = true
                }
            };
            process.Start();
            string err = process.StandardError.ReadToEnd(); // 捕獲任何錯誤輸出
            process.WaitForExit();
            File.Delete(tempXmlFile); // 無論成功與否，都清理暫存檔案

            // 結束代碼 0 表示成功。任何非 0 值都表示失敗。
            if (process.ExitCode != 0)
            {
                throw new Exception($"建立工作排程失敗: {err}");
            }
        }

        /// <summary>
        /// 刪除由這個類別管理的 'SetPanelDimensions' 工作排程。
        /// </summary>
        /// <exception cref="Exception">如果刪除失敗並傳回非預期的錯誤代碼，則擲出此例外狀況。</exception>
        public static void DeleteSetPanelDimensionsTask()
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    // 參數：刪除 (/delete) 工作，並強制執行 (/f) 以避免確認提示。
                    Arguments = $"/delete /tn \"{TASK_NAME}\" /f",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            string err = process.StandardError.ReadToEnd();
            process.WaitForExit();

            // `schtasks /delete` 的結束代碼有特殊含義：
            // 0: 成功刪除。
            // 1: 找不到指定的工作。
            // 在邏輯中，如果工作本來就不存在，也算是達成了「確保工作不存在」的目標，因此不應視為錯誤。
            // 只有當結束代碼大於 1 時，才表示發生了真正的錯誤。
            if (process.ExitCode > 1)
            {
                throw new Exception($"刪除工作排程失敗: {err}");
            }
        }
    }
}