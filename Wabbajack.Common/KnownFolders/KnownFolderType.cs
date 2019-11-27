using System;
using System.Reflection;

namespace Wabbajack.Common.KnownFolders
{
    /// <summary>
    /// Represents the list of standard folders registered with the system. These folders are installed with Windows
    /// Vista and later operating systems, and a computer will have only folders appropriate to it installed.
    /// </summary>
    /// <msdn-id>dd378457</msdn-id>
    public enum KnownFolderType
    {
        /// <summary>
        /// The per-user Account Pictures folder. Introduced in Windows 8.
        /// Defaults to &quot;%APPDATA%\Microsoft\Windows\AccountPictures&quot;.
        /// </summary>
        [KnownFolderGuid("008CA0B1-55B4-4C56-B8A8-4DE4B299D3BE")]
        AccountPictures,

        /// <summary>
        /// The per-user Administrative Tools folder.
        /// Defaults to &quot;%APPDATA%\Microsoft\Windows\Start Menu\Programs\Administrative Tools&quot;.
        /// </summary>
        [KnownFolderGuid("724EF170-A42D-4FEF-9F26-B60E846FBA4F")]
        AdminTools,

        /// <summary>
        /// The per-user app desktop folder, used internally by .NET applications to perform cross-platform app
        /// functionality. Introduced in Windows 10.
        /// Defaults to &quot;%LOCALAPPDATA%\Desktop&quot;.
        /// </summary>
        [KnownFolderGuid("B2C5E279-7ADD-439F-B28C-C41FE1BBF672")]
        AppDataDesktop,

        /// <summary>
        /// The per-user app documents folder, used internally by .NET applications to perform cross-platform app
        /// functionality. Introduced in Windows 10.
        /// Defaults to &quot;%LOCALAPPDATA%\Documents&quot;.
        /// </summary>
        [KnownFolderGuid("7BE16610-1F7F-44AC-BFF0-83E15F2FFCA1")]
        AppDataDocuments,

        /// <summary>
        /// The per-user app favorites folder, used internally by .NET applications to perform cross-platform app
        /// functionality. Introduced in Windows 10.
        /// Defaults to &quot;%LOCALAPPDATA%\Favorites&quot;.
        /// </summary>
        [KnownFolderGuid("7CFBEFBC-DE1F-45AA-B843-A542AC536CC9")]
        AppDataFavorites,

        /// <summary>
        /// The per-user app program data folder, used internally by .NET applications to perform cross-platform app
        /// functionality. Introduced in Windows 10.
        /// Defaults to &quot;%LOCALAPPDATA%\ProgramData&quot;.
        /// </summary>
        [KnownFolderGuid("559D40A3-A036-40FA-AF61-84CB430A4D34")]
        AppDataProgramData,

        /// <summary>
        /// The per-user Application Shortcuts folder. Introduced in Windows 8.
        /// Defaults to &quot;%LOCALAPPDATA%\Microsoft\Windows\Application Shortcuts&quot;.
        /// </summary>
        [KnownFolderGuid("A3918781-E5F2-4890-B3D9-A7E54332328C")]
        ApplicationShortcuts,

        /// <summary>
        /// The per-user Camera Roll folder. Introduced in Windows 8.1.
        /// Defaults to &quot;.%USERPROFILE%\Pictures\Camera Roll&quot;.
        /// </summary>
        [KnownFolderGuid("AB5FB87B-7CE2-4F83-915D-550846C9537B")]
        CameraRoll,

        /// <summary>
        /// The per-user Temporary Burn Folder.
        /// Defaults to &quot;%LOCALAPPDATA%\Microsoft\Windows\Burn\Burn&quot;.
        /// </summary>
        [KnownFolderGuid("9E52AB10-F80D-49DF-ACB8-4330F5687855")]
        CDBurning,

        /// <summary>
        /// The common Administrative Tools folder.
        /// Defaults to &quot;%ALLUSERSPROFILE%\Microsoft\Windows\Start Menu\Programs\Administrative Tools&quot;.
        /// </summary>
        [KnownFolderGuid("D0384E7D-BAC3-4797-8F14-CBA229B392B5")]
        CommonAdminTools,

        /// <summary>
        /// The common OEM Links folder.
        /// Defaults to &quot;%ALLUSERSPROFILE%\OEM Links&quot;.
        /// </summary>
        [KnownFolderGuid("C1BAE2D0-10DF-4334-BEDD-7AA20B227A9D")]
        CommonOemLinks,

        /// <summary>
        /// The common Programs folder.
        /// Defaults to &quot;%ALLUSERSPROFILE%\Microsoft\Windows\Start Menu\Programs&quot;.
        /// </summary>
        [KnownFolderGuid("0139D44E-6AFE-49F2-8690-3DAFCAE6FFB8")]
        CommonPrograms,

