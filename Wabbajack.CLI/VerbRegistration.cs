
using Microsoft.Extensions.DependencyInjection;
namespace Wabbajack.CLI;
using Wabbajack.CLI.Verbs;
using Wabbajack.CLI.Builder;

public static class CommandLineBuilderExtensions{ 

public static void AddCLIVerbs(this IServiceCollection services) {
CommandLineBuilder.RegisterCommand<Compile>(Compile.Definition, c => ((Compile)c).Run);
services.AddSingleton<Compile>();
CommandLineBuilder.RegisterCommand<Decrypt>(Decrypt.Definition, c => ((Decrypt)c).Run);
services.AddSingleton<Decrypt>();
CommandLineBuilder.RegisterCommand<DownloadAll>(DownloadAll.Definition, c => ((DownloadAll)c).Run);
services.AddSingleton<DownloadAll>();
CommandLineBuilder.RegisterCommand<DownloadUrl>(DownloadUrl.Definition, c => ((DownloadUrl)c).Run);
services.AddSingleton<DownloadUrl>();
CommandLineBuilder.RegisterCommand<DumpZipInfo>(DumpZipInfo.Definition, c => ((DumpZipInfo)c).Run);
services.AddSingleton<DumpZipInfo>();
CommandLineBuilder.RegisterCommand<Encrypt>(Encrypt.Definition, c => ((Encrypt)c).Run);
services.AddSingleton<Encrypt>();
CommandLineBuilder.RegisterCommand<Extract>(Extract.Definition, c => ((Extract)c).Run);
services.AddSingleton<Extract>();
CommandLineBuilder.RegisterCommand<ForceHeal>(ForceHeal.Definition, c => ((ForceHeal)c).Run);
services.AddSingleton<ForceHeal>();
CommandLineBuilder.RegisterCommand<HashFile>(HashFile.Definition, c => ((HashFile)c).Run);
services.AddSingleton<HashFile>();
CommandLineBuilder.RegisterCommand<HashUrlString>(HashUrlString.Definition, c => ((HashUrlString)c).Run);
services.AddSingleton<HashUrlString>();
CommandLineBuilder.RegisterCommand<IndexNexusMod>(IndexNexusMod.Definition, c => ((IndexNexusMod)c).Run);
services.AddSingleton<IndexNexusMod>();
CommandLineBuilder.RegisterCommand<Install>(Install.Definition, c => ((Install)c).Run);
services.AddSingleton<Install>();
CommandLineBuilder.RegisterCommand<InstallCompileInstallVerify>(InstallCompileInstallVerify.Definition, c => ((InstallCompileInstallVerify)c).Run);
services.AddSingleton<InstallCompileInstallVerify>();
CommandLineBuilder.RegisterCommand<ListCreationClubContent>(ListCreationClubContent.Definition, c => ((ListCreationClubContent)c).Run);
services.AddSingleton<ListCreationClubContent>();
CommandLineBuilder.RegisterCommand<ListGames>(ListGames.Definition, c => ((ListGames)c).Run);
services.AddSingleton<ListGames>();
CommandLineBuilder.RegisterCommand<ListModlists>(ListModlists.Definition, c => ((ListModlists)c).Run);
services.AddSingleton<ListModlists>();
CommandLineBuilder.RegisterCommand<MegaLogin>(MegaLogin.Definition, c => ((MegaLogin)c).Run);
services.AddSingleton<MegaLogin>();
CommandLineBuilder.RegisterCommand<MirrorFile>(MirrorFile.Definition, c => ((MirrorFile)c).Run);
services.AddSingleton<MirrorFile>();
CommandLineBuilder.RegisterCommand<ModlistReport>(ModlistReport.Definition, c => ((ModlistReport)c).Run);
services.AddSingleton<ModlistReport>();
CommandLineBuilder.RegisterCommand<SteamDownloadFile>(SteamDownloadFile.Definition, c => ((SteamDownloadFile)c).Run);
services.AddSingleton<SteamDownloadFile>();
CommandLineBuilder.RegisterCommand<SteamDumpAppInfo>(SteamDumpAppInfo.Definition, c => ((SteamDumpAppInfo)c).Run);
services.AddSingleton<SteamDumpAppInfo>();
CommandLineBuilder.RegisterCommand<SteamLogin>(SteamLogin.Definition, c => ((SteamLogin)c).Run);
services.AddSingleton<SteamLogin>();
CommandLineBuilder.RegisterCommand<UploadToNexus>(UploadToNexus.Definition, c => ((UploadToNexus)c).Run);
services.AddSingleton<UploadToNexus>();
CommandLineBuilder.RegisterCommand<ValidateLists>(ValidateLists.Definition, c => ((ValidateLists)c).Run);
services.AddSingleton<ValidateLists>();
CommandLineBuilder.RegisterCommand<VerifyModlistInstall>(VerifyModlistInstall.Definition, c => ((VerifyModlistInstall)c).Run);
services.AddSingleton<VerifyModlistInstall>();
CommandLineBuilder.RegisterCommand<VFSIndex>(VFSIndex.Definition, c => ((VFSIndex)c).Run);
services.AddSingleton<VFSIndex>();
}
}