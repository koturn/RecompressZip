set SOLUTION_NAME=RecompressZip
set SOLUTION_FILE=%SOLUTION_NAME%.sln
set BUILD_CONFIG=Release
set BUILD_PLATFORM="Any CPU"

msbuild /nologo /m /t:restore /p:Configuration=%BUILD_CONFIG%;Platform=%BUILD_PLATFORM% %SOLUTION_FILE%

msbuild /nologo /m /p:Configuration=%BUILD_CONFIG%;Platform=%BUILD_PLATFORM% %SOLUTION_FILE%
msbuild /nologo /m /p:Configuration=%BUILD_CONFIG%;Platform="x86" %SOLUTION_FILE%

git cherry-pick patch-dll-avx2

msbuild /nologo /m /p:Configuration=%BUILD_CONFIG%;Platform=%BUILD_PLATFORM% %SOLUTION_FILE%
msbuild /nologo /m /p:Configuration=%BUILD_CONFIG%;Platform="x86" %SOLUTION_FILE%

git reset HEAD~1
git checkout libzopfli