        /// <summary>
        /// The common Start Menu folder.
        /// Defaults to &quot;%ALLUSERSPROFILE%\Microsoft\Windows\Start Menu&quot;.
        /// </summary>
        [KnownFolderGuid("A4115719-D62E-491D-AA7C-E74B8BE3B067")]
        CommonStartMenu,

        /// <summary>
        /// The common Startup folder.
        /// Defaults to &quot;%ALLUSERSPROFILE%\Microsoft\Windows\Start Menu\Programs\StartUp&quot;.
        /// </summary>
        [KnownFolderGuid("82A5EA35-D9CD-47C5-9629-E15D2F714E6E")]
        CommonStartup,

        /// <summary>
        /// The common Templates folder.
        /// Defaults to &quot;%ALLUSERSPROFILE%\Microsoft\Windows\Templates&quot;.
        /// </summary>
        [KnownFolderGuid("B94237E7-57AC-4347-9151-B08C6C32D1F7")]
        CommonTemplates,

        /// <summary>
        /// The per-user Contacts folder. Introduced in Windows Vista.
        /// Defaults to &quot;%USERPROFILE%\Contacts&quot;.
        /// </summary>
        [KnownFolderGuid("56784854-C6CB-462B-8169-88E350ACB882")]
        Contacts,

        /// <summary>
        /// The per-user Cookies folder.
        /// Defaults to &quot;%APPDATA%\Microsoft\Windows\Cookies&quot;.
        /// </summary>
        [KnownFolderGuid("2B0F765D-C0E9-4171-908E-08A611B84FF6")]
        Cookies,

        /// <summary>
        /// The per-user Desktop folder.
        /// Defaults to &quot;%USERPROFILE%\Desktop&quot;.
        /// </summary>
        [KnownFolderGuid("B4BFCC3A-DB2C-424C-B029-7FE99A87C641")]
        Desktop,

        /// <summary>
        /// The common DeviceMetadataStore folder. Introduced in Windows 7.
        /// Defaults to &quot;%ALLUSERSPROFILE%\Microsoft\Windows\DeviceMetadataStore&quot;.
        /// </summary>
        [KnownFolderGuid("5CE4A5E9-E4EB-479D-B89F-130C02886155")]
        DeviceMetadataStore,

        /// <summary>
        /// The per-user Documents folder.
        /// Defaults to &quot;%USERPROFILE%\Documents&quot;.
        /// </summary>
        [KnownFolderGuid("FDD39AD0-238F-46AF-ADB4-6C85480369C7")]
        Documents,

        /// <summary>
        /// The per-user Documents library. Introduced in Windows 7.
        /// Defaults to &quot;%APPDATA%\Microsoft\Windows\Libraries\Documents.library-ms&quot;.
        /// </summary>
        [KnownFolderGuid("7B0DB17D-9CD2-4A93-9733-46CC89022E7C")]
        DocumentsLibrary,

        /// <summary>
        /// The per-user localized Documents folder.
        /// Defaults to &quot;%USERPROFILE%\Documents&quot;.
        /// </summary>
        [KnownFolderGuid("F42EE2D3-909F-4907-8871-4C22FC0BF756")]
        DocumentsLocalized,

        /// <summary>
        /// The per-user Downloads folder.
        /// Defaults to &quot;%USERPROFILE%\Downloads&quot;.
        /// </summary>
        [KnownFolderGuid("374DE290-123F-4565-9164-39C4925E467B")]
        Downloads,

        /// <summary>
        /// The per-user localized Downloads folder.
        /// Defaults to &quot;%USERPROFILE%\Downloads&quot;.
        /// </summary>
        [KnownFolderGuid("7d83ee9b-2244-4e70-b1f5-5393042af1e4")]
        DownloadsLocalized,

        /// <summary>
        /// The per-user Favorites folder.
        /// Defaults to &quot;%USERPROFILE%\Favorites&quot;.
        /// </summary>
        [KnownFolderGuid("1777F761-68AD-4D8A-87BD-30B759FA33DD")]
        Favorites,

        /// <summary>
        /// The fixed Fonts folder.
        /// Points to &quot;%WINDIR%\Fonts&quot;.
        /// </summary>
        [KnownFolderGuid("FD228CB7-AE11-4AE3-864C-16F3910AB8FE")]
        Fonts,

        /// <summary>
        /// The per-user GameExplorer folder. Introduced in Windows Vista.
        /// Defaults to &quot;%LOCALAPPDATA%\Microsoft\Windows\GameExplorer&quot;.
        /// </summary>
        [KnownFolderGuid("054FAE61-4DD8-4787-80B6-090220C4B700")]
        GameTasks,

