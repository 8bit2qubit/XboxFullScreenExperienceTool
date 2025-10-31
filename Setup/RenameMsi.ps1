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
    自動將建置 (Build) 產生的 .msi 安裝檔重新命名。

.DESCRIPTION
    此腳本的目的是自動化安裝檔的命名流程，
    確保檔名能同時反映「版本號」與「建置模式」。

    它會自動執行以下操作：
    1. 讀取 .vdproj 專案檔，取得 "ProductVersion" (例如 0.0.11)。
    2. 讀取主程式 .csproj 檔案，檢查 <SelfContained> 標籤 (true/false)。
    3. 根據建置模式決定後綴：
       - true (獨立部署): 命名為 "-Full" (完整版)
       - false (框架相依): 命名為 "-Lite" (輕量版)
    4. 尋找最新產生的 .msi 檔案。
    5. 將其重新命名 (例如：Setup-0.0.11-Full.msi)。
    
    不需要任何參數，腳本會自動尋找所有需要的檔案。
#>
try {
    Write-Host "--- PowerShell 自動重新命名腳本已啟動 (含建置模式偵測) ---"

    # --- 1. 讀取版本號 ---
    
    # $PSScriptRoot 是 PowerShell 的特殊變數，代表此腳本所在的資料夾
    # 假設此腳本與 .vdproj 檔案放在同一個 'Setup' 目錄中
    $projectDir = $PSScriptRoot
    Write-Host "偵測到 Setup 專案目錄: $projectDir"

    # 在目錄中尋找 .vdproj (安裝專案檔)
    $vdprojFile = Get-ChildItem -Path $projectDir -Filter "*.vdproj" | Select-Object -First 1
    if (-not $vdprojFile) {
        Write-Host "錯誤: 在 '$projectDir' 中找不到 .vdproj 專案檔。"
        exit 1
    }
    Write-Host "找到專案檔: $($vdprojFile.FullName)"

    # 讀取包含 "ProductVersion" 的那一行
    $versionLine = Get-Content $vdprojFile.FullName | Select-String -Pattern '"ProductVersion"'
    if (-not $versionLine) {
        Write-Host "錯誤: 在專案檔中找不到 ProductVersion 這一行。"
        exit 1
    }

    # 使用正規表示式 (Regex) 擷取 1.2.3 這種格式的版本號
    $version = ""
    if ($versionLine -match '(\d+\.\d+\.\d+)') {
        $version = $matches[1]
        Write-Host "找到版本號: $version"
    } else {
        Write-Host "錯誤: 無法從這一行解析出版本號: $versionLine"
        exit 1
    }

    # --- 2. 讀取 SelfContained 狀態 (建置模式) ---

    # 定義主程式 .csproj 檔案的相對路徑
    $csprojPath = Join-Path -Path $projectDir -ChildPath "..\XboxFullScreenExperienceTool\XboxFullScreenExperienceTool.csproj"
    $csprojPath = [System.IO.Path]::GetFullPath($csprojPath) # 轉換為絕對路徑

    if (-not (Test-Path $csprojPath)) {
        Write-Host "錯誤: 找不到 .csproj 檔案於: $csprojPath"
        exit 1
    }

    $isSelfContained = $false
    try {
        # 嘗試將 .csproj 檔案作為 XML 讀取
        $xml = [xml](Get-Content $csprojPath -Encoding UTF8)
        
        # 使用 XPath 尋找 <SelfContained> 節點 (local-name() 可忽略命名空間)
        $xpathNode = '//*[local-name()="SelfContained"]'
        $node = $xml.SelectSingleNode($xpathNode)
        
        # 檢查節點值是否為 'true'
        if ($node -and $node.InnerText.ToLower() -eq 'true') {
            $isSelfContained = $true
        }
    } catch {
        # 這是後備機制，如果 .csproj 檔案格式錯誤或無法讀取
        Write-Host "警告: 讀取 .csproj 檔案失敗: $($_.Exception.Message)"
        Write-Host "將假設為 '輕量版' (false) 模式繼續。"
    }

    # --- 3. 決定檔案後綴 ---
    
    # 根據 SelfContained 的狀態，決定要附加到檔名的後綴
    $versionSuffix = ""
    if ($isSelfContained) {
        # "完整版": 包含 .NET 執行階段，檔案較大，但使用者無需額外安裝
        $versionSuffix = "-Full" 
        Write-Host "偵測到「完整版」模式，將使用 '$versionSuffix' 後綴。"
    } else {
        # "輕量版": 檔案較小，但需要使用者電腦上已安裝 .NET 執行階段
        $versionSuffix = "-Lite" 
        Write-Host "偵測到「輕量版」模式，將使用 '$versionSuffix' 後綴。"
    }

    # --- 4. 尋找並重新命名 MSI ---

    # 遞迴搜尋所有子資料夾 (例如 Release, Debug)
    # 並依建立時間排序，找到最新的 .msi 檔案
    Write-Host "正在搜尋最新的 .msi 檔案..."
    $latestMsi = Get-ChildItem -Path $projectDir -Recurse -Filter "*.msi" | Sort-Object CreationTime -Descending | Select-Object -First 1
    
    if (-not $latestMsi) {
        Write-Host "錯誤: 找不到任何 .msi 檔案可以重新命名。"
        exit 1
    }
    Write-Host "找到要重新命名的最新 MSI: $($latestMsi.FullName)"

    # 執行重新命名操作
    $baseName = $latestMsi.BaseName
    $extension = $latestMsi.Extension
    
    # 組合出新的檔名 (例如: Setup-0.0.11-Full.msi)
    $newName = "$baseName-$version$versionSuffix$extension"

    Write-Host "正在將 '$($latestMsi.Name)' 重新命名為 '$newName'"
    Rename-Item -Path $latestMsi.FullName -NewName $newName

    # $? 變數會儲存上一道命令的執行狀態 (true 表示成功)
    if ($?) {
        Write-Host "--- 腳本成功執行完畢 ---"
        exit 0
    } else {
        Write-Host "錯誤: 重新命名檔案失敗。"
        exit 1
    }
}
catch {
    # 捕獲上述 try 區塊中任何未處理的錯誤
    Write-Host "腳本執行時發生未預期的錯誤:"
    Write-Host $_.Exception.Message
    exit 1
}