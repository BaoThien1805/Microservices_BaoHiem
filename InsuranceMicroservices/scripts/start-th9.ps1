[CmdletBinding()]
param([switch]$NoBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$password = $env:TH9_POSTGRES_PASSWORD
if ($password -notmatch '^[A-Za-z0-9_\-]{12,}$') {
    throw 'Set TH9_POSTGRES_PASSWORD to at least 12 letters, digits, underscores or hyphens. Reuse the same password for an existing TH9 database volume.'
}

function Assert-ExternalSuccess([string]$Operation) {
    if ($LASTEXITCODE -ne 0) { throw "$Operation failed (exit $LASTEXITCODE)." }
}

function Wait-Http([string]$Uri, [System.Diagnostics.Process]$Process, [int]$ExpectedStatus = 200) {
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    do {
        if ($Process -and $Process.HasExited) { throw "Service exited while waiting for $Uri. See $logDirectory." }
        try {
            $response = Invoke-WebRequest -Uri $Uri -TimeoutSec 2 -UseBasicParsing
            if ([int]$response.StatusCode -eq $ExpectedStatus) { return }
            throw "Expected HTTP $ExpectedStatus, received $($response.StatusCode)"
        } catch {
            if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq $ExpectedStatus) { return }
            if ([DateTime]::UtcNow -ge $deadline) { throw "Endpoint $Uri is unavailable: $($_.Exception.Message)" }
            Start-Sleep -Milliseconds 500
        }
    } while ($true)
}

if (-not $NoBuild) {
    dotnet build (Join-Path $root 'InsuranceMicroservices.sln') --nologo
    Assert-ExternalSuccess 'Solution build'
}
docker compose -f (Join-Path $PSScriptRoot 'infra.yml') up -d eureka elasticsearch rabbitmq
Assert-ExternalSuccess 'Eureka, Elasticsearch and RabbitMQ startup'
docker compose -f (Join-Path $PSScriptRoot 'th9-postgres.yml') up -d --wait
Assert-ExternalSuccess 'PostgreSQL startup'
Wait-Http 'http://localhost:8761'
Wait-Http 'http://localhost:9200'
Wait-Http 'http://localhost:15672'

$logDirectory = Join-Path ([IO.Path]::GetTempPath()) ('th9-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $logDirectory | Out-Null
$services = @(
    @{ Name = 'AuthService'; Port = 6060; Health = '/health' },
    @{ Name = 'ProductService'; Port = 5030; Health = '/api/Products' },
    @{ Name = 'PricingService'; Port = 5041; Health = '/health' },
    @{ Name = 'PolicySearchService'; Port = 5065; Health = '/health' },
    @{ Name = 'ChatService'; Port = 4099; Health = '/health' },
    @{ Name = 'DashboardService'; Port = 5035; Health = '/health' },
    @{ Name = 'PaymentService'; Port = 5070; Health = '/api/Payment/accounts/POLICY_1' },
    @{ Name = 'PolicyService'; Port = 5050; Health = '/health' },
    @{ Name = 'AgentPortalApiGateway'; Port = 8099; Health = '/api/Products'; Expected = 401 }
)
$settings = @{
    ASPNETCORE_ENVIRONMENT = 'Development'
    AppSettings__Secret = if ($env:AppSettings__Secret) { $env:AppSettings__Secret } else {
        $bytes = New-Object byte[] 48
        $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
        [Convert]::ToBase64String($bytes)
    }
    ConnectionStrings__PgConnection = "Host=localhost;Port=5434;Database=lab_netmicro_payments;Username=lab_user;Password=$password"
    BackgroundJobs__HangfireConnectionStringName = "Host=localhost;Port=5434;Database=lab_netmicro_jobs;Username=lab_user;Password=$password"
    BackgroundJobs__InPaymentFileFolder = (Join-Path $logDirectory 'bank-statements')
    SeedData__Enabled = 'true'
    PricingServiceUri = 'http://localhost:5041/api/pricing'
}
New-Item -ItemType Directory -Path $settings.BackgroundJobs__InPaymentFileFolder | Out-Null
$previous = @{}
foreach ($key in @($settings.Keys) + @('ASPNETCORE_URLS', 'ConnectionStrings__DefaultConnection', 'Eureka__Instance__Port')) {
    $previous[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
}
$children = @()
try {
    foreach ($key in $settings.Keys) { [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process') }
    foreach ($service in $services) {
        $env:ConnectionStrings__DefaultConnection = if ($service.Name -eq 'PolicyService') {
            "Host=localhost;Port=5434;Database=lab_netmicro_policy;Username=lab_user;Password=$password"
        } else { $null }
        if (Get-NetTCPConnection -LocalPort $service.Port -State Listen -ErrorAction SilentlyContinue) {
            throw "Port $($service.Port) is already in use. Stop the existing service first."
        }
        $env:ASPNETCORE_URLS = "http://localhost:$($service.Port)"
        $env:Eureka__Instance__Port = [string]$service.Port
        $directory = Join-Path $root $service.Name
        $dll = Join-Path $directory "bin/Debug/net8.0/$($service.Name).dll"
        if (-not (Test-Path -LiteralPath $dll)) { throw "Build output missing: $dll" }
        $process = Start-Process dotnet -ArgumentList ('"' + $dll + '"') -WorkingDirectory $directory -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput (Join-Path $logDirectory "$($service.Name).out.log") `
            -RedirectStandardError (Join-Path $logDirectory "$($service.Name).err.log")
        $children += $process
        Write-Host "$($service.Name) PID=$($process.Id) $env:ASPNETCORE_URLS"
        $expected = if ($service.Expected) { $service.Expected } else { 200 }
        Wait-Http ($env:ASPNETCORE_URLS + $service.Health) $process $expected
    }
    Write-Host "Service logs and bank statements: $logDirectory"
} catch {
    foreach ($child in $children) { if (-not $child.HasExited) { Stop-Process -Id $child.Id } }
    throw
} finally {
    foreach ($key in $previous.Keys) { [Environment]::SetEnvironmentVariable($key, $previous[$key], 'Process') }
}
