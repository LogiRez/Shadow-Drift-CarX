param([string]$ReferenceDirectory,[string]$HarmonyDirectory)
$ErrorActionPreference='Stop'
$config=Get-Content (Join-Path $PSScriptRoot 'tool.json') -Raw|ConvertFrom-Json
$package=Split-Path -Parent $PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$refs=@('mscorlib.dll','netstandard.dll','System.dll','System.Core.dll','UnityEngine.TextRenderingModule.dll','UnityEngine.CoreModule.dll','UnityEngine.PhysicsModule.dll','UnityEngine.InputLegacyModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.ImageConversionModule.dll')|ForEach-Object {'/reference:'+(Join-Path $ReferenceDirectory $_)}
$resources=@('0Harmony','Mono.Cecil','MonoMod.Utils','MonoMod.RuntimeDetour')|ForEach-Object {'/resource:'+(Join-Path $HarmonyDirectory ($_+'.dll'))+',RCLI.Harmony.'+$_+'.dll'}
$files=Get-ChildItem $PSScriptRoot -Filter *.cs|Where-Object {$_.Name -notin @('Injector.cs','LauncherUI.cs')}|ForEach-Object {$_.FullName}
& $compiler /nologo /noconfig /target:library /optimize+ /nostdlib+ ('/define:'+$config.Define) ('/out:'+(Join-Path $package ($config.Folder+'.Mod.dll'))) $refs $resources $files
if($LASTEXITCODE -ne 0){throw 'Tool build failed'}
& $compiler /nologo /target:winexe /platform:x64 ('/win32icon:'+(Join-Path $PSScriptRoot 'app.ico')) ('/resource:'+(Join-Path $PSScriptRoot 'app.ico')+',App.Icon.ico') /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll ('/out:'+(Join-Path $package ($config.Name+'.exe'))) (Join-Path $PSScriptRoot 'Injector.cs') (Join-Path $PSScriptRoot 'LauncherUI.cs') (Join-Path $PSScriptRoot 'RcliSettings.cs')
if($LASTEXITCODE -ne 0){throw 'Launcher build failed'}


