$ErrorActionPreference = 'Stop'

$ProjectRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$CscPath = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

$SrcDir = Join-Path $ProjectRoot 'src'
$DistDir = Join-Path $ProjectRoot 'dist'
$IconPath = Join-Path $ProjectRoot 'assets\soundcloud.ico'
$ManifestPath = Join-Path $SrcDir 'app.manifest'

$WebView2RootDir = Join-Path $ProjectRoot 'lib\webview2'
$WebView2Net462Dir = Join-Path $WebView2RootDir 'lib\net462'
$WebView2CoreDll = Join-Path $WebView2Net462Dir 'Microsoft.Web.WebView2.Core.dll'
$WebView2WinFormsDll = Join-Path $WebView2Net462Dir 'Microsoft.Web.WebView2.WinForms.dll'
$WebView2LoaderDll = Join-Path $WebView2RootDir 'runtimes\win-x64\native\WebView2Loader.dll'
$OutputExe = Join-Path $DistDir 'SoundCloud.exe'

if ((-not (Test-Path -LiteralPath $WebView2CoreDll)) -or (-not (Test-Path -LiteralPath $WebView2WinFormsDll)) -or (-not (Test-Path -LiteralPath $WebView2LoaderDll))) {
    Write-Host "WebView2 SDK not found locally. Downloading Microsoft.Web.WebView2 NuGet package..."
    $LibDir = Join-Path $ProjectRoot 'lib'
    if (-not (Test-Path -LiteralPath $LibDir)) {
        New-Item -ItemType Directory -Path $LibDir -Force | Out-Null
    }
    $NupkgZip = Join-Path $LibDir 'Microsoft.Web.WebView2.zip'
    Invoke-WebRequest -Uri 'https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2' -OutFile $NupkgZip -UseBasicParsing
    Expand-Archive -LiteralPath $NupkgZip -DestinationPath $WebView2RootDir -Force
    Remove-Item -LiteralPath $NupkgZip -Force -ErrorAction SilentlyContinue
}

$RequiredPaths = @(
    $CscPath,
    $SrcDir,
    $IconPath,
    $ManifestPath,
    $WebView2CoreDll,
    $WebView2WinFormsDll,
    $WebView2LoaderDll
)

foreach ($path in $RequiredPaths) {
    if (-not (Test-Path -LiteralPath $path)) {
        Write-Error "Required path not found: $path"
        exit 1
    }
}

$SourceFiles = @(Get-ChildItem -LiteralPath $SrcDir -Filter '*.cs' -File | Sort-Object Name | ForEach-Object { $_.FullName })
if ($SourceFiles.Count -eq 0) {
    Write-Error "No C# source files found in $SrcDir"
    exit 1
}

if (-not (Test-Path -LiteralPath $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
}

$CscArgs = @(
    '/nologo'
    '/target:winexe'
    '/platform:x64'
    '/optimize+'
    "/out:$OutputExe"
    "/win32icon:$IconPath"
    "/win32manifest:$ManifestPath"
    "/res:$WebView2WinFormsDll,Microsoft.Web.WebView2.WinForms.dll"
    "/res:$WebView2CoreDll,Microsoft.Web.WebView2.Core.dll"
    "/res:$WebView2LoaderDll,WebView2Loader.dll"
    '/reference:System.dll'
    '/reference:System.Core.dll'
    '/reference:System.Drawing.dll'
    '/reference:System.Windows.Forms.dll'
    '/reference:System.Web.Extensions.dll'
    "/reference:$WebView2CoreDll"
    "/reference:$WebView2WinFormsDll"
) + $SourceFiles

Write-Host "Building SoundCloudDesktop with csc.exe..."
& $CscPath @CscArgs
$exitCode = $LASTEXITCODE
if ($exitCode -ne 0) {
    Write-Error "csc.exe failed with exit code $exitCode."
    exit $exitCode
}

Copy-Item -LiteralPath $WebView2CoreDll -Destination $DistDir -Force
Copy-Item -LiteralPath $WebView2WinFormsDll -Destination $DistDir -Force
Copy-Item -LiteralPath $WebView2LoaderDll -Destination $DistDir -Force

Write-Host "Build succeeded: $OutputExe"
