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
using System.Security.Cryptography.X509Certificates;
using System.ServiceProcess;
using System.Text.RegularExpressions;

namespace XboxFullscreenExperienceTool.Helpers
{
    /// <summary>
    /// 管理 PhysPanelDrv 驅動程式的安裝、移除與狀態檢查。
    /// </summary>
    public static class DriverManager
    {
        private const string DRIVER_SERVICE_NAME = "PhysPanelDrv";
        private static readonly string DriverFilesPath = Path.Combine(Application.StartupPath, "PhysPanelDrv");

        /// <summary>
        /// 檢查 Windows 測試簽章模式 (Test Signing Mode) 是否已啟用。
        /// </summary>
        /// <returns>如果已啟用，返回 true；否則返回 false。</returns>
        public static bool IsTestSigningEnabled()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "bcdedit.exe",
                        Arguments = "",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // 尋找 "testsigning" 項目並檢查其值是否為 "Yes"
                return Regex.IsMatch(output, @"testsigning\s+Yes", RegexOptions.IgnoreCase);
            }
            catch
            {
                return false; // 如果執行 bcdedit 失敗，則假設未啟用
            }
        }

        /// <summary>
        /// 檢查 PhysPanelDrv 驅動程式的服務是否存在且正在執行。
        /// </summary>
        /// <returns>如果服務正在執行，返回 true。</returns>
        public static bool IsDriverServiceRunning()
        {
            try
            {
                ServiceController sc = new ServiceController(DRIVER_SERVICE_NAME);
                return sc.Status == ServiceControllerStatus.Running;
            }
            catch (InvalidOperationException)
            {
                // 服務不存在
                return false;
            }
        }

        /// <summary>
        /// 執行驅動程式的完整安裝流程。
        /// </summary>
        /// <param name="logger">用於記錄進度的回呼函式。</param>
        public static bool InstallDriver(Action<string> logger)
        {
            try
            {
                // 步驟 1: 安裝憑證
                string certPath = Path.Combine(DriverFilesPath, "PhysPanelDrv.cer");
                if (!File.Exists(certPath))
                {
                    logger($"錯誤：找不到憑證檔案 '{certPath}'。");
                    return false;
                }

                logger("正在安裝驅動程式憑證...");
                X509Store store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);
                X509Certificate2 cert = new X509Certificate2(certPath);
                store.Add(cert);
                store.Close();
                logger("憑證安裝成功。");

                // 步驟 2: 使用 devcon.exe 安裝驅動程式
                string devconPath = Path.Combine(DriverFilesPath, "devcon.exe");
                string infPath = Path.Combine(DriverFilesPath, "PhysPanelDrv.inf");

                if (!File.Exists(devconPath) || !File.Exists(infPath))
                {
                    logger($"錯誤：找不到驅動程式檔案 '{devconPath}' 或 '{infPath}'。");
                    return false;
                }

                logger("正在使用 devcon 安裝驅動程式...");
                ExecuteProcess(devconPath, $"install \"{infPath}\" \"root\\{DRIVER_SERVICE_NAME}\"", logger);
                logger("驅動程式安裝指令已執行。");
                return true;
            }
            catch (Exception ex)
            {
                logger($"驅動程式安裝失敗：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 執行驅動程式的完整移除流程。
        /// </summary>
        /// <param name="logger">用於記錄進度的回呼函式。</param>
        public static bool UninstallDriver(Action<string> logger)
        {
            try
            {
                // 步驟 1: 使用 devcon.exe 移除裝置
                string devconPath = Path.Combine(DriverFilesPath, "devcon.exe");
                logger("正在移除驅動程式裝置...");
                ExecuteProcess(devconPath, $"remove \"root\\{DRIVER_SERVICE_NAME}\"", logger);

                // 步驟 2: 尋找驅動程式套件的 oem 編號
                logger("正在尋找驅動程式套件 (PhysPanelDrv.inf)...");
                string oemFile = FindOemInf(logger);
                if (string.IsNullOrEmpty(oemFile))
                {
                    logger("警告：找不到對應的 oem<編號>.inf 檔案，可能已被移除。");
                    return true; // 即使找不到也視為成功
                }

                // 步驟 3: 使用 pnputil 刪除驅動程式套件
                logger($"找到驅動程式套件 '{oemFile}'，正在刪除...");
                ExecuteProcess("pnputil.exe", $"/delete-driver {oemFile} /uninstall /force", logger);
                logger("驅動程式移除指令已執行。");
                return true;
            }
            catch (Exception ex)
            {
                logger($"驅動程式移除失敗：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 執行外部處理程序並記錄其輸出。
        /// </summary>
        private static void ExecuteProcess(string fileName, string arguments, Action<string> logger)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = DriverFilesPath
                }
            };
            process.OutputDataReceived += (sender, args) => { if (args.Data != null) logger(args.Data); };
            process.ErrorDataReceived += (sender, args) => { if (args.Data != null) logger($"錯誤: {args.Data}"); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
        }

        /// <summary>
        /// 使用 pnputil 尋找與 physpaneldrv.inf 關聯的 oem<編號>.inf 檔案。
        /// </summary>
        private static string FindOemInf(Action<string> logger)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "pnputil.exe",
                    Arguments = "/enum-drivers",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // 將輸出的文字塊拆分，並尋找包含 "physpaneldrv.inf" 的區塊
            var sections = output.Split(new[] { "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var section in sections)
            {
                if (section.Contains("physpaneldrv.inf", StringComparison.OrdinalIgnoreCase))
                {
                    // 從區塊中提取 oem<編號>.inf
                    var match = Regex.Match(section, @"oem\d+\.inf");
                    if (match.Success)
                    {
                        return match.Value;
                    }
                }
            }
            return string.Empty;
        }
    }
}