[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$localRoot = Join-Path $repoRoot '.local'
$mysqlRoot = Join-Path $env:ProgramData 'SuperShortLink\mysql'
$dataDir = Join-Path $mysqlRoot 'data'
$settingsPath = Join-Path $localRoot 'local.settings.psd1'
$configPath = Join-Path $mysqlRoot 'my.ini'
$schemaPath = Join-Path $repoRoot 'build\mysql_init.sql'

function Find-MySqlTool([string]$Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    $candidate = Join-Path 'C:\Program Files\MySQL\MySQL Server 8.4\bin' "$Name.exe"
    if (Test-Path -LiteralPath $candidate) {
        return $candidate
    }

    throw "MySQL 8.4 is not installed. Install Oracle.MySQL with winget first."
}

function New-HexSecret([int]$ByteCount) {
    $bytes = New-Object byte[] $ByteCount
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
    }
    finally {
        $generator.Dispose()
    }
    return ([BitConverter]::ToString($bytes)).Replace('-', '')
}

function New-Base62Secret {
    $characters = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789'.ToCharArray()
    return -join ($characters | Sort-Object { Get-Random })
}

New-Item -ItemType Directory -Force -Path $localRoot, $mysqlRoot | Out-Null

if (-not (Test-Path -LiteralPath $settingsPath)) {
    $rootPassword = New-HexSecret 20
    $dbPassword = New-HexSecret 20
    $adminPassword = New-HexSecret 12
    $shortLinkSecret = New-Base62Secret
    $settings = @"
@{
    MySqlPort = 3307
    AppPort = 5080
    DatabaseName = 'super_short_link'
    DatabaseUser = 'shortlink'
    DatabasePassword = '$dbPassword'
    RootPassword = '$rootPassword'
    AdminUser = 'admin'
    AdminPassword = '$adminPassword'
    ShortLinkSecret = '$shortLinkSecret'
}
"@
    Set-Content -LiteralPath $settingsPath -Value $settings -Encoding utf8
}

