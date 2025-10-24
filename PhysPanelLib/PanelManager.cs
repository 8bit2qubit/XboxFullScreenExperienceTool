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

using System.Runtime.InteropServices;

namespace PhysPanelLib
{
    /// <summary>
    /// 定義顯示器的實體尺寸，單位為公釐 (mm)。
    /// 這個結構會被傳遞到非受控程式碼中，因此其記憶體佈局必須是循序的。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Dimensions
    {
        /// <summary>
        /// 顯示器的實體寬度 (公釐)。
        /// </summary>
        public uint WidthMm;

        /// <summary>
        /// 顯示器的實體高度 (公釐)。
        /// </summary>
        public uint HeightMm;
    }

    /// <summary>
    /// 提供與 Windows Notification Facility (WNF) 互動的核心功能，
    /// 用於讀取和設定內建顯示器的實體尺寸覆寫值。
    /// </summary>
    public static class PanelManager
    {
        //======================================================================
        // P/Invoke 常數與結構定義 (P/Invoke Constants & Structures)
        //======================================================================

        /// <summary>
        /// 代表一個 WNF 狀態名稱的內部結構。
        /// WNF State Name 是一個 64 位元的唯一識別碼，用於標識系統中的特定狀態資料。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct WnfStateName
        {
            public uint Data1;
            public uint Data2;
        }

        /// <summary>
        /// WNF 狀態識別碼，專門用於儲存 DirectX 內部顯示面板的實體尺寸。
        /// 這個值是 Windows 系統內部定義的常數。
        /// </summary>
        private static readonly WnfStateName WNF_DX_INTERNAL_PANEL_DIMENSIONS = new() { Data1 = 0xA3BC4875, Data2 = 0x41C61629 };

        /// <summary>
        /// 代表成功的 NTSTATUS 代碼 (0x00000000)。
        /// </summary>
        private const uint STATUS_SUCCESS = 0;

        //======================================================================
        // P/Invoke 委派與函式宣告 (P/Invoke Delegates & Function Imports)
        //======================================================================

        /// <summary>
        /// 定義 RtlQueryWnfStateData 所需的回呼函式簽章。
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate uint WnfUserCallback(
            WnfStateName stateName, uint changeStamp, IntPtr typeId,
            IntPtr callbackContext, IntPtr buffer, uint bufferSize);

        /// <summary>
        /// 將回呼委派的實例靜態持有，防止其在 P/Invoke 呼叫期間被記憶體回收機制 (GC) 意外回收。
        /// 這是與非受控回呼互動時的關鍵步驟。
        /// </summary>
        private static readonly WnfUserCallback QueryCallbackInstance = QueryCallback;

        /// <summary>
        /// 從 ntdll.dll 匯入 RtlQueryWnfStateData 函式，用於查詢 WNF 狀態資料。
        /// </summary>
        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern uint RtlQueryWnfStateData(
            out uint changeStamp, WnfStateName stateName, WnfUserCallback callback,
            IntPtr callbackContext, IntPtr typeId);

        /// <summary>
        /// 從 ntdll.dll 匯入 NtUpdateWnfStateData 函式，用於發佈 (寫入) WNF 狀態資料。
        /// </summary>
        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern uint NtUpdateWnfStateData(
            ref WnfStateName StateName,
            [In] ref ulong Buffer,
            uint Length,
            IntPtr TypeId,
            IntPtr ExplicitScope,
            uint MatchingChangeStamp,
            bool CheckStamp);

        //======================================================================
        // 公開 API 方法 (Public API Methods)
        //======================================================================

