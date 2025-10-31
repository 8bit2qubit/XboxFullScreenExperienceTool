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
    自動將建置 (Build) 產生的 .msi 安裝檔重新命名，在檔名中加入版本號。

.DESCRIPTION
    此腳本會自動執行以下操作：
    1. 尋找此腳本所在目錄中的 .vdproj 專案檔。
    2. 從 .vdproj 檔案中讀取 "ProductVersion" (例如 1.2.3)。
    3. 尋找所有子資料夾中最新產生的 .msi 檔案。
    4. 將 .msi 檔案重新命名 (例如：從 `Setup.msi` 改為 `Setup-1.2.3.msi`)。
    
    不需要任何參數，腳本會自動尋找所有需要的檔案。
#>

try {
    Write-Host "--- PowerShell 自動重新命名腳本已啟動 ---"

    # 腳本的所在位置即為專案目錄 ($PSScriptRoot 是 PowerShell 的特殊變數，代表腳本所在的資料夾)
    $projectDir = $PSScriptRoot
    Write-Host "偵測到專案目錄: $projectDir"

    # 在專案目錄中尋找 .vdproj 檔案
    $projectFile = Get-ChildItem -Path $projectDir -Filter "*.vdproj" | Select-Object -First 1
    if (-not $projectFile) {
        Write-Host "錯誤: 在 '$projectDir' 中找不到 .vdproj 專案檔。"
        exit 1
    }
    Write-Host "找到專案檔: $($projectFile.FullName)"

    # 從 .vdproj 檔案中讀取版本號
    $versionLine = Get-Content $projectFile.FullName | Select-String -Pattern '"ProductVersion"'
    if (-not $versionLine) {
        Write-Host "錯誤: 在專案檔中找不到 ProductVersion 這一行。"
        exit 1
    }

    if ($versionLine -match '(\d+\.\d+\.\d+)') {
        $version = $matches[1]
        Write-Host "找到版本號: $version"
    } else {
        Write-Host "錯誤: 無法從這一行解析出版本號: $versionLine"
        exit 1
    }

    # 在所有子資料夾中，尋找最新建置的 .msi 檔案
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
    $newName = "$baseName-$version$extension"

    Write-Host "正在將 '$($latestMsi.Name)' 重新命名為 '$newName'"
    Rename-Item -Path $latestMsi.FullName -NewName $newName

    if ($?) {
        Write-Host "--- 腳本成功執行完畢 ---"
        exit 0
    } else {
        Write-Host "錯誤: 重新命名檔案失敗。"
        exit 1
    }
}
catch {
    Write-Host "腳本執行時發生未預期的錯誤:"
    Write-Host $_.Exception.Message
    exit 1
}