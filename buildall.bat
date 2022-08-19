rmdir /q/s c:\tmp\publish-wj\app
rmdir /q/s c:\tmp\publish-wj\launcher

python scripts\version_extract.py > VERSION.txt
SET /p VERSION=<VERSION.txt
mkdir c:\tmp\publish-wj

dotnet clean
dotnet restore
dotnet publish Wabbajack.App.Wpf\Wabbajack.App.Wpf.csproj --runtime win10-x64 --configuration Release /p:Platform=x64 -o c:\tmp\publish-wj\app /p:PublishedTrimmed=true /p:PublishReadyToRun=true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true --self-contained
dotnet publish Wabbajack.Launcher\Wabbajack.Launcher.csproj --runtime win10-x64 --configuration Release /p:Platform=x64 -o c:\tmp\publish-wj\launcher /p:PublishedTrimmed=true /p:PublishReadyToRun=true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true --self-contained
dotnet publish c:\oss\Wabbajack\Wabbajack.CLI\Wabbajack.CLI.csproj --runtime win10-x64 --configuration Release /p:Platform=x64 -o c:\tmp\publish-wj\app --self-contained
"C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /t http://timestamp.sectigo.com c:\tmp\publish-wj\app\Wabbajack.exe
"C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /t http://timestamp.sectigo.com c:\tmp\publish-wj\launcher\Wabbajack.exe
"C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /t http://timestamp.sectigo.com c:\tmp\publish-wj\app\wabbajack-cli.exe
"c:\Program Files\7-Zip\7z.exe" a c:\tmp\publish-wj\%VERSION%.zip c:\tmp\publish-wj\app\*

copy c:\tmp\publish-wj\launcher\Wabbajack.exe c:\tmp\publish-wj\Wabbajack.exe
