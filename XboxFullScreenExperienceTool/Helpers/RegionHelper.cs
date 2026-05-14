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
using System.Globalization;
using System.Runtime.InteropServices;

namespace XboxFullScreenExperienceTool.Helpers
{
    /// <summary>
    /// 提供用於偵測系統區域 (Region) 的靜態輔助方法。
    /// 用於判斷目前 Windows 是否位於 EU/EEA 區域，以便在 Native FSE 路徑上套用螢幕尺寸覆寫繞過。
    /// </summary>
    public static class RegionHelper
    {
        // 透過 P/Invoke 從 kernel32.dll 匯入 GetUserGeoID 函式
        [DllImport("kernel32.dll")]
        private static extern int GetUserGeoID(int GeoClass);

        /// <summary>
        /// GetUserGeoID 的參數：查詢使用者地理位置 (國家層級)。
        /// </summary>
        private const int GEOCLASS_NATION = 16;

        /// <summary>
        /// Windows 內部 DeviceRegion 儲存位置 (REG_DWORD 格式)。
        /// </summary>
        private const string DEVICE_REGION_KEY = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Control Panel\DeviceRegion";
        private const string DEVICE_REGION_VALUE = "DeviceRegion";

        /// <summary>
        /// 表示無法判定區域時的 GeoId 預設值。
        /// </summary>
        private const int UNKNOWN_GEO_ID = 0;

        /// <summary>
        /// EU27 + EEA-4 GeoId 名單。
        /// 不含英國 GB(242)：脫歐後不受 DMA 管轄。
        /// </summary>
        private static readonly HashSet<int> EuEeaGeoIds = new()
        {
            // EU27
            14,   // Austria
            21,   // Belgium
            35,   // Bulgaria
            59,   // Cyprus
            61,   // Denmark
            68,   // Ireland
            70,   // Estonia
            75,   // Czech Republic
            77,   // Finland
            84,   // France
            94,   // Germany
            98,   // Greece
            108,  // Croatia
            109,  // Hungary
            118,  // Italy
            140,  // Latvia
            141,  // Lithuania
            143,  // Slovakia
            147,  // Luxembourg
            163,  // Malta
            176,  // Netherlands
            191,  // Poland
            193,  // Portugal
            200,  // Romania
            212,  // Slovenia
            217,  // Spain
            221,  // Sweden

            // EEA-4 (非 EU 但受 DMA 等效規範)
            110,  // Iceland
            145,  // Liechtenstein
            177,  // Norway
            223,  // Switzerland
        };

        /// <summary>
        /// 取得目前系統區域的 GeoId。
        /// 優先讀 HKLM DeviceRegion，失敗則回退至 GetUserGeoID。
        /// </summary>
        /// <returns>目前區域的 GeoId，無法判定時回傳 0。</returns>
        public static int GetCurrentGeoId()
        {
            try
            {
                object? value = Registry.GetValue($@"HKEY_LOCAL_MACHINE\{DEVICE_REGION_KEY}", DEVICE_REGION_VALUE, null);
                if (value is int intValue && intValue > 0)
                {
                    return intValue;
                }
            }
            catch { /* 忽略錯誤，改走回退 */ }

            try
            {
                int geoId = GetUserGeoID(GEOCLASS_NATION);
                if (geoId > 0) return geoId;
            }
            catch { /* 忽略錯誤 */ }

            return UNKNOWN_GEO_ID;
        }

        /// <summary>
        /// 判斷目前系統是否位於 EU/EEA 區域。
        /// 無法判定時保守傳回 false (預設非 EU，不套用繞過)。
        /// </summary>
        public static bool IsEuRegion()
        {
            int geoId = GetCurrentGeoId();
            return geoId != UNKNOWN_GEO_ID && EuEeaGeoIds.Contains(geoId);
        }

        /// <summary>
        /// 取得目前區域的 ISO 兩位字母代碼 (例如 "DE"、"US")，供日誌顯示用。
        /// 透過 CultureInfo 列舉比對 LCID 對應的 GeoId。
        /// </summary>
        /// <returns>ISO 兩位字母代碼，無法判定時回傳 "Unknown"。</returns>
        public static string GetRegionDisplayCode()
        {
            int geoId = GetCurrentGeoId();
            if (geoId == UNKNOWN_GEO_ID) return "Unknown";

            try
            {
                // 列舉所有特定文化，找出 GeoId 對應的 RegionInfo
                foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
                {
                    try
                    {
                        var region = new RegionInfo(culture.Name);
                        if (region.GeoId == geoId)
                        {
                            return region.TwoLetterISORegionName;
                        }
                    }
                    catch { /* 忽略無效文化 */ }
                }
            }
            catch { /* 忽略整體列舉錯誤 */ }

            // 找不到對應 ISO code，回傳數字表示
            return $"GeoId-{geoId}";
        }
    }
}