        /// <summary>
        /// The per-user History folder.
        /// Defaults to &quot;%LOCALAPPDATA%\Microsoft\Windows\History&quot;.
        /// </summary>
        [KnownFolderGuid("D9DC8A3B-B784-432E-A781-5A1130A75963")]
        History,

        /// <summary>
        /// The per-user ImplicitAppShortcuts folder. Introduced in Windows 7.
        /// Defaults to &quot;%APPDATA%\Microsoft\Internet Explorer\Quick Launch\User Pinned\ImplicitAppShortcuts&quot;.
        /// </summary>
        [KnownFolderGuid("BCB5256F-79F6-4CEE-B725-DC34E402FD46")]
        ImplicitAppShortcuts,

        /// <summary>
        /// The per-user Temporary Internet Files folder.
        /// Defaults to &quot;%LOCALAPPDATA%\Microsoft\Windows\Temporary Internet Files&quot;.
        /// </summary>
        [KnownFolderGuid("352481E8-33BE-4251-BA85-6007CAEDCF9D")]
        InternetCache,

        /// <summary>
        /// The per-user Libraries folder. Introduced in Windows 7.
        /// Defaults to &quot;%APPDATA%\Microsoft\Windows\Libraries&quot;.
        /// </summary>
        [KnownFolderGuid("1B3EA5DC-B587-4786-B4EF-BD1DC332AEAE")]
        Libraries,

        /// <summary>
        /// The per-user Links folder.
        /// Defaults to &quot;%USERPROFILE%\Links&quot;.
        /// </summary>
        [KnownFolderGuid("BFB9D5E0-C6A9-404C-B2B2-AE6DB6AF4968")]
        Links,

        /// <summary>
        /// The per-user Local folder.
        /// Defaults to &quot;%LOCALAPPDATA%&quot; (&quot;%USERPROFILE%\AppData\Local&quot;)&quot;.
        /// </summary>
        [KnownFolderGuid("F1B32785-6FBA-4FCF-9D55-7B8E7F157091")]
        LocalAppData,

        /// <summary>
        /// The per-user LocalLow folder.
        /// Defaults to &quot;%USERPROFILE%\AppData\LocalLow&quot;.
        /// </summary>
        [KnownFolderGuid("A520A1A4-1780-4FF6-BD18-167343C5AF16")]
        LocalAppDataLow,

        /// <summary>
        /// The fixed LocalizedResourcesDir folder.
        /// Points to &quot;%WINDIR%\resources\0409&quot; (code page).
        /// </summary>
        [KnownFolderGuid("2A00375E-224C-49DE-B8D1-440DF7EF3DDC")]
        LocalizedResourcesDir,

        /// <summary>
        /// The per-user Music folder.
        /// Defaults to &quot;%USERPROFILE%\Music&quot;.
        /// </summary>
        [KnownFolderGuid("4BD8D571-6D19-48D3-BE97-422220080E43")]
        Music,

        /// <summary>
        /// The per-user Music library. Introduced in Windows 7.
        /// Defaults to &quot;%APPDATA%\Microsoft\Windows\Libraries\Music.library-ms&quot;.
        /// </summary>
        [KnownFolderGuid("2112AB0A-C86A-4FFE-A368-0DE96E47012E")]
        MusicLibrary,

        /// <summary>
        /// The per-user localized Music folder.
        /// Defaults to &quot;%USERPROFILE%\Music&quot;.
        /// </summary>
        [KnownFolderGuid("A0C69A99-21C8-4671-8703-7934162FCF1D")]
        MusicLocalized,

        /// <summary>
        /// The per-user Network Shortcuts folder.
        /// Defaults to &quot;%APPDATA%\Microsoft\Windows\Network Shortcuts&quot;.
        /// </summary>
        [KnownFolderGuid("C5ABBF53-E17F-4121-8900-86626FC2C973")]
        NetHood,

        /// <summary>
        /// The per-user 3D Objects folder. Introduced in Windows 10.
        /// Defaults to &quot;%USERPROFILE%\3D Objects&quot;.
        /// </summary>
        [KnownFolderGuid("31C0DD25-9439-4F12-BF41-7FF4EDA38722")]
        Objects3D,

        /// <summary>
        /// The per-user Original Images folder. Introduced in Windows Vista.
        /// Defaults to &quot;%LOCALAPPDATA%\Microsoft\Windows Photo Gallery\Original Images&quot;.
        /// </summary>
        [KnownFolderGuid("2C36C0AA-5812-4B87-BFD0-4CD0DFB19B39")]
        OriginalImages,

        /// <summary>
        /// The per-user Slide Shows folder. Introduced in Windows Vista.
        /// Defaults to &quot;%USERPROFILE%\Pictures\Slide Shows&quot;.
        /// </summary>
        [KnownFolderGuid("69D2CF90-FC33-4FB7-9A0C-EBB0F0FCB43C")]
        PhotoAlbums,

