
namespace Wabbajack.CLI;
using Wabbajack.CLI.Verbs;

public partial class CommandLineBuilder { 

private static void RegisterAll() {
RegisterCommand<Compile>(Compile.MakeCommand);
RegisterCommand<Decrypt>(Decrypt.MakeCommand);
RegisterCommand<DownloadAll>(DownloadAll.MakeCommand);
RegisterCommand<DownloadCef>(DownloadCef.MakeCommand);
RegisterCommand<DownloadUrl>(DownloadUrl.MakeCommand);
RegisterCommand<DumpZipInfo>(DumpZipInfo.MakeCommand);
RegisterCommand<Encrypt>(Encrypt.MakeCommand);
RegisterCommand<Extract>(Extract.MakeCommand);
RegisterCommand<ForceHeal>(ForceHeal.MakeCommand);
RegisterCommand<GenerateMetricsReports>(GenerateMetricsReports.MakeCommand);
RegisterCommand<HashFile>(HashFile.MakeCommand);
RegisterCommand<HashUrlString>(HashUrlString.MakeCommand);
RegisterCommand<Install>(Install.MakeCommand);
RegisterCommand<InstallCompileInstallVerify>(InstallCompileInstallVerify.MakeCommand);
RegisterCommand<ListCreationClubContent>(ListCreationClubContent.MakeCommand);
RegisterCommand<ListGames>(ListGames.MakeCommand);
RegisterCommand<ListModlists>(ListModlists.MakeCommand);
RegisterCommand<MirrorFile>(MirrorFile.MakeCommand);
RegisterCommand<ModlistReport>(ModlistReport.MakeCommand);
RegisterCommand<SteamDownloadFile>(SteamDownloadFile.MakeCommand);
RegisterCommand<SteamDumpAppInfo>(SteamDumpAppInfo.MakeCommand);
RegisterCommand<SteamLogin>(SteamLogin.MakeCommand);
RegisterCommand<UploadToNexus>(UploadToNexus.MakeCommand);
RegisterCommand<ValidateLists>(ValidateLists.MakeCommand);
RegisterCommand<VFSIndex>(VFSIndex.MakeCommand);
}
}