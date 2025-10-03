# No arguments needed. This script finds the files itself.
try {
    Write-Host "--- PowerShell Self-Discovery Rename Script Started ---"

    # The script's location is the Project Directory ($PSScriptRoot is a special variable for the script's own folder)
    $projectDir = $PSScriptRoot
    Write-Host "Detected Project Directory: $projectDir"

    # Find the .vdproj file in the directory
    $projectFile = Get-ChildItem -Path $projectDir -Filter "*.vdproj" | Select-Object -First 1
    if (-not $projectFile) {
        Write-Host "ERROR: Could not find a .vdproj file in '$projectDir'"
        exit 1
    }
    Write-Host "Found Project File: $($projectFile.FullName)"

    # Read the version from the project file
    $versionLine = Get-Content $projectFile.FullName | Select-String -Pattern '"ProductVersion"'
    if (-not $versionLine) {
        Write-Host "ERROR: Could not find the ProductVersion line in the project file."
        exit 1
    }

    if ($versionLine -match '(\d+\.\d+\.\d+)') {
        $version = $matches[1]
        Write-Host "Found Version: $version"
    } else {
        Write-Host "ERROR: Could not parse the version number from the line: $versionLine"
        exit 1
    }

    # Find the most recently built .msi file in any subfolder
    Write-Host "Searching for the latest .msi file..."
    $latestMsi = Get-ChildItem -Path $projectDir -Recurse -Filter "*.msi" | Sort-Object CreationTime -Descending | Select-Object -First 1
    
    if (-not $latestMsi) {
        Write-Host "ERROR: Could not find any .msi file to rename."
        exit 1
    }
    Write-Host "Found latest MSI to rename: $($latestMsi.FullName)"

    # Perform the rename operation
    $baseName = $latestMsi.BaseName
    $extension = $latestMsi.Extension
    $newName = "$baseName-$version$extension"

    Write-Host "Renaming '$($latestMsi.Name)' to '$newName'"
    Rename-Item -Path $latestMsi.FullName -NewName $newName

    if ($?) {
        Write-Host "--- Script Finished Successfully ---"
        exit 0
    } else {
        Write-Host "ERROR: Failed to rename the file."
        exit 1
    }
}
catch {
    Write-Host "An unexpected error occurred in the script:"
    Write-Host $_.Exception.Message
    exit 1
}