$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$assemblyText = Get-Content -Raw -LiteralPath (Join-Path $root 'src\AssemblyInfo.cs')
$installerText = Get-Content -Raw -LiteralPath (Join-Path $root 'installer\ProFan.iss')
$citationText = Get-Content -Raw -LiteralPath (Join-Path $root 'CITATION.cff')
$manifestText = Get-Content -Raw -LiteralPath (Join-Path $root 'src\ProFan.manifest')
$assemblyVersion = [regex]::Match($assemblyText, 'AssemblyFileVersion\("([0-9.]+)"\)').Groups[1].Value
$releaseVersion = $assemblyVersion -replace '\.0$',''
$expectedVersions = @{
    'installer\ProFan.iss' = [regex]::Match($installerText, '#define MyAppVersion "([0-9.]+)"').Groups[1].Value
    'CITATION.cff' = [regex]::Match($citationText, '(?m)^version:\s*([0-9.]+)\s*$').Groups[1].Value
    'src\ProFan.manifest' = ([regex]::Match($manifestText, '<assemblyIdentity version="([0-9.]+)"').Groups[1].Value -replace '\.0$','')
}
if ([string]::IsNullOrWhiteSpace($assemblyVersion) -or [string]::IsNullOrWhiteSpace($releaseVersion)) { throw 'No se pudo determinar la versión de la aplicación.' }
foreach ($entry in $expectedVersions.GetEnumerator()) {
    if ($entry.Value -ne $releaseVersion) { throw "Versión desalineada en $($entry.Key): $($entry.Value); esperada: $releaseVersion." }
}
foreach ($preview in 'profan-automatic-preview.png','profan-manual-preview.png','profan-tray-preview.png','profan-social-preview.png') {
    if (-not (Test-Path -LiteralPath (Join-Path $root "assets\$preview") -PathType Leaf)) { throw "Falta el preview requerido: assets\$preview" }
}
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