$settings = Import-PowerShellDataFile -LiteralPath $settingsPath
$mysqld = Find-MySqlTool 'mysqld'
$mysql = Find-MySqlTool 'mysql'
$mysqlAdmin = Find-MySqlTool 'mysqladmin'
$baseDir = Split-Path -Parent (Split-Path -Parent $mysqld)
$baseDirIni = $baseDir.Replace('\', '/')
$dataDirIni = $dataDir.Replace('\', '/')
$errorLogIni = (Join-Path $mysqlRoot 'mysql-error.log').Replace('\', '/')
$pidFileIni = (Join-Path $mysqlRoot 'mysql.pid').Replace('\', '/')

$config = @"
[client]
host=127.0.0.1
port=$($settings.MySqlPort)
protocol=tcp

[mysqld]
basedir=$baseDirIni
datadir=$dataDirIni
port=$($settings.MySqlPort)
bind-address=127.0.0.1
mysqlx=0
character-set-server=utf8mb4
collation-server=utf8mb4_unicode_ci
log-error=$errorLogIni
pid-file=$pidFileIni
"@
Set-Content -LiteralPath $configPath -Value $config -Encoding ascii

$isNewDatabase = -not (Test-Path -LiteralPath (Join-Path $dataDir 'mysql'))
if ($isNewDatabase) {
    New-Item -ItemType Directory -Force -Path $dataDir | Out-Null
    & $mysqld "--defaults-file=$configPath" '--initialize-insecure'
    if ($LASTEXITCODE -ne 0) {
        throw "MySQL data directory initialization failed with exit code $LASTEXITCODE."
    }
}

$mysqlPidPath = Join-Path $mysqlRoot 'mysql.pid'
$mysqlRunning = $false
if (Test-Path -LiteralPath $mysqlPidPath) {
    $mysqlPid = [int](Get-Content -LiteralPath $mysqlPidPath -Raw)
    $mysqlRunning = $null -ne (Get-Process -Id $mysqlPid -ErrorAction SilentlyContinue)
}

if (-not $mysqlRunning) {
    $stdoutPath = Join-Path $mysqlRoot 'mysql.stdout.log'
    $stderrPath = Join-Path $mysqlRoot 'mysql.stderr.log'
    Start-Process -FilePath $mysqld -ArgumentList "--defaults-file=$configPath", '--console' -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath | Out-Null
}

$ready = $false
$previousMySqlPassword = $env:MYSQL_PWD
try {
    if ($isNewDatabase) {
        Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue
    }
    else {
        $env:MYSQL_PWD = $settings.RootPassword
    }

    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        & $mysqlAdmin --protocol=tcp --host=127.0.0.1 "--port=$($settings.MySqlPort)" --connect-timeout=1 --user=root ping --silent 2>$null
        if ($LASTEXITCODE -eq 0) {
            $ready = $true
            break
        }
        Start-Sleep -Milliseconds 500
    }
}
finally {
    if ($null -eq $previousMySqlPassword) {
        Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue
    }
    else {
        $env:MYSQL_PWD = $previousMySqlPassword
    }
}
if (-not $ready) {
    throw "MySQL did not become ready. See $errorLogIni."
}

$previousMySqlPassword = $env:MYSQL_PWD
try {
    if ($isNewDatabase) {
        Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue
        $bootstrapSql = @"
CREATE DATABASE IF NOT EXISTS ``$($settings.DatabaseName)`` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER IF NOT EXISTS '$($settings.DatabaseUser)'@'127.0.0.1' IDENTIFIED BY '$($settings.DatabasePassword)';
CREATE USER IF NOT EXISTS '$($settings.DatabaseUser)'@'localhost' IDENTIFIED BY '$($settings.DatabasePassword)';
GRANT ALL PRIVILEGES ON ``$($settings.DatabaseName)``.* TO '$($settings.DatabaseUser)'@'127.0.0.1';
GRANT ALL PRIVILEGES ON ``$($settings.DatabaseName)``.* TO '$($settings.DatabaseUser)'@'localhost';
ALTER USER 'root'@'localhost' IDENTIFIED BY '$($settings.RootPassword)';
FLUSH PRIVILEGES;
"@
        & $mysql --protocol=tcp --host=127.0.0.1 "--port=$($settings.MySqlPort)" --user=root "--execute=$bootstrapSql"
        if ($LASTEXITCODE -ne 0) {
            throw "MySQL user/database creation failed with exit code $LASTEXITCODE."
        }
    }

    $env:MYSQL_PWD = $settings.RootPassword
    $schemaSql = Get-Content -LiteralPath $schemaPath -Raw
    & $mysql --protocol=tcp --host=127.0.0.1 "--port=$($settings.MySqlPort)" --user=root "--database=$($settings.DatabaseName)" "--execute=$schemaSql"
    if ($LASTEXITCODE -ne 0) {
        throw "MySQL schema creation failed with exit code $LASTEXITCODE."
    }

    $metadataMigration = @"
SET @sql = (SELECT IF(COUNT(*) = 0, 'ALTER TABLE short_link ADD COLUMN remark VARCHAR(500) NOT NULL DEFAULT ''''', 'SELECT 1') FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'short_link' AND COLUMN_NAME = 'remark');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql = (SELECT IF(COUNT(*) = 0, 'ALTER TABLE short_link ADD COLUMN group_name VARCHAR(128) NOT NULL DEFAULT ''''', 'SELECT 1') FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'short_link' AND COLUMN_NAME = 'group_name');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql = (SELECT IF(COUNT(*) = 0, 'ALTER TABLE short_link ADD INDEX ix_short_link_group_name (group_name)', 'SELECT 1') FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'short_link' AND INDEX_NAME = 'ix_short_link_group_name');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql = (SELECT IF(COUNT(*) = 0, 'ALTER TABLE short_link ADD COLUMN status INT NOT NULL DEFAULT 1', 'SELECT 1') FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'short_link' AND COLUMN_NAME = 'status');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql = (SELECT IF(COUNT(*) = 0, 'ALTER TABLE short_link ADD INDEX ix_short_link_status (status)', 'SELECT 1') FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'short_link' AND INDEX_NAME = 'ix_short_link_status');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql = (SELECT IF(COUNT(*) = 0, 'ALTER TABLE short_link ADD COLUMN status_reason VARCHAR(500) NOT NULL DEFAULT ''''', 'SELECT 1') FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'short_link' AND COLUMN_NAME = 'status_reason');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql = (SELECT IF(COUNT(*) = 0, 'ALTER TABLE short_link ADD COLUMN review_time TIMESTAMP(0) NULL DEFAULT NULL', 'SELECT 1') FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'short_link' AND COLUMN_NAME = 'review_time');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
"@
    & $mysql --protocol=tcp --host=127.0.0.1 "--port=$($settings.MySqlPort)" --user=root "--database=$($settings.DatabaseName)" "--execute=$metadataMigration" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Short-link metadata migration failed with exit code $LASTEXITCODE."
    }
}
finally {
    if ($null -eq $previousMySqlPassword) {
        Remove-Item Env:MYSQL_PWD -ErrorAction SilentlyContinue
    }
    else {
        $env:MYSQL_PWD = $previousMySqlPassword
    }
}

Write-Host "MySQL is ready on 127.0.0.1:$($settings.MySqlPort)."
