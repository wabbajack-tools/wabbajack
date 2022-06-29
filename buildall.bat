rmdir /q/s e:\Games\wabbajack_files
mkdir e:\Games\wabbajack_files
mkdir e:\Games\wabbajack_files\app
mkdir e:\Games\wabbajack_files\cli
dotnet clean
dotnet restore
dotnet publish Wabbajack\Wabbajack.csproj --runtime win10-x64 --configuration Release /p:Platform=x64 -o e:\Games\wabbajack_files\app --self-contained
dotnet publish c:\oss\Wabbajack\Wabbajack.CLI\Wabbajack.CLI.csproj --runtime win10-x64 --configuration Release /p:Platform=x64 -o e:\Games\wabbajack_files\cli --self-contained
"C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /t http://timestamp.sectigo.com e:\Games\wabbajack_files\app\Wabbajack.exe
"C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /t http://timestamp.sectigo.com e:\Games\wabbajack_files\app\wabbajack-cli.exe
"C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe" sign /t http://timestamp.sectigo.com e:\Games\wabbajack_files\cli\wabbajack-cli.exe
"c:\Program Files\7-Zip\7z.exe" a e:\Games\wabbajack_files\app.zip e:\Games\wabbajack_files\app\*
"c:\Program Files\7-Zip\7z.exe" a e:\Games\wabbajack_files\cli-3.0.zip e:\Games\wabbajack_files\cli\*
