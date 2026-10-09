# Nabla Home - local Android APK build (no EAS account needed).
# Run from EVHomeApp:  .\build-apk.ps1            -> build\NablaHome-<version>.apk
#
# Requires: Android SDK (ANDROID_HOME) with platform 36, build-tools 36, NDK 27.1;
# JDK 17+ (Android Studio's bundled JDK is picked first).
# The release build is signed with the debug keystore from the Expo template — fine for
# sideloading test builds; a real release keystore is needed before publishing to a store.

# "Continue", not "Stop": Windows PowerShell 5.1 turns any stderr line of a native tool
# (e.g. an expo prebuild warning) into a terminating NativeCommandError. Success is judged
# by $LASTEXITCODE after each native call instead.
$ErrorActionPreference = "Continue"
$root   = $PSScriptRoot
$outDir = Join-Path $root "build"

# ── JDK ───────────────────────────────────────────────────────────────────────
$jdk = @("C:\Program Files\Android\openjdk", "C:\Program Files\Microsoft", "C:\Program Files\Eclipse Adoptium") |
    Where-Object { Test-Path $_ } |
    ForEach-Object { Get-ChildItem $_ -Directory | Where-Object Name -match '^jdk-(1[7-9]|2\d)' } |
    Sort-Object Name -Descending | Select-Object -First 1
if (-not $jdk) { throw "No JDK 17+ found. Install Android Studio." }
$env:JAVA_HOME = $jdk.FullName
$env:PATH      = "$($env:JAVA_HOME)\bin;$env:PATH"
if (-not $env:ANDROID_HOME) { $env:ANDROID_HOME = Join-Path $env:LOCALAPPDATA "Android\Sdk" }
Write-Host "JDK:          $($env:JAVA_HOME)"
Write-Host "ANDROID_HOME: $($env:ANDROID_HOME)"

# ── Native project (Continuous Native Generation: android/ is generated, not in git) ──
Push-Location $root
try {
    npx expo prebuild --platform android --no-install
    if ($LASTEXITCODE -ne 0) { throw "expo prebuild failed ($LASTEXITCODE)" }

    Push-Location (Join-Path $root "android")
    try {
        .\gradlew.bat assembleRelease --no-daemon
        if ($LASTEXITCODE -ne 0) { throw "gradle assembleRelease failed ($LASTEXITCODE)" }
    } finally { Pop-Location }

    $version = (Get-Content (Join-Path $root "app.json") -Raw | ConvertFrom-Json).expo.version
    $apk     = Join-Path $root "android\app\build\outputs\apk\release\app-release.apk"
    New-Item -ItemType Directory -Force $outDir | Out-Null
    $target  = Join-Path $outDir "NablaHome-$version.apk"
    Copy-Item $apk $target -Force
    Write-Host ""
    Write-Host ("APK: {0} ({1:N1} MB)" -f $target, ((Get-Item $target).Length / 1MB)) -ForegroundColor Green
} finally { Pop-Location }