        /// <summary>
        /// 取得目前為內建顯示器設定的實體尺寸覆寫值。
        /// </summary>
        /// <returns>
        /// 一個元組 (tuple)，包含：
        /// <c>Success</c> (bool) - 操作是否成功。
        /// <c>Dims</c> (Dimensions) - 包含顯示器尺寸的結構。若操作失敗，其值未定義。
        /// </returns>
        public static (bool Success, Dimensions Dims) GetDisplaySize()
        {
            var output = new Dimensions();
            uint status;

            // 使用 GCHandle 來固定 managed 物件，取得其穩定指標，這是比 unsafe 更推薦的方式。
            GCHandle handle = GCHandle.Alloc(output, GCHandleType.Pinned);
            try
            {
                status = RtlQueryWnfStateData(
                    out _,
                    WNF_DX_INTERNAL_PANEL_DIMENSIONS,
                    QueryCallback, // 直接傳遞靜態方法
                    handle.AddrOfPinnedObject(),
                    IntPtr.Zero);
            }
            finally
            {
                // 從 GCHandle 取回可能已更新的物件
                if (handle.Target != null)
                {
                    output = (Dimensions)handle.Target;
                }
                handle.Free();
            }

            return (status == STATUS_SUCCESS, output);
        }

        /// <summary>
        /// 設定內建顯示器的實體尺寸覆寫值。
        /// </summary>
        /// <param name="dims">包含新寬度和高度 (公釐) 的 Dimensions 結構。</param>
        /// <returns>回傳 NTSTATUS 狀態碼。0 代表成功，非 0 代表失敗。</returns>
        public static uint SetDisplaySize(Dimensions dims)
        {
            // 將兩個 32 位元的 uint (WidthMm 和 HeightMm) 封裝成一個 64 位元的 ulong。
            // WNF API 預期接收一個 ulong 作為資料緩衝區。
            // 高 32 位元儲存高度，低 32 位元儲存寬度。
            // |<--- Height (32 bits) --->|<--- Width (32 bits) --->|
            ulong dimensions = ((ulong)dims.HeightMm << 32) | dims.WidthMm;
            var wnf = WNF_DX_INTERNAL_PANEL_DIMENSIONS; // 建立區域變數以傳遞 ref

            // 呼叫更底層的 NtUpdateWnfStateData 並直接回傳其狀態碼
            return NtUpdateWnfStateData(
                ref wnf,
                ref dimensions,
                sizeof(ulong),
                IntPtr.Zero,
                IntPtr.Zero,
                0,
                false);
        }

        /// <summary>
        /// 直接觸發 Windows 觸控鍵盤的顯示/隱藏。
        /// 這是為 'startkeyboard' 命令提供的專用介面。
        /// </summary>
        public static void StartTouchKeyboard()
        {
            KeyboardManager.ShowKeyboard();
        }

        //======================================================================
        // 私有回呼方法 (Private Callback Method)
        //======================================================================

        /// <summary>
        /// 當 RtlQueryWnfStateData 成功取得 WNF 資料時，由 Windows 系統呼叫的回呼函式。
        /// 這個版本不使用 unsafe 關鍵字。
        /// </summary>
        private static uint QueryCallback(
            WnfStateName stateName, uint changeStamp, IntPtr typeId,
            IntPtr callbackContext, IntPtr buffer, uint bufferSize)
        {
            // 進行基本的驗證，確保收到的資料符合預期
            if (buffer != IntPtr.Zero && bufferSize == sizeof(ulong) && callbackContext != IntPtr.Zero)
            {
                try
                {
                    // 使用 Marshal 類別安全地從 native 指標讀取資料
                    ulong rawDimensions = (ulong)Marshal.ReadInt64(buffer);

                    // 寬度是 ulong 的低 32 位元 (透過遮罩 0xFFFFFFFF 取得)
                    // 高度是 ulong 的高 32 位元 (透過向右位移 32 位取得)
                    Dimensions dims = new()
                    {
                        WidthMm = (uint)(rawDimensions & 0xFFFFFFFF),
                        HeightMm = (uint)((rawDimensions >> 32) & 0xFFFFFFFF)
                    };

                    // 將解析後的 managed 結構寫回到 GCHandle 指向的 native 記憶體位置
                    Marshal.StructureToPtr(dims, callbackContext, false);
                }
                catch
                {
                    // 在回呼中保持靜默錯誤處理
                }
            }
            return STATUS_SUCCESS; // 總是回傳成功，以符合 WNF 回呼的預期行為
        }
    }
}