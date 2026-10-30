@echo off
setlocal
cd /d "%~dp0"
set LOG=%~dp0build_log.txt
echo === build %date% %time% === > "%LOG%"
set DN=dotnet
where dotnet >> "%LOG%" 2>&1 || set DN="%~dp0..\dotnet10\dotnet.exe"
if not exist "J:\Games\1A-IRLHandling\_backups\TrueView_20261024" mkdir "J:\Games\1A-IRLHandling\_backups\TrueView_20261024"
copy /y "J:\Games\SPT 4.1.6\BepInEx\plugins\TrueView.dll" "J:\Games\1A-IRLHandling\_backups\TrueView_20261024\TrueView.dll" >> "%LOG%" 2>&1
%DN% build TrueView.csproj -c Release -nologo -v minimal >> "%LOG%" 2>&1
if errorlevel 1 (echo BUILD FAILED >> "%LOG%" & exit /b 1)
copy /y "bin\Release\netstandard2.1\TrueView.dll" "J:\Games\SPT 4.1.6\BepInEx\plugins\TrueView.dll" >> "%LOG%" 2>&1
if errorlevel 1 (echo DEPLOY FAILED - is the game running? >> "%LOG%" & exit /b 1)
if not exist "J:\Games\SPT 4.1.6\BepInEx\plugins\TrueView\audio" mkdir "J:\Games\SPT 4.1.6\BepInEx\plugins\TrueView\audio"
copy /y "audio\*.wav" "J:\Games\SPT 4.1.6\BepInEx\plugins\TrueView\audio\" >> "%LOG%" 2>&1
if errorlevel 1 (echo AUDIO COPY FAILED >> "%LOG%" & exit /b 1)
echo BUILD+DEPLOY OK >> "%LOG%"
