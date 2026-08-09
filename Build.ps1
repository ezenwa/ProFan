$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$isccCandidates = @(
    'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    'C:\Program Files\Inno Setup 6\ISCC.exe',
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
)
$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (-not (Test-Path -LiteralPath $csc -PathType Leaf)) { throw 'No se encontró el compilador C# de Windows.' }
if ([string]::IsNullOrWhiteSpace($iscc)) { throw 'No se encontró Inno Setup 6.' }

$build = Join-Path $root 'build'
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Path $build,$dist -Force | Out-Null

& $csc /nologo /target:exe /optimize+ /out:"$build\IconGenerator.exe" /reference:System.Drawing.dll "$root\assets\IconGenerator.cs"
if ($LASTEXITCODE -ne 0) { throw 'Falló la generación del compilador de iconos.' }
& "$build\IconGenerator.exe" "$root\assets\ProFan.ico"
if ($LASTEXITCODE -ne 0) { throw 'Falló la generación del icono.' }

& $csc /nologo /target:winexe /optimize+ /out:"$build\ProFan.exe" /win32manifest:"$root\src\ProFan.manifest" /win32icon:"$root\assets\ProFan.ico" /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "$root\src\AssemblyInfo.cs" "$root\src\ProFan.cs"
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de ProFan.' }

& $iscc "$root\installer\ProFan.iss"
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación del instalador.' }
Write-Host "Aplicación: $build\ProFan.exe"
Write-Host "Instalador: $dist\ProFan-Setup.exe"
