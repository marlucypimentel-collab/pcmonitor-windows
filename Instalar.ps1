$ErrorActionPreference = 'Stop'
$root = Join-Path $env:LOCALAPPDATA 'PCmonitor'
$exe = Join-Path $root 'bin\PCmonitor.exe'
$built = Join-Path $PSScriptRoot 'bin\PCmonitor.exe'
if (!(Test-Path -LiteralPath $built)) { throw 'Execute Compilar.ps1 primeiro.' }
if ([IO.Path]::GetFullPath($built) -eq [IO.Path]::GetFullPath($exe)) { throw 'Execute o instalador a partir de uma copia dos fontes fora da pasta de instalacao.' }
# Stop only this installation, not unrelated processes with the same name.
if (Test-Path -LiteralPath $exe) {
    Start-Process -FilePath $exe -ArgumentList '--stop' -WindowStyle Hidden -Wait
    $deadline = [DateTime]::Now.AddSeconds(30)
    do {
        $running = @(Get-Process PCmonitor -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe })
        if ($running.Count -eq 0) { break }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::Now -lt $deadline)
    if ($running.Count -gt 0) { throw 'O monitor ainda esta ativo; a instalacao foi interrompida.' }
}
New-Item -ItemType Directory -Path (Join-Path $root 'bin') -Force | Out-Null
Copy-Item -LiteralPath $built -Destination $exe -Force
$sh = New-Object -ComObject WScript.Shell
foreach ($entry in @(@('Iniciar','--run'),@('Parar','--stop'),@('Marcar DEPOIS','--after'),@('Marcar ANTES','--before'))) {
    $s = $sh.CreateShortcut((Join-Path $root ($entry[0] + '.lnk')))
    $s.TargetPath = $exe
    $s.Arguments = $entry[1]
    $s.WorkingDirectory = Split-Path $exe
    $s.Save()
}
$s = $sh.CreateShortcut((Join-Path $root 'Abrir registros.lnk'))
$s.TargetPath = Join-Path $env:WINDIR 'explorer.exe'
$s.Arguments = '"' + (Join-Path $root 'data') + '"'
$s.Save()
$s = $sh.CreateShortcut((Join-Path $root 'Ver status.lnk'))
$s.TargetPath = Join-Path $env:WINDIR 'System32\notepad.exe'
$s.Arguments = '"' + (Join-Path $root 'data\status.txt') + '"'
$s.Save()
$startup = Join-Path ([Environment]::GetFolderPath('Startup')) 'PCmonitor.lnk'
try {
    $user = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    $action = New-ScheduledTaskAction -Execute $exe -Argument '--run' -WorkingDirectory (Split-Path $exe)
    $trigger = New-ScheduledTaskTrigger -AtLogOn -User $user
    $principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
    $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero) -MultipleInstances IgnoreNew -StartWhenAvailable
    $task = New-ScheduledTask -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description 'Monitor local de CPU, memoria e disco. Inicia no login sem atraso programado.'
    Register-ScheduledTask -TaskName 'PCmonitor' -InputObject $task -Force | Out-Null
    if (Test-Path -LiteralPath $startup) { Remove-Item -LiteralPath $startup }
    Write-Host 'Tarefa PCmonitor configurada: login sem atraso programado.'
} catch {
    Write-Warning ('Nao foi possivel configurar a tarefa: ' + $_.Exception.Message)
    $s = $sh.CreateShortcut($startup)
    $s.TargetPath = $exe
    $s.Arguments = '--run'
    $s.WorkingDirectory = Split-Path $exe
    $s.Save()
    Write-Warning 'Foi usado o atalho Inicializar. O Windows pode atrasar essa modalidade de inicio.'
}
Start-Process -FilePath $exe -WindowStyle Hidden
Write-Host "Monitor instalado em $root. Os dados anteriores foram preservados."