        /// <summary>
        /// The per-user Pictures library. Introduced in Windows 7.
        /// Defaults to &quot;%APPDATA%\Microsoft\Windows\Libraries\Pictures.library-ms&quot;.
        /// </summary>
        [KnownFolderGuid("A990AE9F-A03B-4E80-94BC-9912D7504104")]
        PicturesLibrary,

        /// <summary>
        /// The per-user Pictures folder.
        /// Defaults to &quot;%USERPROFILE%\Pictures&quot;.
        /// </summary>
        [KnownFolderGuid("33E28130-4E1E-4676-835A-98395C3BC3BB")]
        Pictures,

        /// <summary>
        /// The per-user localized Pictures folder.
        /// Defaults to &quot;%USERPROFILE%\Pictures&quot;.
        /// </summary>
        [KnownFolderGuid("0DDD015D-B06C-45D5-8C4C-F59713854639")]
        PicturesLocalized,

        /// <summary>
        /// The per-user Playlists folder.
        /// Defaults to &quot;%USERPROFILE%\Music\Playlists&quot;.
        /// </summary>
        [KnownFolderGuid("DE92C1C7-837F-4F69-A3BB-86E631204A23")]
        Playlists,

        /// <summary>
        /// The per-user Printer Shortcuts folder.
        /// Defaults to &quot;%APPDATA%\Microsoft\Windows\Printer Shortcuts&quot;.
        /// </summary>
        [KnownFolderGuid("9274BD8D-CFD1-41C3-B35E-B13F55A758F4")]
        PrintHood,

        /// <summary>
        /// The fixed user profile folder.
        /// Defaults to &quot;%USERPROFILE%&quot; (&quot;%SYSTEMDRIVE%\USERS\%USERNAME%&quot;)&quot;.
        /// </summary>
        [KnownFolderGuid("5E6C858F-0E22-4760-9AFE-EA3317B67173")]
        Profile,

        /// <summary>
        /// The fixed ProgramData folder.
        /// Points to &quot;%ALLUSERSPROFILE%&quot; (&quot;%PROGRAMDATA%&quot;,
        /// &quot;%SYSTEMDRIVE%\ProgramData&quot;).
        /// </summary>
        [KnownFolderGuid("62AB5D82-FDC1-4DC3-A9DD-070D1D495D97")]
        ProgramData,

        /// <summary>
        /// The fixed Program Files folder.
        /// This is the same as the <see cref="ProgramFilesX86"/> known folder in 32-bit applications or the
        /// <see cref="ProgramFilesX64"/> known folder in 64-bit applications.
        /// Points to %SYSTEMDRIVE%\Program Files on a 32-bit operating system or in 64-bit applications on a 64-bit
        /// operating system and to %SYSTEMDRIVE%\Program Files (x86) in 32-bit applications on a 64-bit operating
        /// system.
        /// </summary>
        [KnownFolderGuid("905E63B6-C1BF-494E-B29C-65B732D3D21A")]
        ProgramFiles,

        /// <summary>
        /// The fixed Program Files folder (64-bit forced).
        /// This known folder is unsupported in 32-bit applications.
        /// Points to %SYSTEMDRIVE%\Program Files.
        /// </summary>
        [KnownFolderGuid("6D809377-6AF0-444B-8957-A3773F02200E")]
        ProgramFilesX64,

        /// <summary>
        /// The fixed Program Files folder (32-bit forced).
        /// This is the same as the <see cref="ProgramFiles"/> known folder in 32-bit applications.
        /// Points to &quot;%SYSTEMDRIVE%\Program Files&quot; on a 32-bit operating system and to
        /// &quot;%SYSTEMDRIVE%\Program Files (x86)&quot; on a 64-bit operating system.
        /// </summary>
        [KnownFolderGuid("7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E")]
        ProgramFilesX86,

        /// <summary>
        /// The fixed Common Files folder.
        /// This is the same as the <see cref="ProgramFilesCommonX86"/> known folder in 32-bit applications or the
        /// <see cref="ProgramFilesCommonX64"/> known folder in 64-bit applications.
        /// Points to&quot; %PROGRAMFILES%\Common Files&quot; on a 32-bit operating system or in 64-bit applications on
        /// a 64-bit operating system and to &quot;%PROGRAMFILES(X86)%\Common Files&quot; in 32-bit applications on a
        /// 64-bit operating system.
        /// </summary>
        [KnownFolderGuid("F7F1ED05-9F6D-47A2-AAAE-29D317C6F066")]
        ProgramFilesCommon,

