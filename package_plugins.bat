@echo off
set ARTIFACTS_DIR=%1
set BUILD_NUMBER=%2

cd %ARTIFACTS_DIR%
for /d %%i in (*) do (
    echo Creating package for %%i...
    powershell Compress-Archive -Path "%%i" -DestinationPath "%%i_v%BUILD_NUMBER%.zip" -Force
    echo Created: %%i_v%BUILD_NUMBER%.zip
)
