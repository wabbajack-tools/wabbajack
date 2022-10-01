
namespace Wabbajack.CLI;
using Wabbajack.CLI.Verbs;

public partial class CommandLineBuilder { 

private static void RegisterAll() {
RegisterCommand<Compile>(Compile.Definition, c => ((Compile)c).Run);
RegisterCommand<Decrypt>(Decrypt.Definition, c => ((Decrypt)c).Run);
RegisterCommand<DownloadAll>(DownloadAll.Definition, c => ((DownloadAll)c).Run);
RegisterCommand<DownloadUrl>(DownloadUrl.Definition, c => ((DownloadUrl)c).Run);
RegisterCommand<DumpZipInfo>(DumpZipInfo.Definition, c => ((DumpZipInfo)c).Run);
RegisterCommand<Encrypt>(Encrypt.Definition, c => ((Encrypt)c).Run);
RegisterCommand<Extract>(Extract.Definition, c => ((Extract)c).Run);
RegisterCommand<ForceHeal>(ForceHeal.Definition, c => ((ForceHeal)c).Run);
RegisterCommand<HashFile>(HashFile.Definition, c => ((HashFile)c).Run);
RegisterCommand<HashUrlString>(HashUrlString.Definition, c => ((HashUrlString)c).Run);
RegisterCommand<Install>(Install.Definition, c => ((Install)c).Run);
RegisterCommand<InstallCompileInstallVerify>(InstallCompileInstallVerify.Definition, c => ((InstallCompileInstallVerify)c).Run);
RegisterCommand<ListCreationClubContent>(ListCreationClubContent.Definition, c => ((ListCreationClubContent)c).Run);
RegisterCommand<ListGames>(ListGames.Definition, c => ((ListGames)c).Run);
RegisterCommand<ListModlists>(ListModlists.Definition, c => ((ListModlists)c).Run);
RegisterCommand<MirrorFile>(MirrorFile.Definition, c => ((MirrorFile)c).Run);
RegisterCommand<ModlistReport>(ModlistReport.Definition, c => ((ModlistReport)c).Run);
RegisterCommand<SteamDownloadFile>(SteamDownloadFile.Definition, c => ((SteamDownloadFile)c).Run);
RegisterCommand<SteamDumpAppInfo>(SteamDumpAppInfo.Definition, c => ((SteamDumpAppInfo)c).Run);
RegisterCommand<SteamLogin>(SteamLogin.Definition, c => ((SteamLogin)c).Run);
RegisterCommand<UploadToNexus>(UploadToNexus.Definition, c => ((UploadToNexus)c).Run);
RegisterCommand<ValidateLists>(ValidateLists.Definition, c => ((ValidateLists)c).Run);
RegisterCommand<VFSIndex>(VFSIndex.Definition, c => ((VFSIndex)c).Run);
}
}