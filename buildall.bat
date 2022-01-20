dotnet clean
dotnet restore
dotnet publish Wabbajack\Wabbajack.csproj --runtime win10-x64 --configuration Release /p:Platform=x64 -o M:\Games\wabbajack_files\app --self-contained
dotnet publish Wabbajack.Launcher\Wabbajack.Launcher.csproj --runtime win10-x64 --configuration Release /p:Platform=x64 -o M:\Games\wabbajack_files\launcher --self-contained
"C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /t http://timestamp.sectigo.com M:\Games\wabbajack_files\launcher\Wabbajack.exe
"C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /t http://timestamp.sectigo.com M:\Games\wabbajack_files\app\Wabbajack.exe
"C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /t http://timestamp.sectigo.com M:\Games\wabbajack_files\app\wabbajack-cli.exe