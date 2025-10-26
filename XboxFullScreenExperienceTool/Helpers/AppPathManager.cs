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