        /// <summary>
        /// The fixed Common Files folder (64-bit forced).
        /// This known folder is unsupported in 32-bit applications.
        /// Points to &quot;%PROGRAMFILES%\Common Files&quot;.
        /// </summary>
        [KnownFolderGuid("6365D5A7-0F0D-45E5-87F6-0DA56B6A4F7D")]
        ProgramFilesCommonX64,

        /// <summary>
        /// The fixed Common Files folder (32-bit forced).
        /// This is the same as the <see cref="ProgramFilesCommon"/> known folder in 32-bit applications.
        /// Points to &quot;%PROGRAMFILES%\Common Files&quot; on a 32-bit operating system and to
        /// &quot;%PROGRAMFILES(X86)%\Common Files&quot; on a 64-bit operating system.
        /// </summary>
        [KnownFolderGuid("DE974D24-D9C6-4D3E-BF91-F4455120B917")]
        ProgramFilesCommonX86,

        /// <summary>
        /// The per-user Programs folder.
        /// Defaults to &quot;%APPDATA%\Microsoft\Windows\Start Menu\Programs&quot;.
        /// </summary>
        [KnownFolderGuid("A77F5D77-2E2B-44C3-A6A2-ABA601054A51")]
        Programs,

        /// <summary>
        /// The fixed Public folder. Introduced in Windows Vista.
        /// Defaults to &quot;%PUBLIC%&quot; (&quot;%SYSTEMDRIVE%\Users\Public)&quot;.
        /// </summary>
        [KnownFolderGuid("DFDF76A2-C82A-4D63-906A-5644AC457385")]
        Public,

        /// <summary>
        /// The common Public Desktop folder.
        /// Defaults to &quot;%PUBLIC%\Desktop&quot;.
        /// </summary>
        [KnownFolderGuid("C4AA340D-F20F-4863-AFEF-F87EF2E6BA25")]
        PublicDesktop,

        /// <summary>
        /// The common Public Documents folder.
        /// Defaults to &quot;%PUBLIC%\Documents&quot;.
        /// </summary>
        [KnownFolderGuid("ED4824AF-DCE4-45A8-81E2-FC7965083634")]
        PublicDocuments,

        /// <summary>
        /// The common Public Downloads folder. Introduced in Windows Vista.
        /// Defaults to &quot;%PUBLIC%\Downloads&quot;.
        /// </summary>
        [KnownFolderGuid("3D644C9B-1FB8-4F30-9B45-F670235F79C0")]
        PublicDownloads,

        /// <summary>
        /// The common GameExplorer folder. Introduced in Windows Vista.
        /// Defaults to &quot;%ALLUSERSPROFILE%\Microsoft\Windows\GameExplorer&quot;.
        /// </summary>
        [KnownFolderGuid("DEBF2536-E1A8-4C59-B6A2-414586476AEA")]
        PublicGameTasks,

        /// <summary>
        /// The common Libraries folder. Introduced in Windows 7.
        /// Defaults to &quot;%ALLUSERSPROFILE%\Microsoft\Windows\Libraries&quot;.
        /// </summary>
        [KnownFolderGuid("48DAF80B-E6CF-4F4E-B800-0E69D84EE384")]
        PublicLibraries,

        /// <summary>
        /// The common Public Music folder.
        /// Defaults to &quot;%PUBLIC%\Music&quot;.
        /// </summary>
        [KnownFolderGuid("3214FAB5-9757-4298-BB61-92A9DEAA44FF")]
        PublicMusic,

        /// <summary>
        /// The common Public Pictures folder.
        /// Defaults to &quot;%PUBLIC%\Pictures&quot;.
        /// </summary>
        [KnownFolderGuid("B6EBFB86-6907-413C-9AF7-4FC2ABF07CC5")]
        PublicPictures,

        /// <summary>
        /// The common Ringtones folder. Introduced in Windows 7.
        /// Defaults to &quot;%ALLUSERSPROFILE%\Microsoft\Windows\Ringtones&quot;.
        /// </summary>
        [KnownFolderGuid("E555AB60-153B-4D17-9F04-A5FE99FC15EC")]
        PublicRingtones,

        /// <summary>
        /// The common Public Account Pictures folder. Introduced in Windows 8.
        /// Defaults to &quot;%PUBLIC%\AccountPictures&quot;.
        /// </summary>
        [KnownFolderGuid("0482AF6C-08F1-4C34-8C90-E17EC98B1E17")]
        PublicUserTiles,

        /// <summary>
        /// The common Public Videos folder.
        /// Defaults to &quot;%PUBLIC%\Videos&quot;.
        /// </summary>
        [KnownFolderGuid("2400183A-6185-49FB-A2D8-4A392A602BA3")]
        PublicVideos,

        /// <summary>
        /// The per-user Quick Launch folder.
        /// Defaults to &quot;%APPDATA%\Microsoft\Internet Explorer\Quick Launch&quot;.
        /// </summary>
        [KnownFolderGuid("52A4F021-7B75-48A9-9F6B-4B87A210BC8F")]
        QuickLaunch,

