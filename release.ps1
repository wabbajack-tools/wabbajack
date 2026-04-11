$ErrorActionPreference = 'Stop'

$repoRoot     = $PSScriptRoot
$codeSignDir  = if ($env:CODE_SIGN_DIR)  { $env:CODE_SIGN_DIR }  else { "c:\oss\CodeSignTool" }
$publishDir   = if ($env:PUBLISH_DIR)    { $env:PUBLISH_DIR }    else { "c:\tmp\publish-wj" }
$sevenZip     = if ($env:SEVEN_ZIP_PATH) { $env:SEVEN_ZIP_PATH } else { "c:\Program Files\7-Zip\7z.exe" }

$nexusApiBase     = "https://api.nexusmods.com/v3"
$nexusGameDomain  = "site"
$nexusModId       = 403
# Nexus v3 file update group IDs
$nexusMainGroupId     = "1328371"   # Wabbajack.zip (MAIN - launcher)
$nexusOptionalGroupId = "1328362"   # VERSION.zip (OPTIONAL - full release)

# --- Helpers ---

function Invoke-Step {
    param([string]$Description, [scriptblock]$Action)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host " $Description" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    & $Action
}

function Assert-ExitCode {
    param([string]$Step)
    if ($LASTEXITCODE -ne 0) {
        Write-Host "FAILED: $Step (exit code $LASTEXITCODE)" -ForegroundColor Red
        exit 1
    }
}

function Invoke-NexusApi {
    param(
        [string]$Method = "GET",
        [string]$Path,
        [object]$Body,
        [string]$ContentType = "application/json"
    )
    $headers = @{
        "apikey"     = $env:NEXUS_API_KEY
        "User-Agent" = "Wabbajack-Release-Script"
    }
    $params = @{
        Method      = $Method
        Uri         = "$nexusApiBase$Path"
        Headers     = $headers
        ContentType = $ContentType
    }
    if ($Body) {
        $params.Body = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 10 }
    }
    Invoke-RestMethod @params
}

# --- Step 1: Changelog version check ---

Invoke-Step "Reading changelog version" {
    $changelogPath = Join-Path $repoRoot "CHANGELOG.md"
    $content = Get-Content $changelogPath -Raw

    if ($content -match '(?m)^#### Version - ([\d.]+) - TBD\s*$') {
        Write-Host "ERROR: CHANGELOG.md still has TBD as the date for version $($Matches[1])." -ForegroundColor Red
        Write-Host "Set the release date in CHANGELOG.md, commit, and push before running this script." -ForegroundColor Red
        exit 1
    }

    if ($content -notmatch '(?m)^#### Version - ([\d.]+) - ') {
        Write-Host "ERROR: No version header found in CHANGELOG.md" -ForegroundColor Red
        exit 1
    }

    $script:version = $Matches[1]
    Write-Host "Version: $script:version" -ForegroundColor Green
}

# --- Step 2: Build ---

Invoke-Step "Building projects" {
    if (Test-Path "$publishDir\app")      { Remove-Item "$publishDir\app" -Recurse -Force }
    if (Test-Path "$publishDir\launcher") { Remove-Item "$publishDir\launcher" -Recurse -Force }
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

    Push-Location $repoRoot

    dotnet clean
    Assert-ExitCode "dotnet clean"

    $commonArgs = @(
        "--framework", "net9.0-windows",
        "--runtime", "win-x64",
        "--configuration", "Release",
        "/p:IncludeNativeLibrariesForSelfExtract=true",
        "--self-contained",
        "/p:DebugType=embedded",
        "/p:VERSION=$script:version"
    )

    Write-Host "`nPublishing Wabbajack.App.Wpf..." -ForegroundColor Yellow
    dotnet publish Wabbajack.App.Wpf\Wabbajack.App.Wpf.csproj @commonArgs -o "$publishDir\app"
    Assert-ExitCode "dotnet publish Wabbajack.App.Wpf"

    Write-Host "`nPublishing Wabbajack.Launcher..." -ForegroundColor Yellow
    dotnet publish Wabbajack.Launcher\Wabbajack.Launcher.csproj @commonArgs -o "$publishDir\launcher" /p:PublishSingleFile=true
    Assert-ExitCode "dotnet publish Wabbajack.Launcher"

    Write-Host "`nPublishing Wabbajack.CLI..." -ForegroundColor Yellow
    dotnet publish Wabbajack.CLI\Wabbajack.CLI.csproj @commonArgs -o "$publishDir\app\cli"
    Assert-ExitCode "dotnet publish Wabbajack.CLI"

    Pop-Location
}

