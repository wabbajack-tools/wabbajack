rmdir /q/s c:\tmp\publish-wj\cli
rmdir /q/s c:\tmp\publish-wj\launcher

python scripts\version_extract.py > VERSION.txt
SET /p VERSION=<VERSION.txt
mkdir c:\tmp\publish-wj

dotnet clean
dotnet publish Wabbajack.Launcher\Wabbajack.Launcher.csproj --framework "net9.0-windows" --runtime win-x64 --configuration Release /p:Platform=x64 -o c:\tmp\publish-wj\launcher /p:PublishSingleFile=true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true --self-contained  /p:DebugType=embedded
dotnet publish Wabbajack.CLI\Wabbajack.CLI.csproj --framework "net9.0" --runtime win-x64 --configuration Release /p:Platform=x64 -o c:\tmp\publish-wj\cli  /p:IncludeNativeLibrariesForSelfExtract=true --self-contained  /p:DebugType=embedded

d:
cd d:\oss\CodeSignTool\
call CodeSignTool.bat sign -input_file_path c:\tmp\publish-wj\launcher\Wabbajack.exe -username=%CODE_SIGN_USER% -password=%CODE_SIGN_PASS%
call CodeSignTool.bat sign -input_file_path c:\tmp\publish-wj\cli\wabbajack-cli.exe -username=%CODE_SIGN_USER% -password=%CODE_SIGN_PASS%
d:
cd f:\oss\Wabbajack

"c:\Program Files\7-Zip\7z.exe" a c:\tmp\publish-wj\%VERSION%.zip c:\tmp\publish-wj\cli\*

copy c:\tmp\publish-wj\launcher\Wabbajack.exe c:\tmp\publish-wj\Wabbajack.exe