        /// <summary>
        /// The per-user Recent Items folder.
        /// Defaults to &quot;%APPDATA%\Microsoft\Windows\Recent&quot;.
        /// </summary>
        [KnownFolderGuid("AE50C081-EBD2-438A-8655-8A092E34987A")]
        Recent,

        /// <summary>
        /// The common Recorded TV library. Introduced in Windows 7.
        /// Defaults to &quot;%PUBLIC%\RecordedTV.library-ms&quot;.
        /// </summary>
        [KnownFolderGuid("1A6FDBA2-F42D-4358-A798-B74D745926C5")]
        RecordedTVLibrary,

        /// <summary>
        /// The fixed Resources folder.
        /// Points to &quot;%WINDIR%\Resources&quot;.
        /// </summary>
        [KnownFolderGuid("8AD10C31-2ADB-4296-A8F7-E4701232C972")]
        ResourceDir,

        /// <summary>
        /// The per-user Ringtones folder. Introduced in Windows 7.
        /// Defaults to &quot;%LOCALAPPDATA%\Microsoft\Windows\Ringtones&quot;.
        /// </summary>
        [KnownFolderGuid("C870044B-F49E-4126-A9C3-B52A1FF411E8")]
        Ringtones,

        /// <summary>
        /// The per-user Roaming folder.
        /// Defaults to &quot;%APPDATA%&quot; (&quot;%USERPROFILE%\AppData\Roaming&quot;).
        /// </summary>
        [KnownFolderGuid("3EB685DB-65F9-4CF6-A03A-E3EF65729F3D")]
        RoamingAppData,

        /// <summary>
        /// The per-user RoamedTileImages folder. Introduced in Windows 8.
        /// Defaults to &quot;%LOCALAPPDATA%\Microsoft\Windows\RoamedTileImages&quot;.
        /// </summary>
        [KnownFolderGuid("AAA8D5A5-F1D6-4259-BAA8-78E7EF60835E")]
        RoamedTileImages,

        /// <summary>
        /// The per-user RoamingTiles folder. Introduced in Windows 8.
        /// Defaults to &quot;%LOCALAPPDATA%\Microsoft\Windows\RoamingTiles&quot;.
        /// </summary>
        [KnownFolderGuid("00BCFC5A-ED94-4E48-96A1-3F6217F21990")]
        RoamingTiles,

        /// <summary>
        /// The common Sample Music folder.
        /// Defaults to &quot;%PUBLIC%\Music\Sample Music&quot;.
        /// </summary>
        [KnownFolderGuid("B250C668-F57D-4EE1-A63C-290EE7D1AA1F")]
        SampleMusic,

        /// <summary>
        /// The common Sample Pictures folder.
        /// Defaults to &quot;%PUBLIC%\Pictures\Sample Pictures&quot;.
        /// </summary>
        [KnownFolderGuid("C4900540-2379-4C75-844B-64E6FAF8716B")]
        SamplePictures,

        /// <summary>
        /// The common Sample Playlists folder. Introduced in Windows Vista.
        /// Defaults to &quot;%PUBLIC%\Music\Sample Playlists&quot;.
        /// </summary>
        [KnownFolderGuid("15CA69B3-30EE-49C1-ACE1-6B5EC372AFB5")]
        SamplePlaylists,

        /// <summary>
        /// The common Sample Videos folder.
        /// Defaults to &quot;%PUBLIC%\Videos\Sample Videos&quot;.
        /// </summary>
        [KnownFolderGuid("859EAD94-2E85-48AD-A71A-0969CB56A6CD")]
        SampleVideos,

        /// <summary>
        /// The per-user Saved Games folder. Introduced in Windows Vista.
        /// Defaults to &quot;%USERPROFILE%\Saved Games&quot;.
        /// </summary>
        [KnownFolderGuid("4C5C32FF-BB9D-43B0-B5B4-2D72E54EAAA4")]
        SavedGames,

        /// <summary>
        /// The per-user Searches folder.
        /// Defaults to &quot;%USERPROFILE%\Searches&quot;.
        /// </summary>
        [KnownFolderGuid("7D1D3A04-DEBB-4115-95CF-2F29DA2920DA")]
        SavedSearches,

        /// <summary>
        /// The per-user Screenshots folder. Introduced in Windows 8.
        /// Defaults to &quot;%USERPROFILE%\Pictures\Screenshots&quot;.
        /// </summary>
        [KnownFolderGuid("B7BEDE81-DF94-4682-A7D8-57A52620B86F")]
        Screenshots,

