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
    切換主專案 (XboxFullScreenExperienceTool.csproj) 的 <IsExperimentalBuild> 狀態。

.DESCRIPTION
    此腳本用於在「實驗版 (Experimental)」和「正式版 (Stable)」兩種編譯模式之間快速切換。

    腳本會自動偵測 <IsExperimentalBuild> 標籤的值 (true/false) 並將其反轉：
    - 如果為 true (實驗版)，則切換為 false (正式版)。
    - 如果為 false 或不存在，則切換為 true (實驗版)。
    
    此腳本完整保留 .csproj 原始的 XML 排版、縮排和空白，並確保以 UTF-8 with BOM 格式儲存。
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

    # 5. 尋找 IsExperimentalBuild 節點
    $tagName = "IsExperimentalBuild"
    $xpathNode = "//*[local-name()='$tagName']"
    $node = $propGroup.SelectSingleNode($xpathNode)

    # 檢查目前的狀態
    $isCurrentlyTrue = $false
    if ($node -and $node.InnerText.ToLower() -eq 'true') {
        $isCurrentlyTrue = $true
    }

    # 6. 執行切換
    if ($isCurrentlyTrue) {
        # --- 切換到 False (正式版) ---
        $node.InnerText = 'false'
        Write-Host ">> 成功切換為: false (正式版/Stable Mode)" -ForegroundColor Yellow
    } else {
        # --- 切換到 True (實驗版) ---
        if (!$node) {
            # 如果節點不存在，則建立它
            Write-Host "  (未找到 <$tagName> 標籤，正在建立...)"
            
            # 建立新節點時，手動加入前後的空白 (換行和縮排)
            $indent = $xml.CreateWhitespace("    ") # 4 個空格縮排
            $newline = $xml.CreateWhitespace("`r`n") # Windows 換行
            
            $newNode = $xml.CreateElement($tagName, $xml.DocumentElement.NamespaceURI)
            $newNode.InnerText = 'true'
            
            # 插入: 換行 -> 縮排 -> 新節點
            $propGroup.AppendChild($newline)
            $propGroup.AppendChild($indent)
            $propGroup.AppendChild($newNode)
            
        } else {
            # 節點已存在，直接修改
            $node.InnerText = 'true'
        }
        Write-Host ">> 成功切換為: true (實驗版/Preview Mode - 將顯示 Git Hash)" -ForegroundColor Green
    }

    # 7. 儲存變更
    
    # 存檔時，只儲存 "DocumentElement" (根節點) 的 OuterXml 以避免添加 <?xml ...?>
    # 使用 UTF-8 with BOM
    $encoding = New-Object System.Text.UTF8Encoding($true) 
    
    [System.IO.File]::WriteAllText($ProjectFilePath, $xml.DocumentElement.OuterXml, $encoding)
    
    Write-Host "已成功儲存檔案。"

} catch {
    Write-Error "處理 $ProjectFilePath 時發生錯誤: $_"
}