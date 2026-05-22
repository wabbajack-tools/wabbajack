$ErrorActionPreference = 'Stop'

$repoRoot     = $PSScriptRoot
$codeSignDir  = if ($env:CODE_SIGN_DIR)  { $env:CODE_SIGN_DIR }  else { "c:\oss\CodeSignTool" }
$publishDir   = if ($env:PUBLISH_DIR)    { $env:PUBLISH_DIR }    else { "c:\tmp\publish-wj" }
$sevenZip     = if ($env:SEVEN_ZIP_PATH) { $env:SEVEN_ZIP_PATH } else { "c:\Program Files\7-Zip\7z.exe" }

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

    # Export for GitHub Actions workflow steps
    if ($env:GITHUB_OUTPUT) {
        "version=$($script:version)" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    }
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

Invoke-Step "Build & package complete" {
    Write-Host "`nReady for upload. Version: $($script:version)" -ForegroundColor Green
    Write-Host "  GitHub draft: https://github.com/wabbajack-tools/wabbajack/releases/tag/$($script:version)" -ForegroundColor Green
}