# --- Step 3: Code sign ---

Invoke-Step "Code signing (OTP will be prompted)" {
    $codeSignBat = Join-Path $codeSignDir "CodeSignTool.bat"
    $filesToSign = @(
        "$publishDir\app\Wabbajack.exe",
        "$publishDir\launcher\Wabbajack.exe",
        "$publishDir\app\cli\wabbajack-cli.exe"
    )

    $totpArg = if ($env:CODE_SIGN_TOTP_SECRET) { "-totp_secret=%CODE_SIGN_TOTP_SECRET%" } else { "" }

    Push-Location $codeSignDir
    foreach ($file in $filesToSign) {
        Write-Host "Signing $file ..." -ForegroundColor Yellow
        & cmd /c "CodeSignTool.bat sign -input_file_path `"$file`" -override=true -username=%CODE_SIGN_USER% -password=%CODE_SIGN_PASS% $totpArg"
        Assert-ExitCode "CodeSignTool sign $file"
    }
    Pop-Location

    # Verify each file has a valid Authenticode signature.
    # CodeSignTool.bat is known to swallow exit codes on failure, so we can't trust $LASTEXITCODE alone.
    foreach ($file in $filesToSign) {
        $sig = Get-AuthenticodeSignature -FilePath $file
        Write-Host "  $($file): $($sig.Status) - signed by $($sig.SignerCertificate.Subject)"
        if ($sig.Status -ne 'Valid') {
            Write-Host "ERROR: $file is not validly signed (status: $($sig.Status))" -ForegroundColor Red
            exit 1
        }
    }
    Write-Host "All executables have valid signatures." -ForegroundColor Green
}

# --- Step 4: Package ---

Invoke-Step "Packaging" {
    # Full release zip (app + cli)
    $releaseZip = "$publishDir\$($script:version).zip"
    & $sevenZip a $releaseZip "$publishDir\app\*"
    Assert-ExitCode "7z create $releaseZip"

    # Copy launcher standalone
    Copy-Item "$publishDir\launcher\Wabbajack.exe" "$publishDir\Wabbajack.exe" -Force

    # Launcher zip for Nexus (wrapping exe in zip to avoid AV)
    $launcherZip = "$publishDir\Wabbajack.zip"
    & $sevenZip a $launcherZip "$publishDir\Wabbajack.exe"
    Assert-ExitCode "7z create Wabbajack.zip"

    Write-Host "Created:" -ForegroundColor Green
    Write-Host "  $releaseZip"
    Write-Host "  $publishDir\Wabbajack.exe"
    Write-Host "  $launcherZip"
}

# --- Step 5: Extract changelog for release notes ---

function Get-ChangelogSection {
    param([string]$Version)
    $changelogPath = Join-Path $repoRoot "CHANGELOG.md"
    $content = Get-Content $changelogPath -Raw
    # Match from the version header to the next version header (or end of file)
    if ($content -match "(?ms)(#### Version - $([regex]::Escape($Version)) - .+?)(?=\r?\n#### Version - |\z)") {
        return $Matches[1].Trim()
    }
    return ""
}

$releaseNotes = Get-ChangelogSection -Version $script:version
if (-not $releaseNotes) {
    Write-Host "WARNING: Could not extract changelog section for $script:version" -ForegroundColor Yellow
}

# --- Step 6: GitHub release ---

Invoke-Step "Creating GitHub draft release" {
    Push-Location $repoRoot

    $notesFile = [System.IO.Path]::GetTempFileName()
    Set-Content $notesFile $releaseNotes -NoNewline

    gh release create $script:version `
        "$publishDir\$($script:version).zip" `
        "$publishDir\Wabbajack.exe" `
        --title $script:version `
        --notes-file $notesFile `
        --draft

    Assert-ExitCode "gh release create"
    Remove-Item $notesFile -ErrorAction SilentlyContinue

    Pop-Location
    Write-Host "Draft release created for $($script:version)" -ForegroundColor Green
}

# --- Step 7: Nexus Mods upload ---

function Upload-ToNexus {
    param(
        [string]$FilePath,
        [string]$GroupId,
        [string]$DisplayName,
        [string]$Description,
        [string]$Version,
        [string]$Category
    )

    $fileSize = (Get-Item $FilePath).Length
    $fileName = Split-Path $FilePath -Leaf
    Write-Host "Uploading $fileName ($([math]::Round($fileSize / 1MB, 1)) MB) to group $GroupId..." -ForegroundColor Yellow

    # 1. Create multipart upload
    $upload = Invoke-NexusApi -Method POST -Path "/uploads/multipart" -Body @{
        filename   = $fileName
        size_bytes = $fileSize
    }
    $uploadId    = $upload.data.id
    $partUrls    = $upload.data.parts_presigned_url
    $partSize    = $upload.data.parts_size
    $completeUrl = $upload.data.complete_presigned_url

    Write-Host "  Upload ID: $uploadId ($($partUrls.Count) parts, $([math]::Round($partSize / 1MB, 1)) MB each)"

    # 2. Upload parts
    $fileStream = [System.IO.File]::OpenRead($FilePath)
    $parts = @()
    try {
        for ($i = 0; $i -lt $partUrls.Count; $i++) {
            $offset = [int64]$i * [int64]$partSize
            $remaining = $fileSize - $offset
            $currentSize = [math]::Min($partSize, $remaining)
            $buffer = New-Object byte[] $currentSize
            $fileStream.Position = $offset
            [void]$fileStream.Read($buffer, 0, $currentSize)

            Write-Host "  Uploading part $($i + 1)/$($partUrls.Count) ($currentSize bytes)..."

            $response = Invoke-WebRequest -Uri $partUrls[$i] -Method PUT `
                -ContentType "application/octet-stream" `
                -Body $buffer `
                -UseBasicParsing

            $etag = $response.Headers["ETag"]
            if ($etag -is [array]) { $etag = $etag[0] }
            $etag = $etag -replace '"', ''
            $parts += @{ PartNumber = $i + 1; ETag = $etag }
        }
    }
    finally {
        $fileStream.Close()
    }

    # 3. Complete multipart upload
    $xmlParts = ($parts | ForEach-Object {
        "  <Part>`n    <PartNumber>$($_.PartNumber)</PartNumber>`n    <ETag>$($_.ETag)</ETag>`n  </Part>"
    }) -join "`n"
    $completeXml = "<CompleteMultipartUpload>`n$xmlParts`n</CompleteMultipartUpload>"

    Invoke-WebRequest -Uri $completeUrl -Method POST `
        -ContentType "application/xml" `
        -Body $completeXml `
        -UseBasicParsing | Out-Null

    Write-Host "  Multipart upload completed"

    # 4. Finalise
    Invoke-NexusApi -Method POST -Path "/uploads/$uploadId/finalise" | Out-Null
    Write-Host "  Finalised upload, waiting for processing..."

    # 5. Poll until available
    $maxAttempts = 60
    for ($attempt = 0; $attempt -lt $maxAttempts; $attempt++) {
        $status = Invoke-NexusApi -Method GET -Path "/uploads/$uploadId"
        $state = $status.data.state
        Write-Host "  State: $state"
        if ($state -eq "available") { break }
        $delay = [math]::Min(2000 * [math]::Pow(1.5, $attempt), 30000)
        Start-Sleep -Milliseconds $delay
    }
    if ($state -ne "available") {
        Write-Host "ERROR: Upload processing timed out" -ForegroundColor Red
        exit 1
    }

    # 6. Create version in update group
    $result = Invoke-NexusApi -Method POST -Path "/mod-file-update-groups/$GroupId/versions" -Body @{
        upload_id     = $uploadId
        name          = $DisplayName
        description   = $Description
        version       = $Version
        file_category = $Category
    }

    Write-Host "  File created: $($result.data.id)" -ForegroundColor Green
}

Invoke-Step "Uploading to Nexus Mods" {
    if (-not $env:NEXUS_API_KEY) {
        Write-Host "ERROR: NEXUS_API_KEY environment variable not set" -ForegroundColor Red
        exit 1
    }

    # MAIN: Wabbajack.zip (launcher)
    Upload-ToNexus `
        -FilePath "$publishDir\Wabbajack.zip" `
        -GroupId $nexusMainGroupId `
        -DisplayName "Wabbajack.zip" `
        -Description "Version - $($script:version) - download this file`n`nChangelog: https://github.com/wabbajack-tools/wabbajack/releases/tag/$($script:version)" `
        -Version $script:version `
        -Category "main"

    # OPTIONAL: VERSION.zip (full release)
    Upload-ToNexus `
        -FilePath "$publishDir\$($script:version).zip" `
        -GroupId $nexusOptionalGroupId `
        -DisplayName "$($script:version).zip" `
        -Description "Release files, ignore this unless you're sure you need it" `
        -Version $script:version `
        -Category "optional"

    Write-Host "`nNexus Mods uploads complete!" -ForegroundColor Green
}

# --- Step 8: Publish GitHub release ---

Invoke-Step "Publishing GitHub release" {
    Push-Location $repoRoot
    gh release edit $script:version --draft=false
    Assert-ExitCode "gh release edit --draft=false"
    Pop-Location
    Write-Host "Release $($script:version) is now live!" -ForegroundColor Green
}

# --- Step 9: Discord announcement ---

function Send-DiscordMessage {
    param([string]$Content)
    $payload = @{ content = $Content } | ConvertTo-Json -Depth 5 -Compress
    Invoke-RestMethod -Uri $env:DISCORD_RELEASE_WEBHOOK -Method POST `
        -ContentType "application/json" -Body $payload | Out-Null
}

Invoke-Step "Posting to Discord" {
    if (-not $env:DISCORD_RELEASE_WEBHOOK) {
        Write-Host "DISCORD_RELEASE_WEBHOOK not set, skipping" -ForegroundColor Yellow
        return
    }

    $header = @"
$($script:version) is released

Please download via the launcher or via the link on the website: https://www.wabbajack.org/
Or on the Nexus: https://www.nexusmods.com/site/mods/403
"@

    $fullMessage = "$header`n`n$releaseNotes"

    if ($fullMessage.Length -le 2000) {
        Send-DiscordMessage -Content $fullMessage
    }
    else {
        Write-Host "Message exceeds 2000 chars, splitting into header + changelog" -ForegroundColor Yellow
        Send-DiscordMessage -Content $header
        Send-DiscordMessage -Content $releaseNotes
    }

    Write-Host "Posted to Discord" -ForegroundColor Green
}

Invoke-Step "Release complete!" {
    Write-Host "`nGitHub: https://github.com/wabbajack-tools/wabbajack/releases/tag/$($script:version)" -ForegroundColor Green
    Write-Host "Nexus:  https://www.nexusmods.com/site/mods/403?tab=files" -ForegroundColor Green
}
