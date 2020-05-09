USE [master]
GO
/****** Object:  Database [wabbajack_prod]    Script Date: 3/28/2020 4:58:58 PM ******/
CREATE DATABASE [wabbajack_prod]
    CONTAINMENT = NONE
    WITH CATALOG_COLLATION = DATABASE_DEFAULT
GO
ALTER DATABASE [wabbajack_prod] SET COMPATIBILITY_LEVEL = 150
GO
IF (1 = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))
    begin
        EXEC [wabbajack_prod].[dbo].[sp_fulltext_database] @action = 'enable'
    end
GO
ALTER DATABASE [wabbajack_prod] SET ANSI_NULL_DEFAULT OFF
GO
ALTER DATABASE [wabbajack_prod] SET ANSI_NULLS OFF
GO
ALTER DATABASE [wabbajack_prod] SET ANSI_PADDING OFF
GO
ALTER DATABASE [wabbajack_prod] SET ANSI_WARNINGS OFF
GO
ALTER DATABASE [wabbajack_prod] SET ARITHABORT OFF
GO
ALTER DATABASE [wabbajack_prod] SET AUTO_CLOSE OFF
GO
ALTER DATABASE [wabbajack_prod] SET AUTO_SHRINK OFF
GO
ALTER DATABASE [wabbajack_prod] SET AUTO_UPDATE_STATISTICS ON
GO
ALTER DATABASE [wabbajack_prod] SET CURSOR_CLOSE_ON_COMMIT OFF
GO
ALTER DATABASE [wabbajack_prod] SET CURSOR_DEFAULT  GLOBAL
GO
ALTER DATABASE [wabbajack_prod] SET CONCAT_NULL_YIELDS_NULL OFF
GO
ALTER DATABASE [wabbajack_prod] SET NUMERIC_ROUNDABORT OFF
GO
ALTER DATABASE [wabbajack_prod] SET QUOTED_IDENTIFIER OFF
GO
ALTER DATABASE [wabbajack_prod] SET RECURSIVE_TRIGGERS OFF
GO
ALTER DATABASE [wabbajack_prod] SET  DISABLE_BROKER
GO
ALTER DATABASE [wabbajack_prod] SET AUTO_UPDATE_STATISTICS_ASYNC OFF
GO
ALTER DATABASE [wabbajack_prod] SET DATE_CORRELATION_OPTIMIZATION OFF
GO
ALTER DATABASE [wabbajack_prod] SET TRUSTWORTHY OFF
GO
ALTER DATABASE [wabbajack_prod] SET ALLOW_SNAPSHOT_ISOLATION OFF
GO
ALTER DATABASE [wabbajack_prod] SET PARAMETERIZATION SIMPLE
GO
ALTER DATABASE [wabbajack_prod] SET READ_COMMITTED_SNAPSHOT OFF
GO
ALTER DATABASE [wabbajack_prod] SET HONOR_BROKER_PRIORITY OFF
GO
ALTER DATABASE [wabbajack_prod] SET RECOVERY FULL
GO
ALTER DATABASE [wabbajack_prod] SET  MULTI_USER
GO
ALTER DATABASE [wabbajack_prod] SET PAGE_VERIFY CHECKSUM
GO
ALTER DATABASE [wabbajack_prod] SET DB_CHAINING OFF
GO
ALTER DATABASE [wabbajack_prod] SET FILESTREAM( NON_TRANSACTED_ACCESS = OFF )
GO
ALTER DATABASE [wabbajack_prod] SET TARGET_RECOVERY_TIME = 60 SECONDS
GO
ALTER DATABASE [wabbajack_prod] SET DELAYED_DURABILITY = DISABLED
GO
EXEC sys.sp_db_vardecimal_storage_format N'wabbajack_prod', N'ON'
GO
ALTER DATABASE [wabbajack_prod] SET QUERY_STORE = OFF
GO
USE [wabbajack_prod]
GO
/****** Object:  Schema [test]    Script Date: 3/28/2020 4:58:59 PM ******/
CREATE SCHEMA [test]
GO
/****** Object:  UserDefinedTableType [dbo].[ArchiveContentType]    Script Date: 3/28/2020 4:58:59 PM ******/
CREATE TYPE [dbo].[ArchiveContentType] AS TABLE(
                                                   [Parent] [bigint] NOT NULL,
                                                   [Child] [bigint] NOT NULL,
                                                   [Path] [nvarchar](max) NOT NULL
                                               )
