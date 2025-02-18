rmdir /q/s c:\tmp\publish-wj\app
rmdir /q/s c:\tmp\publish-wj\launcher

python scripts\version_extract.py > VERSION.txt
SET /p VERSION=<VERSION.txt
mkdir c:\tmp\publish-wj

dotnet clean
dotnet publish Wabbajack.App.Wpf\Wabbajack.App.Wpf.csproj --framework "net9.0-windows" --runtime win-x64 --configuration Release /p:Platform=x64 -o c:\tmp\publish-wj\app /p:IncludeNativeLibrariesForSelfExtract=true --self-contained  /p:DebugType=embedded 
dotnet publish Wabbajack.Launcher\Wabbajack.Launcher.csproj --framework "net9.0-windows" --runtime win-x64 --configuration Release /p:Platform=x64 -o c:\tmp\publish-wj\launcher /p:PublishSingleFile=true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true --self-contained  /p:DebugType=embedded
dotnet publish Wabbajack.CLI\Wabbajack.CLI.csproj --framework "net9.0" --runtime win-x64 --configuration Release /p:Platform=x64 -o c:\tmp\publish-wj\app\cli  /p:IncludeNativeLibrariesForSelfExtract=true --self-contained  /p:DebugType=embedded

cd C:\tmp\CodeSignTool-v1.3.2-windows\
call CodeSignTool.bat sign -input_file_path c:\tmp\publish-wj\app\Wabbajack.exe -username=%CODE_SIGN_USER% -password=%CODE_SIGN_PASS%
call CodeSignTool.bat sign -input_file_path c:\tmp\publish-wj\launcher\Wabbajack.exe -username=%CODE_SIGN_USER% -password=%CODE_SIGN_PASS%
call CodeSignTool.bat sign -input_file_path c:\tmp\publish-wj\app\cli\wabbajack-cli.exe -username=%CODE_SIGN_USER% -password=%CODE_SIGN_PASS%
cd c:\oss\Wabbajack

REM "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /fd sha256 /tr http://ts.ssl.com /td sha256 /sha1 4f60ccdc98d879537ee3cabda8da56e068f79d3b c:\tmp\publish-wj\app\Wabbajack.exe
REM "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /fd sha256 /tr http://ts.ssl.com /td sha256 /sha1 4f60ccdc98d879537ee3cabda8da56e068f79d3b c:\tmp\publish-wj\launcher\Wabbajack.exe
REM "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /fd sha256 /tr http://ts.ssl.com /td sha256 /sha1 4f60ccdc98d879537ee3cabda8da56e068f79d3b c:\tmp\publish-wj\app\cli\wabbajack-cli.exe
"c:\Program Files\7-Zip\7z.exe" a c:\tmp\publish-wj\%VERSION%.zip c:\tmp\publish-wj\app\*

copy c:\tmp\publish-wj\launcher\Wabbajack.exe c:\tmp\publish-wj\Wabbajack.exe
