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
using System.Diagnostics;

namespace XboxFullScreenExperienceTool.Helpers
{
    /// <summary>
    /// 提供用於管理特定 Windows 工作排程器工作的靜態輔助方法。
    /// 這個類別是 `schtasks.exe` 命令列工具的一個高階封裝。
    /// </summary>
    public static class TaskSchedulerManager
    {
        /// <summary>
        /// 定義用於在系統啟動時設定實體顯示面板的工作排程之唯一名稱。
        /// </summary>
        private const string TASK_NAME = "XFSET-SetPanelDimensions";
        private const string OLD_TASK_NAME = "SetPanelDimensions"; // 舊版名稱，用於遷移

        /// <summary>
        /// 定義用於在登入時啟動遊戲控制器鍵盤的工作排程之唯一名稱。
        /// </summary>
        private const string KEYBOARD_TASK_NAME = "XFSET-StartGamepadKeyboardOnLogon";
        private const string OLD_KEYBOARD_TASK_NAME = "StartTouchKeyboardOnLogon"; // 舊版名稱，用於遷移

        /// <summary>
        /// 判斷是否為 26220.7271 或更新的原生支援版本 (Native Build)。
        /// </summary>
        private static bool IsNativeSupportBuild()
        {
            try
            {
                const string REG_PATH_PARENT = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
                string? currentBuildStr = Registry.GetValue($@"HKEY_LOCAL_MACHINE\{REG_PATH_PARENT}", "CurrentBuild", null)?.ToString();
                string? currentRevisionStr = Registry.GetValue($@"HKEY_LOCAL_MACHINE\{REG_PATH_PARENT}", "UBR", null)?.ToString();

                if (int.TryParse(currentBuildStr, out int build) && int.TryParse(currentRevisionStr, out int revision))
                {
                    // Build > 26220 或 Build = 26220 且 Revision >= 7271
                    if (build > 26220) return true;
                    if (build == 26220 && revision >= 7271) return true;
                }
            }
            catch { /* 忽略錯誤 */ }
            return false;
        }

        /// <summary>
        /// 組合並傳回 `PhysPanelCS.exe` 工具的完整路徑。
        /// </summary>
        /// <returns>假設 `PhysPanelCS.exe` 與主程式位於同一目錄下。</returns>
        /// <exception cref="DirectoryNotFoundException">如果無法確定應用程式的執行目錄，則擲出此例外狀況。</exception>
        private static string GetPhysPanelPath()
        {
            string assemblyDirectory = AppPathManager.InstallPath;

            // 健全性檢查，確保目錄路徑有效
            if (string.IsNullOrEmpty(assemblyDirectory))
            {
                throw new DirectoryNotFoundException(Resources.Strings.TaskSchedulerManagerErrorDirectoryNotFound);
            }

            // 安全地組合目錄和檔名，形成一個跨平台相容的完整路徑
            return Path.Combine(assemblyDirectory, "PhysPanelCS.exe");
        }

