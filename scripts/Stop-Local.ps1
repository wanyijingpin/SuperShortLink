[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$localRoot = Join-Path $repoRoot '.local'
$mysqlRoot = Join-Path $env:ProgramData 'SuperShortLink\mysql'
$settingsPath = Join-Path $localRoot 'local.settings.psd1'
$appPidPath = Join-Path $localRoot 'app.pid'
$mysqlPidPath = Join-Path $mysqlRoot 'mysql.pid'

if (Test-Path -LiteralPath $appPidPath) {
    $appPid = [int](Get-Content -LiteralPath $appPidPath -Raw)
    $appProcess = Get-Process -Id $appPid -ErrorAction SilentlyContinue
    if ($appProcess -and $appProcess.ProcessName -eq 'dotnet') {
        Stop-Process -Id $appPid
        $appProcess.WaitForExit(10000) | Out-Null
    }
    Remove-Item -LiteralPath $appPidPath -Force
}

if ((Test-Path -LiteralPath $settingsPath) -and (Test-Path -LiteralPath $mysqlPidPath)) {
    $settings = Import-PowerShellDataFile -LiteralPath $settingsPath
    $mysqlAdmin = 'C:\Program Files\MySQL\MySQL Server 8.4\bin\mysqladmin.exe'
    if (Test-Path -LiteralPath $mysqlAdmin) {
        $previousMySqlPassword = $env:MYSQL_PWD
        try {
            $env:MYSQL_PWD = $settings.RootPassword
            & $mysqlAdmin --protocol=tcp --host=127.0.0.1 "--port=$($settings.MySqlPort)" --user=root shutdown
        }
        finally {
            if ($null -eq $previousMySqlPassword) {
                Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue
            }
            else {
                $env:MYSQL_PWD = $previousMySqlPassword
            }
        }
    }
}

Write-Host 'SuperShortLink and its local MySQL instance are stopped.'
