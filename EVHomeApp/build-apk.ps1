# Nabla Home - local Android APK build (no EAS account needed).
# Run from EVHomeApp:  .\build-apk.ps1                 -> build\NablaHome-<version>.apk
#                      .\build-apk.ps1 -AllAbis        (also 32-bit / x86 devices; ~3x slower)
#
# Requires: Android SDK (ANDROID_HOME) with platform 36, build-tools 36, NDK 27.1;
# JDK 17+ (Android Studio's bundled JDK is picked first).
# The release build is signed with the debug keystore from the Expo template — fine for
# sideloading test builds; a real release keystore is needed before publishing to a store.
#
# Windows MAX_PATH: CMake/ninja embed the full project path in object-file paths and fail
# beyond 260 chars ("ninja: error: mkdir(...)"). A subst drive or junction does not help —
# Node resolves it back to the real path and React Native codegen then sees two roots.
# So the build runs from a physical mirror at a short path ($BuildRoot), synced with robocopy
# (incremental after the first run).

param(
    [switch]$AllAbis,
    [string]$BuildRoot = "D:\nb\app"
)

# "Continue", not "Stop": Windows PowerShell 5.1 turns any stderr line of a native tool
# (e.g. an expo prebuild warning) into a terminating NativeCommandError. Success is judged
# by $LASTEXITCODE after each native call instead.
$ErrorActionPreference = "Continue"
$root   = $PSScriptRoot
$outDir = Join-Path $root "build"

# ── JDK / SDK ─────────────────────────────────────────────────────────────────
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

# ── Mirror to the short path ──────────────────────────────────────────────────
Write-Host "Syncing $root -> $BuildRoot"
# /XD with FULL paths: a bare name (e.g. "build") would also skip every node_modules/**/build folder.
$skip = @("android", "build", ".expo", "dist", "web-build") | ForEach-Object { Join-Path $root $_ }
robocopy "$root" "$BuildRoot" /MIR /XD @skip /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }

Push-Location $BuildRoot
try {
    # --clean: android/ is generated (CNG, not in git) — regenerate from app.json every time.
    npx expo prebuild --platform android --no-install --clean
    if ($LASTEXITCODE -ne 0) { throw "expo prebuild failed ($LASTEXITCODE)" }

    $abis = if ($AllAbis) { "armeabi-v7a,arm64-v8a,x86,x86_64" } else { "arm64-v8a" }
    Push-Location (Join-Path $BuildRoot "android")
    try {
        .\gradlew.bat assembleRelease --no-daemon "-PreactNativeArchitectures=$abis"
        if ($LASTEXITCODE -ne 0) { throw "gradle assembleRelease failed ($LASTEXITCODE)" }
    } finally { Pop-Location }

    $version = (Get-Content (Join-Path $root "app.json") -Raw | ConvertFrom-Json).expo.version
    $apk     = Join-Path $BuildRoot "android\app\build\outputs\apk\release\app-release.apk"
    New-Item -ItemType Directory -Force $outDir | Out-Null
    $target  = Join-Path $outDir "NablaHome-$version.apk"
    Copy-Item $apk $target -Force
    Write-Host ""
    Write-Host ("APK: {0} ({1:N1} MB, {2})" -f $target, ((Get-Item $target).Length / 1MB), $abis) -ForegroundColor Green
} finally {
    Pop-Location
}
