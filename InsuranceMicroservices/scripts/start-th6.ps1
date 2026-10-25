[CmdletBinding()]
param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$solutionRoot = Split-Path -Parent $PSScriptRoot
if (-not $NoBuild) {
    & dotnet build (Join-Path $solutionRoot 'TH6.slnf') --nologo
    if ($LASTEXITCODE -ne 0) { throw 'TH6 build failed.' }
}
if ([string]::IsNullOrWhiteSpace($env:AppSettings__Secret)) {
    $randomBytes = New-Object byte[] 48
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($randomBytes) } finally { $generator.Dispose() }
    $env:AppSettings__Secret = [Convert]::ToBase64String($randomBytes)
}
if ([System.Text.Encoding]::UTF8.GetByteCount($env:AppSettings__Secret) -lt 32) {
    throw 'AppSettings__Secret must contain at least 32 bytes.'
}
$logDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('th6-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $logDirectory | Out-Null
$savedUrls = $env:ASPNETCORE_URLS
$savedEnvironment = $env:ASPNETCORE_ENVIRONMENT
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    foreach ($service in @(
        @{ Name = 'AuthService'; Port = 6060 },
        @{ Name = 'ChatService'; Port = 4099 },
        @{ Name = 'DashboardService'; Port = 5035 },
        @{ Name = 'AgentPortalApiGateway'; Port = 8099 }
    )) {
        $projectDirectory = Join-Path $solutionRoot $service.Name
        $assembly = Join-Path $projectDirectory ('bin/Debug/net8.0/' + $service.Name + '.dll')
        if (-not (Test-Path -LiteralPath $assembly)) { throw "Missing assembly: $assembly" }
        $env:ASPNETCORE_URLS = 'http://localhost:' + $service.Port
        $process = Start-Process -FilePath 'dotnet' -ArgumentList @('"' + $assembly + '"') `
            -WorkingDirectory $projectDirectory -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput (Join-Path $logDirectory ($service.Name + '.out.log')) `
            -RedirectStandardError (Join-Path $logDirectory ($service.Name + '.err.log'))
        [pscustomobject]@{ Service = $service.Name; PID = $process.Id; URL = $env:ASPNETCORE_URLS; Logs = $logDirectory }
    }
}
finally {
    $env:ASPNETCORE_URLS = $savedUrls
    $env:ASPNETCORE_ENVIRONMENT = $savedEnvironment
}