        /// <summary>
        /// 檢查指定名稱的工作排程是否存在。
        /// </summary>
        private static bool CheckTaskExists(string taskName)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    // 參數：查詢 (/query) 特定名稱 (/tn) 的工作
                    Arguments = $"/query /tn \"{taskName}\"",
                    UseShellExecute = false,       // 不使用作業系統殼層啟動
                    RedirectStandardOutput = true, // 重新導向標準輸出，以便可以忽略它
                    CreateNoWindow = true          // 不建立可見的命令提示字元視窗
                }
            };
            process.Start();
            process.WaitForExit();  // 等待命令執行完成
            // `schtasks /query` 命令在成功找到工作時傳回結束代碼 0。
            // 任何非 0 的代碼都表示找不到工作或發生錯誤。
            return process.ExitCode == 0;
        }

        public static bool SetPanelDimensionsTaskExists() => CheckTaskExists(TASK_NAME);
        public static bool StartGamepadKeyboardOnLogonTaskExists() => CheckTaskExists(KEYBOARD_TASK_NAME);

        /// <summary>
        /// 供安裝程式在 Commit 階段呼叫，用於遷移舊工作或更新現有工作定義。
        /// </summary>
        public static void RunTaskMigration(Action<string> logger)
        {
            logger("Starting Task Migration...");

            // 1. 處理面板尺寸設定工作
            bool oldPanelTaskExists = CheckTaskExists(OLD_TASK_NAME);
            bool newPanelTaskExists = CheckTaskExists(TASK_NAME);

            // 判斷是否已安裝驅動程式 (PhysPanelDrv)
            // 為了驅動模式下使用 reg 參數 (確保在遷移過程中補建對應的 regOnly 工作)
            bool isDriverInstalled = DriverManager.IsDriverServiceInstalled();

            // 注意：如果舊工作存在，或者新工作已經存在 (需要更新 XML 設定，如參數變更)，都執行重建
            // 只要滿足任一條件：舊工作存在 OR 新工作存在 OR 驅動已安裝
            if (oldPanelTaskExists || newPanelTaskExists || isDriverInstalled)
            {
                logger($"Detecting Panel Task needs update/creation... Old: {oldPanelTaskExists}, New: {newPanelTaskExists}, DrvInstalled: {isDriverInstalled}. Rebuilding...");

                // 判斷 OS 版本
                bool isNative = IsNativeSupportBuild();

                // 先刪除舊有的工作 (無論名稱是新是舊)
                if (oldPanelTaskExists) DeleteTask(OLD_TASK_NAME);
                if (newPanelTaskExists) DeleteTask(TASK_NAME);

                try
                {
                    // 根據 OS 版本或驅動程式狀態決定參數：
                    // Native Build -> 使用 regOnly (僅設定登錄檔，交由系統原生處理 FSE)
                    // Driver Installed -> 使用 regOnly (驅動程式處理 FSE，工作排程僅負責鎖定登錄檔)
                    // Legacy Build -> 使用完整 set 155 87 reg (強制覆寫面板尺寸 + 登錄檔)
                    bool useRegOnly = isNative || isDriverInstalled;

                    logger($"Migrating Panel Task (Native={isNative}, DrvInstalled={isDriverInstalled}) -> Creating task with regOnly={useRegOnly}...");

                    CreateSetPanelDimensionsTask(regOnly: useRegOnly);

                    logger("Panel Task updated successfully.");
                }
                catch (Exception ex)
                {
                    // 建議檢查 XFSET_Install.log
                    logger($"Failed to create Panel Task: {ex.Message}");
                }
            }

            // 2. 處理鍵盤啟動工作
            bool oldKbTaskExists = CheckTaskExists(OLD_KEYBOARD_TASK_NAME);
            bool newKbTaskExists = CheckTaskExists(KEYBOARD_TASK_NAME);

            if (oldKbTaskExists || newKbTaskExists)
            {
                logger($"Detecting Keyboard Task... Old: {oldKbTaskExists}, New: {newKbTaskExists}. Rebuilding...");

                if (oldKbTaskExists) DeleteTask(OLD_KEYBOARD_TASK_NAME);
                if (newKbTaskExists) DeleteTask(KEYBOARD_TASK_NAME);

                try
                {
                    CreateStartGamepadKeyboardOnLogonTask();
                    logger("Keyboard Task updated successfully.");
                }
                catch (Exception ex)
                {
                    // 建議檢查 XFSET_Install.log
                    logger($"Failed to create Keyboard Task: {ex.Message}");
                }
            }

            logger("Task Migration Completed.");
        }

        /// <summary>
        /// 建立或覆寫 SetPanelDimensions 工作排程。
        /// </summary>
        /// <param name="regOnly">如果為 true，則僅執行 'reg' 指令 (適用於 26220.7271+)；否則執行標準的 'set 155 87 reg'。</param>
        /// <exception cref="FileNotFoundException">如果找不到 `PhysPanelCS.exe`，則擲出此例外狀況。</exception>
        /// <exception cref="Exception">如果 `schtasks.exe` 命令因任何其他原因失敗，則擲出此例外狀況。</exception>
        public static void CreateSetPanelDimensionsTask(bool regOnly = false)
        {
            string physPanelPath = GetPhysPanelPath();
            // 執行前檢查：確保必要的工具程式存在，若不存在則提早失敗並提供清晰的錯誤訊息。
            if (!File.Exists(physPanelPath))
            {
                throw new FileNotFoundException(string.Format(Resources.Strings.TaskSchedulerManagerErrorFindFile, physPanelPath));
            }

            // 若 regOnly 為 true，參數僅為 "reg"，否則維持原有的 "set 155 87 reg"
            string arguments = regOnly ? "reg" : "set 155 87 reg";

            // 使用 XML 定義工作排程。這種方法比使用一長串命令列參數更精確且可靠。
            string xmlContent = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Author>XboxFullScreenExperienceTool</Author>
    <URI>\{TASK_NAME}</URI>
  </RegistrationInfo>
  <Triggers>
    <!-- 觸發器：系統啟動時立即執行 (早於使用者登入) -->
    <BootTrigger>
      <Enabled>true</Enabled>
    </BootTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <!-- 執行身分：S-1-5-18 (LocalSystem) -->
      <!-- 使用最高權限 (System) 執行，確保能修改實體顯示面板設定 -->
      <UserId>S-1-5-18</UserId>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <!-- 並行策略：忽略新實例 (避免重複執行) -->
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <!-- 電源設定：針對掌機優化，允許在電池供電模式下執行 -->
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <!-- 容錯設定：允許強制終止，且系統可用時立即啟動 -->
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <!-- 優先級：0 (THREAD_PRIORITY_TIME_CRITICAL)，確保開機後第一時間修改實體顯示面板設定 -->
    <Priority>0</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>""{physPanelPath}""</Command>
      <Arguments>{arguments}</Arguments>
    </Exec>
  </Actions>
</Task>";

            CreateTaskFromXml(TASK_NAME, xmlContent);
        }

        /// <summary>
        /// 建立或覆寫 StartGamepadKeyboardOnLogon 工作排程。
        /// </summary>
        public static void CreateStartGamepadKeyboardOnLogonTask()
        {
            string physPanelPath = GetPhysPanelPath();
            if (!File.Exists(physPanelPath))
            {
                throw new FileNotFoundException(string.Format(Resources.Strings.TaskSchedulerManagerErrorFindFile, physPanelPath));
            }

            string xmlContent = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Author>XboxFullScreenExperienceTool</Author>
    <URI>\{KEYBOARD_TASK_NAME}</URI>
  </RegistrationInfo>
  <Triggers>
    <!-- 觸發器：任何使用者登入時執行 -->
    <LogonTrigger>
      <Enabled>true</Enabled>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <!-- 執行身份：S-1-5-32-545。 -->
      <!-- 這確保當由 Installer 建立工作時，此工作是針對「使用者」群組的。 -->
      <GroupId>S-1-5-32-545</GroupId>
      <!-- 權限等級：最小權限 (避免觸發 UAC 或 UI 隔離問題，確保鍵盤能與桌面互動) -->
      <RunLevel>LeastPrivilege</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <!-- 電源設定：確保掌機在未接電源登入時也能啟動鍵盤服務 (但掌機通常有觸控螢幕，此工作會作用不到) -->
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <!-- 優先級：4 (THREAD_PRIORITY_NORMAL) -->
    <Priority>4</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>""{physPanelPath}""</Command>
      <Arguments>startkeyboard</Arguments>
    </Exec>
  </Actions>
</Task>";

            CreateTaskFromXml(KEYBOARD_TASK_NAME, xmlContent);
        }

        /// <summary>
        /// 通用私有方法：從 XML 建立工作。
        /// </summary>
        private static void CreateTaskFromXml(string name, string xmlContent)
        {
            // `schtasks /create /xml` 需要一個 XML 檔案路徑，因此先建立一個暫存檔案。
            var tempXmlFile = Path.GetTempFileName();
            File.WriteAllText(tempXmlFile, xmlContent);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    // 參數：建立 (/create) 工作，從 XML 檔案 (/xml) 讀取設定，並在工作已存在時強制覆寫 (/f)。
                    Arguments = $"/create /tn \"{name}\" /xml \"{tempXmlFile}\" /f",
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
                throw new Exception(string.Format(Resources.Strings.TaskSchedulerManagerErrorCreate, err));
            }
        }

        /// <summary>
        /// 通用私有方法：刪除工作。
        /// </summary>
        private static void DeleteTask(string name)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    // 參數：刪除 (/delete) 工作，並強制執行 (/f) 以避免確認提示。
                    Arguments = $"/delete /tn \"{name}\" /f",
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
                throw new Exception(string.Format(Resources.Strings.TaskSchedulerManagerErrorDelete, err));
            }
        }

        public static void DeleteSetPanelDimensionsTask() => DeleteTask(TASK_NAME);
        public static void DeleteStartGamepadKeyboardOnLogonTask() => DeleteTask(KEYBOARD_TASK_NAME);
    }
}