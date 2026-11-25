$ErrorActionPreference = 'Stop'

$repo = 'evgen70rus/NVIDIA-Driver-Downloader'
$branch = 'main'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$tempDir = Join-Path $env:TEMP ("NVDD_Update_" + [guid]::NewGuid().ToString('N'))

try {
    New-Item -ItemType Directory -Path $tempDir | Out-Null

    $source = Join-Path $tempDir 'NVIDIA_Driver_Downloader.cs'
    $icon = Join-Path $tempDir 'nvdd_icon.ico'
    $newExe = Join-Path $tempDir 'NVIDIA_Driver_Downloader.exe'

    $sourceUrl = "https://raw.githubusercontent.com/$repo/$branch/NVIDIA_Driver_Downloader.cs"
    $iconUrl = "https://raw.githubusercontent.com/$repo/$branch/nvdd_icon.ico"

    Write-Host '1/4 - Загрузка исходного кода...' -ForegroundColor Cyan
    Invoke-WebRequest -Uri $sourceUrl -OutFile $source -UseBasicParsing
    if ((Get-Item $source).Length -lt 10000) {
        throw 'Исходный код не прошёл проверку размера.'
    }

    Write-Host '2/4 - Загрузка иконки...' -ForegroundColor Cyan
    Invoke-WebRequest -Uri $iconUrl -OutFile $icon -UseBasicParsing
    if ((Get-Item $icon).Length -lt 1000) {
        throw 'Иконка не прошла проверку размера.'
    }

    # Correct Windows .NET Framework compiler path: Microsoft.NET, not Microsoft\NET
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path $compiler)) {
        $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
    }
    if (-not (Test-Path $compiler)) {
        throw "Компилятор не найден. Проверены пути Framework64 и Framework в папке Microsoft.NET."
    }

    Write-Host ('Найден компилятор: ' + $compiler) -ForegroundColor DarkGray
    Write-Host '3/4 - Сборка EXE...' -ForegroundColor Cyan
    $compilerArgs = @(
        '/nologo',
        '/target:winexe',
        '/codepage:65001',
        '/r:System.dll',
        '/r:System.Windows.Forms.dll',
        '/r:System.Drawing.dll',
        '/r:System.Management.dll',
        ("/win32icon:$icon"),
        ("/out:$newExe"),
        $source
    )
    & $compiler @compilerArgs
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $newExe)) {
        throw "Ошибка компиляции. Код завершения: $LASTEXITCODE"
    }

    Write-Host '4/4 - Установка новой версии...' -ForegroundColor Cyan
    $names = @('NVIDIA_Driver_Downloader.cs', 'nvdd_icon.ico', 'NVIDIA_Driver_Downloader.exe')
    $newFiles = @($source, $icon, $newExe)
    $backupDir = Join-Path $tempDir 'backup'
    New-Item -ItemType Directory -Path $backupDir | Out-Null

    for ($i = 0; $i -lt $names.Count; $i++) {
        $dest = Join-Path $root $names[$i]
        if (Test-Path $dest) {
            Copy-Item $dest (Join-Path $backupDir $names[$i]) -Force
        }
    }

    try {
        for ($i = 0; $i -lt $names.Count; $i++) {
            Copy-Item $newFiles[$i] (Join-Path $root $names[$i]) -Force
        }
    }
    catch {
        for ($i = 0; $i -lt $names.Count; $i++) {
            $backup = Join-Path $backupDir $names[$i]
            $dest = Join-Path $root $names[$i]
            if (Test-Path $backup) {
                Copy-Item $backup $dest -Force
            }
        }
        throw
    }

    Write-Host ''
    Write-Host 'Программа успешно обновлена.' -ForegroundColor Green
}
catch {
    Write-Host ''
    Write-Host ('ОШИБКА: ' + $_.Exception.Message) -ForegroundColor Red
    exit 1
}
finally {
    Remove-Item $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}