GO
/****** Object:  UserDefinedTableType [dbo].[IndexedFileType]    Script Date: 3/28/2020 4:58:59 PM ******/
CREATE TYPE [dbo].[IndexedFileType] AS TABLE(
                                                [Hash] [bigint] NOT NULL,
                                                [Sha256] [binary](32) NOT NULL,
                                                [Sha1] [binary](20) NOT NULL,
                                                [Md5] [binary](16) NOT NULL,
                                                [Crc32] [int] NOT NULL,
                                                [Size] [bigint] NOT NULL
                                            )
GO
/****** Object:  UserDefinedFunction [dbo].[Base64ToLong]    Script Date: 3/28/2020 4:58:59 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date, ,>
-- Description:	<Description, ,>
-- =============================================
CREATE FUNCTION [dbo].[Base64ToLong]
(
    -- Add the parameters for the function here
    @Input varchar
)
    RETURNS bigint
AS
BEGIN
    -- Declare the return variable here
    DECLARE @ResultVar bigint

    -- Add the T-SQL statements to compute the return value here
    SELECT @ResultVar = CAST('string' as varbinary(max)) FOR XML PATH(''), BINARY BASE64

    -- Return the result of the function
    RETURN @ResultVar

END
GO
/****** Object:  UserDefinedFunction [dbo].[MaxMetricDate]    Script Date: 3/28/2020 4:58:59 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date, ,>
-- Description:	<Description, ,>
-- =============================================
CREATE FUNCTION [dbo].[MaxMetricDate]
(
)
    RETURNS date
AS
BEGIN
    -- Declare the return variable here
    DECLARE @Result date

    -- Add the T-SQL statements to compute the return value here
    SELECT @Result = max(Timestamp) from dbo.Metrics where MetricsKey is not null

    -- Return the result of the function
    RETURN @Result

END
GO
/****** Object:  UserDefinedFunction [dbo].[MinMetricDate]    Script Date: 3/28/2020 4:58:59 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date, ,>
-- Description:	<Description, ,>
-- =============================================
CREATE FUNCTION [dbo].[MinMetricDate]
(
)
    RETURNS date
AS
BEGIN
    -- Declare the return variable here
    DECLARE @Result date

    -- Add the T-SQL statements to compute the return value here
    SELECT @Result = min(Timestamp) from dbo.Metrics WHERE MetricsKey is not null

    -- Return the result of the function
    RETURN @Result

END
GO
/****** Object:  Table [dbo].[IndexedFile]    Script Date: 3/28/2020 4:58:59 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[IndexedFile](
                                    [Hash] [bigint] NOT NULL,
                                    [Sha256] [binary](32) NOT NULL,
                                    [Sha1] [binary](20) NOT NULL,
                                    [Md5] [binary](16) NOT NULL,
                                    [Crc32] [int] NOT NULL,
                                    [Size] [bigint] NOT NULL,
                                    CONSTRAINT [PK_IndexedFile] PRIMARY KEY CLUSTERED
                                        (
                                         [Hash] ASC
                                            )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

/****** Object:  Table [dbo].[Jobs] ******/
CREATE TABLE [dbo].[Jobs](
                             [Id] [bigint] IDENTITY(1,1) NOT NULL,
                             [Priority] [int] NOT NULL,
                             [PrimaryKeyString] [nvarchar](max) NULL,
                             [Started] [datetime] NULL,
                             [Ended] [datetime] NULL,
                             [Created] [datetime] NOT NULL,
                             [Success] [tinyint] NULL,
                             [ResultContent] [nvarchar](max) NULL,
                             [Payload] [nvarchar](max) NULL,
                             [OnSuccess] [nvarchar](max) NULL,
                             [RunBy] [uniqueidentifier] NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO



/****** Object:  Table [dbo].[ArchiveContent]    Script Date: 3/28/2020 4:58:59 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ArchiveContent](
                                       [Parent] [bigint] NOT NULL,
                                       [Child] [bigint] NOT NULL,
                                       [Path] [nvarchar](max) NULL,
                                       [PathHash]  AS (CONVERT([binary](32),hashbytes('SHA2_256',[Path]))) PERSISTED NOT NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

CREATE STATISTICS [Child_Parent_Stat] ON [dbo].[ArchiveContent]([Child], [Parent])
GO
CREATE CLUSTERED INDEX [Child_Parent_IDX] ON [dbo].[ArchiveContent]
    (
     [Child] ASC,
     [Parent] ASC
        )WITH (SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AllFilesInArchive]    Script Date: 3/28/2020 4:58:59 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AllFilesInArchive](
                                          [TopParent] [bigint] NOT NULL,
                                          [Child] [bigint] NOT NULL,
                                          CONSTRAINT [PK_AllFilesInArchive] PRIMARY KEY CLUSTERED
                                              (
                                               [TopParent] ASC,
                                               [Child] ASC
                                                  )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  View [dbo].[AllArchiveContent]    Script Date: 3/28/2020 4:58:59 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


CREATE VIEW [dbo].[AllArchiveContent]
            WITH SCHEMABINDING
AS
SELECT af.TopParent, ac.Parent, af.Child, ac.Path, idx.Size
FROM
    dbo.AllFilesInArchive af
        LEFT JOIN dbo.ArchiveContent ac on af.Child = ac.Child
        LEFT JOIN dbo.IndexedFile idx on af.Child = idx.Hash
GO

/****** Object:  Table [dbo].[NexusFileInfos]    Script Date: 4/1/2020 2:41:00 PM ******/
CREATE TABLE [dbo].[NexusFileInfos](
                                       [Game] [int] NOT NULL,
                                       [ModId] [bigint] NOT NULL,
                                       [FileId] [bigint] NOT NULL,
                                       [LastChecked] [datetime] NOT NULL,
                                       [Data] [nvarchar](max) NOT NULL,
                                       CONSTRAINT [PK_NexusFileInfos] PRIMARY KEY CLUSTERED
                                           (
                                            [Game] ASC,
                                            [ModId] ASC,
                                            [FileId] ASC
                                               )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

/****** Object:  Table [dbo].[NexusModFiles]    Script Date: 4/1/2020 2:41:04 PM ******/

CREATE TABLE [dbo].[NexusModFiles](
                                      [Game] [int] NOT NULL,
                                      [ModId] [bigint] NOT NULL,
                                      [LastChecked] [datetime] NOT NULL,
                                      [Data] [nvarchar](max) NOT NULL,
                                      CONSTRAINT [PK_NexusModFiles] PRIMARY KEY CLUSTERED
                                          (
                                           [Game] ASC,
                                           [ModId] ASC
                                              )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

/****** Object:  Table [dbo].[NexusModInfos]    Script Date: 4/1/2020 2:41:07 PM ******/

CREATE TABLE [dbo].[NexusModInfos](
                                      [Game] [int] NOT NULL,
                                      [ModId] [bigint] NOT NULL,
                                      [LastChecked] [datetime] NOT NULL,
                                      [Data] [nvarchar](max) NOT NULL,
                                      CONSTRAINT [PK_NexusModInfos] PRIMARY KEY CLUSTERED
                                          (
                                           [Game] ASC,
                                           [ModId] ASC
                                              )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

/****** Object:  Table [dbo].[ModLists]    Script Date: 4/2/2020 3:59:19 PM ******/
CREATE TABLE [dbo].[ModLists](
 [MachineURL] [nvarchar](50) NOT NULL,
 [Hash] [bigint] NOT NULL,
 [Metadata] [nvarchar](max) NOT NULL,
 [Modlist] [nvarchar](max) NOT NULL,
 CONSTRAINT [PK_ModLists] PRIMARY KEY CLUSTERED
     (
      [MachineURL] ASC
         )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

/****** Object:  Table [dbo].[ModListArchive]    Script Date: 4/11/2020 10:33:20 AM ******/

CREATE TABLE [dbo].[ModListArchives](
[MachineUrl] [nvarchar](50) NOT NULL,
[Hash] [bigint] NOT NULL,
[PrimaryKeyString] [nvarchar](max) NOT NULL,
[Size] [bigint] NOT NULL,
[State] [nvarchar](max) NOT NULL,
CONSTRAINT [PK_ModListArchive] PRIMARY KEY CLUSTERED
   (
    [MachineUrl] ASC,
    [Hash] ASC
       )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

/****** Object:  Table [dbo].[ModListArchiveStatus]    Script Date: 4/11/2020 9:44:25 PM ******/

CREATE TABLE [dbo].[ModListArchiveStatus](
 [PrimaryKeyStringHash] [binary](32) NOT NULL,
 [Hash] [bigint] NOT NULL,
 [PrimaryKeyString] [nvarchar](max) NOT NULL,
 [IsValid] [tinyint] NOT NULL,
 CONSTRAINT [PK_ModListArchiveStatus] PRIMARY KEY CLUSTERED
     (
      [PrimaryKeyStringHash] ASC,
      [Hash] ASC
         )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

/****** Object:  Table [dbo].[ArchivePatches]    Script Date: 4/13/2020 9:39:25 PM ******/
CREATE TABLE [dbo].[ArchivePatches](
[SrcPrimaryKeyStringHash] [binary](32) NOT NULL,
[SrcPrimaryKeyString] [nvarchar](max) NOT NULL,
[SrcHash] [bigint] NOT NULL,
[DestPrimaryKeyStringHash] [binary](32) NOT NULL,
[DestPrimaryKeyString] [nvarchar](max) NOT NULL,
[DestHash] [bigint] NOT NULL,
[SrcState] [nvarchar](max) NOT NULL,
[DestState] [nvarchar](max) NOT NULL,
[SrcDownload] [nvarchar](max) NULL,
[DestDownload] [nvarchar](max) NULL,
[CDNPath] [nvarchar](max) NULL,
CONSTRAINT [PK_ArchivePatches] PRIMARY KEY CLUSTERED
   (
    [SrcPrimaryKeyStringHash] ASC,
    [SrcHash] ASC,
    [DestPrimaryKeyStringHash] ASC,
    [DestHash] ASC
       )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO


/****** Object:  Table [dbo].[Metrics]    Script Date: 3/28/2020 4:58:59 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Metrics](
                                [Id] [bigint] IDENTITY(1,1) NOT NULL,
                                [Timestamp] [datetime] NOT NULL,
                                [Action] [nvarchar](64) NOT NULL,
                                [Subject] [nvarchar](max) NOT NULL,
                                [MetricsKey] [nvarchar](64) NULL,
                                [GroupingSubject]  AS (substring([Subject],(0),case when patindex('%[0-9].%',[Subject])=(0) then len([Subject])+(1) else patindex('%[0-9].%',[Subject]) end)),
                                CONSTRAINT [PK_Metrics] PRIMARY KEY CLUSTERED
                                    (
                                     [Id] ASC
                                        )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

/****** Object:  Table [dbo].[AuthoredFiles]    Script Date: 5/9/2020 2:22:00 PM ******/
CREATE TABLE [dbo].[AuthoredFiles](
                                      [ServerAssignedUniqueId] [uniqueidentifier] NOT NULL,
                                      [LastTouched] [datetime] NOT NULL,
                                      [CDNFileDefinition] [nvarchar](max) NOT NULL,
                                      [Finalized] [datetime] NULL,
                                      CONSTRAINT [PK_AuthoredFiles] PRIMARY KEY CLUSTERED
                                          (
                                           [ServerAssignedUniqueId] ASC
                                              )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

/****** Uploaded Files [UploadedFiles] *************/

CREATE TABLE [dbo].[UploadedFiles](
      [Id] [uniqueidentifier] NOT NULL,
      [Name] [nvarchar](max) NOT NULL,
      [Size] [bigint] NOT NULL,
      [UploadedBy] [nvarchar](40) NOT NULL,
      [Hash] [bigint] NOT NULL,
      [UploadDate] [datetime] NOT NULL,
      [CDNName] [nvarchar](max) NULL,
      CONSTRAINT [PK_UploadedFiles] PRIMARY KEY CLUSTERED
          (
           [Id] ASC
              )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** API Keys [ApiKeys] ********/
CREATE TABLE [dbo].[ApiKeys](
[APIKey] [nvarchar](260) NOT NULL,
[Owner] [nvarchar](40) NOT NULL,
CONSTRAINT [PK_ApiKeys] PRIMARY KEY CLUSTERED
    (
     [APIKey] ASC,
     [Owner] ASC
        )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX [ByAPIKey] ON [dbo].[ApiKeys]
    (
     [APIKey] ASC
        )
    INCLUDE([Owner]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO

/****** Object:  Table [dbo].[DownloadStates]    Script Date: 3/31/2020 6:22:47 AM ******/

CREATE TABLE [dbo].[DownloadStates](
   [Id] [binary](32) NOT NULL,
   [Hash] [bigint] NOT NULL,
   [PrimaryKey] [nvarchar](max) NOT NULL,
   [IniState] [nvarchar](max) NOT NULL,
   [JsonState] [nvarchar](max) NOT NULL,
   CONSTRAINT [PK_DownloadStates] PRIMARY KEY CLUSTERED
       (
        [Id] ASC
           )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO


CREATE NONCLUSTERED INDEX [ByHash] ON [dbo].[DownloadStates]
    (
     [Hash] ASC
        )
    INCLUDE([IniState]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO

/****** Object:  View [dbo].[GameFiles]    Script Date: 4/30/2020 4:23:25 PM ******/

CREATE VIEW [dbo].[GameFiles]
            WITH SCHEMABINDING
AS

Select
    Id,
    CONVERT(NVARCHAR(20), JSON_VALUE(JsonState,'$.GameVersion')) as GameVersion,
    CONVERT(NVARCHAR(32),JSON_VALUE(JsonState,'$.Game')) as Game,
    JSON_VALUE(JsonState,'$.GameFile') as Path,
    Hash as Hash
FROM dbo.DownloadStates
WHERE PrimaryKey like 'GameFileSourceDownloader+State|%'
  AND JSON_VALUE(JsonState,'$.GameFile') NOT LIKE '%.xxhash'
GO

CREATE UNIQUE CLUSTERED INDEX [ByGameAndVersion] ON [dbo].[GameFiles]
    (
     [Game] ASC,
     [GameVersion] ASC,
     [Id] ASC
        )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO



/****** Object:  Index [IX_Child]    Script Date: 3/28/2020 4:58:59 PM ******/
CREATE NONCLUSTERED INDEX [IX_Child] ON [dbo].[AllFilesInArchive]
    (
     [Child] ASC
        )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_ArchiveContent_Child]    Script Date: 3/28/2020 4:58:59 PM ******/
CREATE NONCLUSTERED INDEX [IX_ArchiveContent_Child] ON [dbo].[ArchiveContent]
    (
     [Child] ASC
        )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ARITHABORT ON
SET CONCAT_NULL_YIELDS_NULL ON
SET QUOTED_IDENTIFIER ON
SET ANSI_NULLS ON
SET ANSI_PADDING ON
SET ANSI_WARNINGS ON
SET NUMERIC_ROUNDABORT OFF
GO
/****** Object:  Index [PK_ArchiveContent]    Script Date: 3/28/2020 4:58:59 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [PK_ArchiveContent] ON [dbo].[ArchiveContent]
    (
     [Parent] ASC,
     [PathHash] ASC
        )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_IndexedFile_By_SHA256]    Script Date: 3/28/2020 4:58:59 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_IndexedFile_By_SHA256] ON [dbo].[IndexedFile]
    (
     [Sha256] ASC
        )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  StoredProcedure [dbo].[MergeAllFilesInArchive]    Script Date: 3/28/2020 4:58:59 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[MergeAllFilesInArchive]
AS
BEGIN
    -- SET NOCOUNT ON added to prevent extra result sets from
    -- interfering with SELECT statements.
    SET NOCOUNT ON;

    MERGE dbo.AllFilesInArchive t USING (
        SELECT DISTINCT TopParent, unpvt.Child
        FROM
            (SELECT a3.Parent AS P3, a2.Parent as P2, a1.Parent P1, a0.Parent P0, a0.Parent as Parent, a0.Child FROM
                dbo.ArChiveContent a0
                    LEFT JOIN dbo.ArChiveContent a1 ON a0.Parent = a1.Child
                    LEFT JOIN dbo.ArChiveContent a2 ON a1.Parent = a2.Child
                    LEFT JOIN dbo.ArChiveContent a3 ON a2.Parent = a3.Child) p
                UNPIVOT
                (TopParent For C IN (p.P3, p.P2, p.P1, p.P0)) as unpvt
                LEFT JOIN dbo.IndexedFile idf on unpvt.Child = idf.Hash
        WHERE TopParent is not null) s
    ON t.TopParent = s.TopParent AND t.Child = s.Child
    WHEN NOT MATCHED
        THEN INSERT (TopParent, Child) VALUES (s.TopParent, s.Child);
END
GO
/****** Object:  StoredProcedure [dbo].[MergeIndexedFiles]    Script Date: 3/28/2020 4:58:59 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[MergeIndexedFiles]
    -- Add the parameters for the stored procedure here
    @Files dbo.IndexedFileType READONLY,
    @Contents dbo.ArchiveContentType READONLY
AS
BEGIN
    -- SET NOCOUNT ON added to prevent extra result sets from
    -- interfering with SELECT statements.
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    MERGE dbo.IndexedFile AS TARGET
    USING (SELECT DISTINCT * FROM @Files) as SOURCE
    ON (TARGET.Hash = SOURCE.HASH)
    WHEN NOT MATCHED BY TARGET
        THEN INSERT (Hash, Sha256, Sha1, Md5, Crc32, Size)
             VALUES (Source.Hash, Source.Sha256, Source.Sha1, Source.Md5, Source.Crc32, Source.Size);

    MERGE dbo.ArchiveContent AS TARGET
    USING (SELECT DISTINCT * FROM @Contents) as SOURCE
    ON (TARGET.Parent = SOURCE.Parent AND TARGET.PathHash = CAST(HASHBYTES('SHA2_256', SOURCE.Path) as binary(32)))
    WHEN NOT MATCHED
        THEN INSERT (Parent, Child, Path)
             VALUES (Source.Parent, Source.Child, Source.Path);

    MERGE dbo.AllFilesInArchive t USING (
        SELECT DISTINCT TopParent, unpvt.Child
        FROM
            (SELECT a3.Parent AS P3, a2.Parent as P2, a1.Parent P1, a0.Parent P0, a0.Parent as Parent, a0.Child FROM
                dbo.ArChiveContent a0
                    LEFT JOIN dbo.ArChiveContent a1 ON a0.Parent = a1.Child
                    LEFT JOIN dbo.ArChiveContent a2 ON a1.Parent = a2.Child
                    LEFT JOIN dbo.ArChiveContent a3 ON a2.Parent = a3.Child) p
                UNPIVOT
                (TopParent For C IN (p.P3, p.P2, p.P1, p.P0)) as unpvt
                LEFT JOIN dbo.IndexedFile idf on unpvt.Child = idf.Hash
        WHERE TopParent is not null
          AND Child in (SELECT DISTINCT Hash FROM @Files)) s
    ON t.TopParent = s.TopParent AND t.Child = s.Child
    WHEN NOT MATCHED
        THEN INSERT (TopParent, Child) VALUES (s.TopParent, s.Child);

    COMMIT;

END
GO
USE [master]
GO
ALTER DATABASE [wabbajack_prod] SET  READ_WRITE
GO
