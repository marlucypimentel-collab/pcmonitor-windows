$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /r:System.Management.dll /r:System.Core.dll "/out:$PSScriptRoot\bin\PCmonitor.exe" "$PSScriptRoot\src\PCmonitor.cs"
if ($LASTEXITCODE -ne 0) { throw 'Falha na compilacao' }
Get-FileHash "$PSScriptRoot\bin\PCmonitor.exe" -Algorithm SHA256