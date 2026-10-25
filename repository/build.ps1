$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $taskCompiler)) {
    $taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $taskCompiler)) {
    throw 'The Windows .NET Framework C# compiler was not found. Enable .NET Framework 4.8 and try again.'
}
$taskOutput = Join-Path $taskRoot 'dist'
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskSources = @('Program.cs', 'TaskData.cs', 'TaskForms.cs', 'HelpForms.cs', 'DateTimeEntry.cs', 'TaskOperations.cs', 'DeleteTasksDialog.cs', 'TaskColors.cs', 'ColorSettingsDialog.cs', 'Recurrence.cs', 'RepeatForms.cs', 'CalendarImport.cs', 'CalendarImportDialog.cs', 'CalendarCommands.cs', 'SelfTests.cs', 'RepeatTests.cs', 'CalendarTests.cs', 'DailyTests.cs', 'InteractionTests.cs', 'CompletedTaskTests.cs', 'ColorTests.cs') | ForEach-Object { Join-Path $taskRoot $_ }
$taskExecutable = Join-Path $taskOutput 'Simple Tasks.exe'
& $taskCompiler /nologo /target:winexe /platform:anycpu /optimize+ "/out:$taskExecutable" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Runtime.Serialization.dll /reference:System.Xml.dll "/win32manifest:$(Join-Path $taskRoot 'app.manifest')" $taskSources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item -LiteralPath (Join-Path $taskRoot 'App.config') -Destination ($taskExecutable + '.config') -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'README.md') -Destination (Join-Path $taskOutput 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'LICENSE') -Destination (Join-Path $taskOutput 'LICENSE') -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs') -Destination $taskOutput -Recurse -Force
$taskPackageFiles = @($taskExecutable, ($taskExecutable + '.config'), (Join-Path $taskOutput 'README.md'), (Join-Path $taskOutput 'LICENSE'), (Join-Path $taskOutput 'docs'))
Compress-Archive -LiteralPath $taskPackageFiles -DestinationPath (Join-Path $taskRoot 'Simple-Tasks-Windows.zip') -Force
Write-Output "Built $taskExecutable"
