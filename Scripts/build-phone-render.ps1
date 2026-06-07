$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "build-phone.ps1") `
    -ApiBaseUrl "https://plants-xwek.onrender.com" `
    -Configuration Debug
