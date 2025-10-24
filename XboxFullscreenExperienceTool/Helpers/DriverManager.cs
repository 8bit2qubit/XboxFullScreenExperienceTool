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
using System.Runtime.InteropServices;

namespace XboxFullScreenExperienceTool.Helpers
{
    /// <summary>
    /// 管理 PhysPanelDrv 驅動程式的安裝、移除與狀態檢查。
    /// </summary>
    public static class DriverManager
    {
        private const string DRIVER_SERVICE_NAME = "PhysPanelDrv";
        private static readonly string DriverFilesPath = Path.Combine(Application.StartupPath, "PhysPanelDrv");

        #region Win32 P/Invoke for NtQuerySystemInformation
        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtQuerySystemInformation(
            int SystemInformationClass,
            ref SYSTEM_CODE_INTEGRITY_INFORMATION SystemInformation,
            uint SystemInformationLength,
            out uint ReturnLength);

        /// <summary>
        /// NtQuerySystemInformation 的參數：查詢系統程式碼完整性資訊
        /// </summary>
        private const int SystemCodeIntegrityInformation = 103;

        /// <summary>
        /// CodeIntegrityOptions 的旗標：測試簽章已啟用
        /// </summary>
        private const uint CODEINTEGRITY_OPTION_TESTSIGN = 0x02;

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_CODE_INTEGRITY_INFORMATION
        {
            public uint Length;
            public uint CodeIntegrityOptions;
        }
        #endregion

        /// <summary>
        /// 檢查 Windows 測試簽章模式 (Test Signing Mode) 是否目前處於作用中狀態。
        /// 使用 ntdll.dll NtQuerySystemInformation 作為可靠的檢查。
        /// </summary>
        /// <returns>如果目前已啟用，返回 true；否則返回 false。</returns>
        public static bool IsTestSigningEnabled()
        {
            try
            {
                var codeIntegrityInfo = new SYSTEM_CODE_INTEGRITY_INFORMATION();
                uint structSize = (uint)Marshal.SizeOf(codeIntegrityInfo);
                codeIntegrityInfo.Length = structSize;

                uint returnLength;

                int status = NtQuerySystemInformation(
                    SystemCodeIntegrityInformation, // 103
                    ref codeIntegrityInfo,
                    structSize,
                    out returnLength
                );

                if (status == 0) // STATUS_SUCCESS
                {
                    // 檢查 TESTSIGN (0x02) 旗標是否被設定
                    return (codeIntegrityInfo.CodeIntegrityOptions & CODEINTEGRITY_OPTION_TESTSIGN) != 0;
                }
                else
                {
                    // API 呼叫失敗
                    return false;
                }
            }
            catch (Exception)
            {
                // P/Invoke 呼叫失敗
                return false;
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
                    logger(string.Format(Resources.Strings.ErrorCertificateFileNotFound, certPath));
                    return false;
                }

                logger(Resources.Strings.LogInstallingCertificate);
                X509Store store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadWrite);
                X509Certificate2 cert = new X509Certificate2(certPath);
                store.Add(cert);
                store.Close();
                logger(Resources.Strings.LogCertificateInstallSuccess);

                // 步驟 2: 使用 devcon.exe 安裝驅動程式
                string devconPath = Path.Combine(DriverFilesPath, "devcon.exe");
                string infPath = Path.Combine(DriverFilesPath, "PhysPanelDrv.inf");

                if (!File.Exists(devconPath) || !File.Exists(infPath))
                {
                    logger(string.Format(Resources.Strings.ErrorDriverFilesNotFound, devconPath, infPath));
                    return false;
                }

                logger(Resources.Strings.LogInstallingDriverWithDevcon);
                ExecuteProcess(devconPath, $"install \"{infPath}\" \"root\\{DRIVER_SERVICE_NAME}\"", logger);
                logger(Resources.Strings.LogDriverInstallCommandExecuted);
                return true;
            }
            catch (Exception ex)
            {
                logger(string.Format(Resources.Strings.LogErrorDriverInstallFailed, ex.Message));
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
                logger(Resources.Strings.LogRemovingDriverDevice);
                ExecuteProcess(devconPath, $"remove \"root\\{DRIVER_SERVICE_NAME}\"", logger);

                // 步驟 2: 尋找驅動程式套件的 oem 編號
                logger(Resources.Strings.LogFindingDriverPackage);
                string oemFile = FindOemInf(logger);
                if (string.IsNullOrEmpty(oemFile))
                {
                    logger(Resources.Strings.WarningOemInfNotFound);
                    return true; // 即使找不到也視為成功
                }

                // 步驟 3: 使用 pnputil 刪除驅動程式套件
                logger(string.Format(Resources.Strings.LogDeletingDriverPackage, oemFile));
                ExecuteProcess("pnputil.exe", $"/delete-driver {oemFile} /uninstall /force", logger);
                logger(Resources.Strings.LogDriverRemoveCommandExecuted);
                return true;
            }
            catch (Exception ex)
            {
                logger(string.Format(Resources.Strings.LogErrorDriverRemoveFailed, ex.Message));
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
            process.ErrorDataReceived += (sender, args) => { if (args.Data != null) logger(string.Format(Resources.Strings.LogErrorPrefix, args.Data)); };

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