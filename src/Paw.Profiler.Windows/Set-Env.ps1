#
# use:
# . ..\Paw.Profiler.Windows\Set-Env.ps1
#

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$BuildDir = Join-Path $ScriptDir "x64" "Debug"

$env:CORECLR_ENABLE_PROFILING = "1"
$env:CORECLR_PROFILER_PATH_64 = Join-Path $BuildDir "Paw.Profiler.Windows.dll"
$env:CORECLR_PROFILER = "{9F2716B7-F482-45F8-BDD5-867512FB9225}"
$env:DOTNEXT_LOGDIR = $BuildDir

Write-Host "CORECLR_ENABLE_PROFILING: $($env:CORECLR_ENABLE_PROFILING)"
Write-Host "CORECLR_PROFILER_PATH_64: $($env:CORECLR_PROFILER_PATH_64)"
Write-Host "CORECLR_PROFILER:         $($env:CORECLR_PROFILER)"
Write-Host "DOTNEXT_LOGDIR:           $($env:DOTNEXT_LOGDIR)"