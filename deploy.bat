@echo off

REM Deploy script for Target Scheduler Plugin

REM Publishes and copies the required plugin files into NINA's 3.0 plugin folder



setlocal enableextensions



echo ========================================

echo Target Scheduler Plugin Deployment

echo ========================================

echo.



taskkill /F /IM "NINA.exe" 2>nul



REM Publish the plugin (Release)

echo Publishing plugin (Release)...

dotnet publish "NINA.Plugin.TargetScheduler\NINA.Plugin.TargetScheduler.csproj" -c Release -o publish

if errorlevel 1 (

    echo.

    echo ERROR: dotnet publish failed.

    echo.

    pause

    exit /b 1

)



REM Define paths

set "PUBLISH_DIR=%cd%\publish"

set "NINA_PLUGIN_DIR=%localappdata%\NINA\Plugins\3.0.0\NINA.Plugin.TargetScheduler"



REM Ensure destination exists

if not exist "%NINA_PLUGIN_DIR%" (

    echo Creating plugin directory...

    mkdir "%NINA_PLUGIN_DIR%"

)



REM Files to copy (main plugin and dependencies)

set "FILE1=NINA.Plugin.TargetScheduler.dll"

set "FILE2=NINA.Plugin.TargetScheduler.deps.json"

set "FILE3=NINA.Plugin.TargetScheduler.runtimeconfig.json"

set "FILE4=NINA.Plugin.TargetScheduler.SyncService.dll"

set "FILE5=NINA.Plugin.TargetScheduler.Shared.dll"

set "FILE6=LinqKit.Core.dll"

set "FILE7=LinqKit.dll"

set "FILE8=Microsoft.WindowsAPICodePack.dll"

set "FILE9=Microsoft.WindowsAPICodePack.Shell.dll"



echo Copying files to: "%NINA_PLUGIN_DIR%"

for %%F in ("%FILE1%" "%FILE2%" "%FILE3%" "%FILE4%" "%FILE5%" "%FILE6%" "%FILE7%" "%FILE8%" "%FILE9%") do (

    if exist "%PUBLISH_DIR%\%%~F" (

        copy /Y "%PUBLISH_DIR%\%%~F" "%NINA_PLUGIN_DIR%\" >nul

        echo   - Copied %%~F

    ) else (

        echo   - WARNING: %%~F not found in publish output

    )

)



REM Clean up locale folders and runtimes (as per PostBuild event)

echo Cleaning up unnecessary folders...

for /d %%D in ("%NINA_PLUGIN_DIR%\ca-ES" "%NINA_PLUGIN_DIR%\cs" "%NINA_PLUGIN_DIR%\cs-CZ" "%NINA_PLUGIN_DIR%\da-DK" "%NINA_PLUGIN_DIR%\de" "%NINA_PLUGIN_DIR%\de-DE" "%NINA_PLUGIN_DIR%\el-GR" "%NINA_PLUGIN_DIR%\en-GB" "%NINA_PLUGIN_DIR%\en-US" "%NINA_PLUGIN_DIR%\es" "%NINA_PLUGIN_DIR%\es-ES" "%NINA_PLUGIN_DIR%\eu-ES" "%NINA_PLUGIN_DIR%\fr" "%NINA_PLUGIN_DIR%\fr-FR" "%NINA_PLUGIN_DIR%\gl-ES" "%NINA_PLUGIN_DIR%\hu-HU" "%NINA_PLUGIN_DIR%\it" "%NINA_PLUGIN_DIR%\it-IT" "%NINA_PLUGIN_DIR%\ja" "%NINA_PLUGIN_DIR%\ja-JP" "%NINA_PLUGIN_DIR%\ko" "%NINA_PLUGIN_DIR%\ko-KR" "%NINA_PLUGIN_DIR%\nb-NO" "%NINA_PLUGIN_DIR%\nl-NL" "%NINA_PLUGIN_DIR%\pl" "%NINA_PLUGIN_DIR%\pl-PL" "%NINA_PLUGIN_DIR%\pt-BR" "%NINA_PLUGIN_DIR%\pt-PT" "%NINA_PLUGIN_DIR%\ru" "%NINA_PLUGIN_DIR%\ru-RU" "%NINA_PLUGIN_DIR%\tr" "%NINA_PLUGIN_DIR%\tr-TR" "%NINA_PLUGIN_DIR%\uk-UA" "%NINA_PLUGIN_DIR%\zh-CN" "%NINA_PLUGIN_DIR%\zh-HK" "%NINA_PLUGIN_DIR%\zh-Hans" "%NINA_PLUGIN_DIR%\zh-Hant" "%NINA_PLUGIN_DIR%\zh-TW" "%NINA_PLUGIN_DIR%\runtimes") do (

    if exist %%D (

        rd /s /q %%D 2>nul

    )

)



echo.

echo Deployment complete.

echo Destination: "%NINA_PLUGIN_DIR%"

echo.



REM Locate and start NINA automatically

set "NINA_EXE="

for %%E in (

    "C:\Program Files\N.I.N.A. - Nighttime Imaging 'N' Astronomy\NINA.exe"

) do (

    if exist %%~E (

        set "NINA_EXE=%%~E"

        goto :start_nina

    )

)



echo WARNING: Could not find NINA.exe to start automatically.

echo Please start NINA manually.

goto :end



:start_nina

echo Starting NINA: "%NINA_EXE%"

start "" "%NINA_EXE%"



:end

endlocal

exit /b 0