        /// <summary>
        /// The per-user History folder. Introduced in Windows 8.1.
        /// Defaults to &quot;%LOCALAPPDATA%\Microsoft\Windows\ConnectedSearch\History&quot;.
        /// </summary>
        [KnownFolderGuid("0D4C3DB6-03A3-462F-A0E6-08924C41B5D4")]
        SearchHistory,

        /// <summary>
        /// The per-user Templates folder. Introduced in Windows 8.1.
        /// Defaults to &quot;%LOCALAPPDATA%\Microsoft\Windows\ConnectedSearch\Templates&quot;.
        /// </summary>
        [KnownFolderGuid("7E636BFE-DFA9-4D5E-B456-D7B39851D8A9")]
        SearchTemplates,

        /// <summary>
        /// The per-user SendTo folder.
        /// Defaults to &quot;%APPDATA%\Microsoft\Windows\SendTo&quot;.
        /// </summary>
        [KnownFolderGuid("8983036C-27C0-404B-8F08-102D10DCFD74")]
        SendTo,

        /// <summary>
        /// The common Gadgets folder. Introduced in Windows 7.
        /// Defaults to &quot;%ProgramFiles%\Windows Sidebar\Gadgets&quot;.
        /// </summary>
        [KnownFolderGuid("7B396E54-9EC5-4300-BE0A-2482EBAE1A26")]
        SidebarDefaultParts,

        /// <summary>
        /// The per-user Gadgets folder. Introduced in Windows 7.
        /// Defaults to &quot;%LOCALAPPDATA%\Microsoft\Windows Sidebar\Gadgets&quot;.
        /// </summary>
        [KnownFolderGuid("A75D362E-50FC-4FB7-AC2C-A8BEAA314493")]
        SidebarParts,

        /// <summary>
        /// The per-user OneDrive folder. Introduced in Windows 8.1.
        /// Defaults to &quot;%USERPROFILE%\OneDrive&quot;.
        /// </summary>
        [KnownFolderGuid("A52BBA46-E9E1-435F-B3D9-28DAA648C0F6")]
        SkyDrive,

        /// <summary>
        /// The per-user OneDrive Camera Roll folder. Introduced in Windows 8.1.
        /// Defaults to &quot;%USERPROFILE%\OneDrive\Pictures\Camera Roll&quot;.
        /// </summary>
        [KnownFolderGuid("767E6811-49CB-4273-87C2-20F355E1085B")]
        SkyDriveCameraRoll,

        /// <summary>
        /// The per-user OneDrive Documents folder. Introduced in Windows 8.1.
        /// Defaults to &quot;%USERPROFILE%\OneDrive\Documents&quot;.
        /// </summary>
        [KnownFolderGuid("24D89E24-2F19-4534-9DDE-6A6671FBB8FE")]
        SkyDriveDocuments,

        /// <summary>
        /// The per-user OneDrive Pictures folder. Introduced in Windows 8.1.
        /// Defaults to &quot;%USERPROFILE%\OneDrive\Pictures&quot;.
        /// </summary>
        [KnownFolderGuid("339719B5-8C47-4894-94C2-D8F77ADD44A6")]
        SkyDrivePictures,

        /// <summary>
        /// The per-user Start Menu folder.
        /// Defaults to &quot;%APPDATA%\Microsoft\Windows\Start Menu&quot;.
        /// </summary>
        [KnownFolderGuid("625B53C3-AB48-4EC1-BA1F-A1EF4146FC19")]
        StartMenu,

        /// <summary>
        /// The per-user Startup folder.
        /// Defaults to &quot;%APPDATA%\Microsoft\Windows\Start Menu\Programs\StartUp&quot;.
        /// </summary>
        [KnownFolderGuid("B97D20BB-F46A-4C97-BA10-5E3608430854")]
        Startup,

        /// <summary>
        /// The fixed System32 folder.
        /// This is the same as the <see cref="SystemX86"/> known folder in 32-bit applications.
        /// Points to &quot;%WINDIR%\system32&quot; on 32-bit operating systems or in 64-bit applications on a 64-bit
        /// operating system and to &quot;%WINDIR%\syswow64&quot; in 32-bit applications on a 64-bit operating system.
        /// </summary>
        [KnownFolderGuid("1AC14E77-02E7-4E5D-B744-2EB1AE5198B7")]
        System,

        /// <summary>
        /// The fixed System32 folder (32-bit forced).
        /// This is the same as the <see cref="System"/> known folder in 32-bit applications.
        /// Points to &quot;%WINDIR%\syswow64&quot; in 64-bit applications or in 32-bit applications on a 64-bit
        /// operating system and to &quot;%WINDIR%\system32&quot; on 32-bit operating systems.
        /// </summary>
        [KnownFolderGuid("D65231B0-B2F1-4857-A4CE-A8E7C6EA7D27")]
        SystemX86,

