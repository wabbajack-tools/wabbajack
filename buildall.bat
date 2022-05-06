dotnet clean
dotnet restore
dotnet publish Wabbajack\Wabbajack.csproj --runtime win10-x64 --configuration Release /p:Platform=x64 -o M:\Games\wabbajack_files\app --self-contained
dotnet publish Wabbajack.Launcher\Wabbajack.Launcher.csproj --runtime win10-x64 --configuration Release /p:Platform=x64 -o M:\Games\wabbajack_files\launcher --self-contained
dotnet publish c:\oss\Wabbajack\Wabbajack.CLI\Wabbajack.CLI.csproj --runtime win10-x64 --configuration Release /p:Platform=x64 -o M:\Games\wabbajack_files\cli --self-contained
"C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /t http://timestamp.sectigo.com M:\Games\wabbajack_files\launcher\Wabbajack.exe
"C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /t http://timestamp.sectigo.com M:\Games\wabbajack_files\app\Wabbajack.exe
"C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /t http://timestamp.sectigo.com M:\Games\wabbajack_files\app\wabbajack-cli.exe
"C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /t http://timestamp.sectigo.com M:\Games\wabbajack_files\cli\wabbajack-cli.exe
"c:\Program Files\7-Zip\7z.exe" a m:\Games\wabbajack_files\app.zip m:\Games\wabbajack_files\app\*
"c:\Program Files\7-Zip\7z.exe" a m:\Games\wabbajack_files\cli-3.0.zip m:\Games\wabbajack_files\cli\*
copy m:\Games\wabbajack_files\launcher\Wabbajack.exe m:\Games\wabbajack_files\Wabbajack.exe