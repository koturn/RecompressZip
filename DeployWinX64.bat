set SOLUTION_NAME=RecompressZip
set SOLUTION_FILE=%SOLUTION_NAME%.sln
set BUILD_CONFIG=Release
set MAIN_PROJECT_OUTDIR=%SOLUTION_NAME%\bin\%BUILD_CONFIG%\net5.0
set ARTIFACTS_BASEDIR=Artifacts
set ARTIFACTS_SUBDIR=%SOLUTION_NAME%-winx64
set ARTIFACTS_NAME=%ARTIFACTS_SUBDIR%.zip

dotnet publish --nologo -c %BUILD_CONFIG% -o %ARTIFACTS_BASEDIR%\%ARTIFACTS_SUBDIR% -p:PublishReadyToRun=true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:PublishReadyToRunShowWarnings=true --self-contained true -r win-x64 %SOLUTION_FILE% || set ERRORLEVEL=0

xcopy /S /Y %MAIN_PROJECT_OUTDIR%\x64\*.dll %ARTIFACTS_BASEDIR%\%ARTIFACTS_SUBDIR%\x64\

del %ARTIFACTS_BASEDIR%\%ARTIFACTS_SUBDIR%\*.pdb

cd %ARTIFACTS_BASEDIR%
"C:\Program Files\7-Zip\7z.exe" a -mmt=on -mm=Deflate -mfb=258 -mpass=15 -r ..\%ARTIFACTS_NAME% %ARTIFACTS_SUBDIR%
cd ..