        /// <summary>
        /// The per-user Templates folder.
        /// Defaults to &quot;%APPDATA%\Microsoft\Windows\Templates&quot;.
        /// </summary>
        [KnownFolderGuid("A63293E8-664E-48DB-A079-DF759E0509F7")]
        Templates,

        /// <summary>
        /// The per-user User Pinned folder. Introduced in Windows 7.
        /// Defaults to &quot;%APPDATA%\Microsoft\Internet Explorer\Quick Launch\User Pinned&quot;.
        /// </summary>
        [KnownFolderGuid("9E3995AB-1F9C-4F13-B827-48B24B6C7174")]
        UserPinned,

        /// <summary>
        /// The fixed Users folder. Introduced in Windows Vista.
        /// Points to &quot;%SYSTEMDRIVE%\Users&quot;.
        /// </summary>
        [KnownFolderGuid("0762D272-C50A-4BB0-A382-697DCD729B80")]
        UserProfiles,

        /// <summary>
        /// The per-user Programs folder. Introduced in Windows 7.
        /// Defaults to &quot;%LOCALAPPDATA%\Programs.&quot;.
        /// </summary>
        [KnownFolderGuid("5CD7AEE2-2219-4A67-B85D-6C9CE15660CB")]
        UserProgramFiles,

        /// <summary>
        /// The per-user common Programs folder. INtroduced in Windows 7.
        /// Defaults to &quot;%LOCALAPPDATA%\Programs\Common&quot;.
        /// </summary>
        [KnownFolderGuid("BCBD3057-CA5C-4622-B42D-BC56DB0AE516")]
        UserProgramFilesCommon,

        /// <summary>
        /// The per-user Videos folder.
        /// Defaults to &quot;%USERPROFILE%\Videos&quot;.
        /// </summary>
        [KnownFolderGuid("18989B1D-99B5-455B-841C-AB7C74E4DDFC")]
        Videos,

        /// <summary>
        /// The per-user Videos library. Introduced in Windows 7.
        /// Defaults to &quot;%APPDATA%\Microsoft\Windows\Libraries\Videos.library-ms&quot;.
        /// </summary>
        [KnownFolderGuid("491E922F-5643-4AF4-A7EB-4E7A138D8174")]
        VideosLibrary,

        /// <summary>
        /// The per-user localized Videos folder.
        /// Defaults to &quot;%USERPROFILE%\Videos&quot;.
        /// </summary>
        [KnownFolderGuid("35286A68-3C57-41A1-BBB1-0EAE73D76C95")]
        VideosLocalized,

        /// <summary>
        /// The fixed Windows folder.
        /// Points to &quot;%WINDIR%&quot;.
        /// </summary>
        [KnownFolderGuid("F38BF404-1D43-42F2-9305-67DE0B28FC23")]
        Windows
    }

    /// <summary>
    /// Represents extension methods for the <see cref="KnownFolderType"/> type.
    /// </summary>
    internal static class KnownFolderTypeExtensions
    {
        // ---- METHODS (INTERNAL) -------------------------------------------------------------------------------------

        /// <summary>
        /// Gets the <see cref="Guid"/> with which the <see cref="KnownFolderType"/> enumeration member has been
        /// decorated.
        /// </summary>
        /// <param name="value">The decorated <see cref="KnownFolderType"/> enumeration member.</param>
        /// <returns>The <see cref="Guid"/> of the <see cref="KnownFolderType"/>.</returns>
        internal static Guid GetGuid(this KnownFolderType value)
        {
            FieldInfo member = typeof(KnownFolderType).GetField(value.ToString());
            object[] attributes = member.GetCustomAttributes(typeof(KnownFolderGuidAttribute), false);
            KnownFolderGuidAttribute guidAttribute = (KnownFolderGuidAttribute)attributes[0];
            return guidAttribute.Guid;
        }
    }

    /// <summary>
    /// Represents an attribute to decorate the members of the <see cref="KnownFolderType"/> enumeration with their
    /// corresponding <see cref="Guid"/> on the Windows system.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    internal class KnownFolderGuidAttribute : Attribute
    {
        // ---- CONSTRUCTORS & DESTRUCTOR ------------------------------------------------------------------------------

        /// <summary>
        /// Initializes a new instance of the <see cref="KnownFolderGuidAttribute"/> class with the given string
        /// representing the GUID of the <see cref="KnownFolderType"/>.
        /// </summary>
        /// <param name="guid">The GUID string of the <see cref="KnownFolderType"/>.</param>
        internal KnownFolderGuidAttribute(string guid)
        {
            Guid = new Guid(guid);
        }

        // ---- PROPERTIES ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Gets the <see cref="Guid"/> for the <see cref="KnownFolderType"/> enumeration member.
        /// </summary>
        internal Guid Guid { get; }
    }
}
