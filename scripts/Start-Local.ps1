[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$localRoot = Join-Path $repoRoot '.local'
$settingsPath = Join-Path $localRoot 'local.settings.psd1'
$appPidPath = Join-Path $localRoot 'app.pid'
$projectPath = Join-Path $repoRoot 'src\SuperShortLink\SuperShortLink.csproj'
$appDll = Join-Path $repoRoot 'src\SuperShortLink\bin\Release\net8.0\SuperShortLink.dll'

& (Join-Path $PSScriptRoot 'Initialize-Local.ps1')
$settings = Import-PowerShellDataFile -LiteralPath $settingsPath

if (Test-Path -LiteralPath $appPidPath) {
    $existingPid = [int](Get-Content -LiteralPath $appPidPath -Raw)
    if (Get-Process -Id $existingPid -ErrorAction SilentlyContinue) {
        Write-Host "SuperShortLink is already running at http://127.0.0.1:$($settings.AppPort)/Login/Index"
        Write-Host "Login: $($settings.AdminUser) / $($settings.AdminPassword)"
        exit 0
    }
}

dotnet build $projectPath --configuration Release --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Application build failed with exit code $LASTEXITCODE."
}

$environmentNames = @(
    'ASPNETCORE_ENVIRONMENT',
    'ASPNETCORE_URLS',
    'ShortLink__DbType',
    'ShortLink__ConnectionString',
    'ShortLink__LoginAcount',
    'ShortLink__LoginPassword',
    'ShortLink__Secrect'
)
$oldEnvironment = @{}
foreach ($name in $environmentNames) {
    $oldEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_URLS = "http://127.0.0.1:$($settings.AppPort)"
    $env:ShortLink__DbType = 'MySQL'
    $env:ShortLink__ConnectionString = "Server=127.0.0.1;Port=$($settings.MySqlPort);User Id=$($settings.DatabaseUser);Password=$($settings.DatabasePassword);Database=$($settings.DatabaseName);SslMode=None;AllowPublicKeyRetrieval=True;"
    $env:ShortLink__LoginAcount = $settings.AdminUser
    $env:ShortLink__LoginPassword = $settings.AdminPassword
    $env:ShortLink__Secrect = $settings.ShortLinkSecret

    $stdoutPath = Join-Path $localRoot 'app.stdout.log'
    $stderrPath = Join-Path $localRoot 'app.stderr.log'
    $process = Start-Process -FilePath 'dotnet' -ArgumentList $appDll -WorkingDirectory (Split-Path -Parent $projectPath) -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
    Set-Content -LiteralPath $appPidPath -Value $process.Id -Encoding ascii
}
finally {
    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $oldEnvironment[$name], 'Process')
    }
}

$url = "http://127.0.0.1:$($settings.AppPort)/Login/Index"
$ready = $false
for ($attempt = 0; $attempt -lt 60; $attempt++) {
    try {
        $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2
        if ($response.StatusCode -eq 200) {
            $ready = $true
            break
        }
    }
    catch {
        Start-Sleep -Milliseconds 500
    }
}
if (-not $ready) {
    throw "SuperShortLink did not become ready. See $(Join-Path $localRoot 'app.stderr.log')."
}

Write-Host "SuperShortLink is ready: $url"
Write-Host "Login: $($settings.AdminUser) / $($settings.AdminPassword)"
