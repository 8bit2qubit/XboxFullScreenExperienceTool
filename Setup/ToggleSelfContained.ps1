# Xbox Full Screen Experience Tool
# Copyright (C) 2025 8bit2qubit

# This program is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.

# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.

# You should have received a copy of the GNU General Public License
# along with this program.  If not, see <https:#www.gnu.org/licenses/>.

<#
.SYNOPSIS
    切換主專案 (XboxFullScreenExperienceTool.csproj) 的 <SelfContained> 狀態。

.DESCRIPTION
    此腳本用於在「獨立部署 (SelfContained)」和「框架相依 (FrameworkDependent)」
    兩種模式之間快速切換。

    腳本會自動偵測 <SelfContained> 標籤的值 (true/false) 並將其反轉：
    - 如果為 true (獨立部署)，則切換為 false (框架相依)。
    - 如果為 false 或不存在，則切換為 true (獨立部署)。
    
    此腳本被設計為能安全地修改 .csproj 檔案，並具有以下特性：
    - 完整保留 .csproj 原始的 XML 排版、縮排和空白。
    - 儲存時不會在檔案開頭添加 <?xml ...?> 宣告。
    - 確保檔案以 .csproj 標準的 "UTF-8 with BOM" 格式儲存。
#>

# --- 設定 ---

# 1. 取得此腳本所在的目錄 (e.g., ...\Setup)
$ScriptDir = Split-Path -Path $MyInvocation.MyCommand.Definition -Parent

# 2. 定義目標 .csproj 檔案的路徑 (相對於此腳本，往上一層再進入專案資料夾)
$ProjectFilePath = Join-Path -Path $ScriptDir -ChildPath "..\XboxFullScreenExperienceTool\XboxFullScreenExperienceTool.csproj"
# 將相對路徑轉換為絕對路徑，以便在錯誤訊息中顯示
$ProjectFilePath = [System.IO.Path]::GetFullPath($ProjectFilePath)

# --- 主要邏輯 ---

# 檢查檔案是否存在
if (-not (Test-Path $ProjectFilePath)) {
    Write-Error "錯誤：在 $ProjectFilePath 找不到目標 .csproj 檔案。"
    Write-Error "請確認此腳本是放在 'Setup' 資料夾中。"
    return
}

Write-Host "正在讀取: $ProjectFilePath (並保留排版)"

try {
    # 1. 建立一個新的 XML 物件
    $xml = New-Object System.Xml.XmlDocument
    
    # 2. (保留排版的關鍵) 告訴 XML 剖析器要保留所有空白字元
    $xml.PreserveWhitespace = $true
    
    # 3. 從檔案路徑載入 XML 內容
    $xml.Load($ProjectFilePath)
    
    # 4. 尋找第一個 PropertyGroup 節點
    #    (使用 local-name() 可忽略 XML 命名空間，增加相容性)
    #    (使用單引號包住 XPath，避免 PowerShell 剖析 '()' 時出錯)
    $xpathPropGroup = '//*[local-name()="Project"]/*[local-name()="PropertyGroup"]'
    $propGroup = $xml.SelectSingleNode($xpathPropGroup)

    if (!$propGroup) {
        Write-Error "在 $ProjectFilePath 中找不到 <PropertyGroup>。"
        return
    }

    # 5. 尋找 SelfContained 節點
    $xpathNode = '//*[local-name()="SelfContained"]'
    $node = $propGroup.SelectSingleNode($xpathNode)

    # 檢查 <SelfContained> 目前的值是否為 true
    $isCurrentlyTrue = $false
    if ($node -and $node.InnerText.ToLower() -eq 'true') {
        $isCurrentlyTrue = $true
    }

    # 6. 執行切換
    if ($isCurrentlyTrue) {
        # --- 切換到 False (框架相依) ---
        $node.InnerText = 'false'
        Write-Host ">> 成功切換為: false (框架相依模式)" -ForegroundColor Yellow
    } else {
        # --- 切換到 True (獨立部署) ---
        if (!$node) {
            # 如果節點不存在，則建立它
            Write-Host "  (未找到 <SelfContained> 標籤，正在建立...)"
            
            # 建立新節點時，也必須手動加入前後的空白 (換行和縮排)
            # 這樣才能維持 XML 的排版
            $indent = $xml.CreateWhitespace("    ") # 假設使用 4 個空格縮排
            $newline = $xml.CreateWhitespace("`r`n") # Windows 換行
            
            $newNode = $xml.CreateElement("SelfContained", $xml.DocumentElement.NamespaceURI)
            $newNode.InnerText = 'true'
            
            # 插入: 換行 -> 縮排 -> 新節點
            # (這會將新節點加在 <PropertyGroup> 的最下方)
            $propGroup.AppendChild($newline)
            $propGroup.AppendChild($indent)
            $propGroup.AppendChild($newNode)
            
        } else {
            # 節點已存在，直接修改
            $node.InnerText = 'true'
        }
        Write-Host ">> 成功切換為: true (獨立部署模式)" -ForegroundColor Green
    }

    # 7. 儲存變更
    
    # 存檔時，只儲存 "DocumentElement" (根節點) 的 OuterXml
    # 這樣可以避免 .Save() 方法自動添加 <?xml ...?> 宣告
    
    # 指定 $true 代表 "要包含 BOM" (Byte Order Mark)
    # 這是 .csproj 檔案的標準格式
    $encoding = New-Object System.Text.UTF8Encoding($true) 
    
    # 將根節點 (及其所有子節點) 的 XML 內容寫回檔案
    [System.IO.File]::WriteAllText($ProjectFilePath, $xml.DocumentElement.OuterXml, $encoding)
    
    Write-Host "已成功儲存檔案 (排版已保留，已儲存為 UTF-8 with BOM)。"

} catch {
    Write-Error "處理 $ProjectFilePath 時發生錯誤: $_"
}