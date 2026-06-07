param(
    [Parameter(Mandatory = $true)]
    [string]$ApiBaseUrl,

    [string]$AppKey,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot

if (-not $ApiBaseUrl.StartsWith("https://", [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "A physical-phone build requires an HTTPS API URL."
}

if ([string]::IsNullOrWhiteSpace($AppKey)) {
    $secureAppKey = Read-Host "Enter PLANTS_APP_KEY from Render" -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureAppKey)
    try {
        $AppKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

if ([string]::IsNullOrWhiteSpace($AppKey)) {
    throw "PLANTS_APP_KEY is required."
}

$normalizedApiBaseUrl = $ApiBaseUrl.TrimEnd('/')

Push-Location $projectRoot
try {
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot ".dotnet"
    $env:APPDATA = Join-Path $projectRoot ".appdata"
    New-Item -ItemType Directory -Force -Path $env:DOTNET_CLI_HOME, $env:APPDATA | Out-Null

    & dotnet restore .\Plants.csproj --configfile .\NuGet.Config
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE."
    }

    $publishArgs = @(
        "build",
        ".\Plants.csproj",
        "--no-restore",
        "-f",
        "net10.0-android",
        "-c",
        $Configuration,
        "-p:AndroidPackageFormats=apk",
        "-p:PlantsApiBaseUrl=$normalizedApiBaseUrl",
        "-p:PlantsAppKey=$AppKey"
    )

    & dotnet @publishArgs

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }

    $apk = Get-ChildItem `
        -Path ".\bin\$Configuration\net10.0-android" `
        -Filter "*.apk" `
        | Sort-Object LastWriteTime -Descending `
        | Select-Object -First 1

    if ($null -eq $apk) {
        throw "APK was not found after building."
    }

    Write-Host "APK ready: $($apk.FullName)"
}
finally {
    Pop-Location
}
