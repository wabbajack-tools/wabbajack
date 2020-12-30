USE [master]
GO
/****** Object:  Database [wabbajack_prod]    Script Date: 12/29/2020 8:55:03 PM ******/
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
ALTER DATABASE [wabbajack_prod] SET QUERY_STORE = OFF
GO
USE [wabbajack_prod]
GO
/****** Object:  User [wabbajack]    Script Date: 12/29/2020 8:55:03 PM ******/
CREATE USER [wabbajack] WITHOUT LOGIN WITH DEFAULT_SCHEMA=[dbo]
GO
ALTER ROLE [db_datareader] ADD MEMBER [wabbajack]
GO
ALTER ROLE [db_datawriter] ADD MEMBER [wabbajack]
GO
/****** Object:  Schema [test]    Script Date: 12/29/2020 8:55:04 PM ******/
CREATE SCHEMA [test]
    GO
/****** Object:  UserDefinedTableType [dbo].[ArchiveContentType]    Script Date: 12/29/2020 8:55:04 PM ******/
CREATE TYPE [dbo].[ArchiveContentType] AS TABLE(
    [Parent] [bigint] NOT NULL,
    [Child] [bigint] NOT NULL,
    [Path] [nvarchar](max) NOT NULL
    )
    GO
/****** Object:  UserDefinedTableType [dbo].[IndexedFileType]    Script Date: 12/29/2020 8:55:04 PM ******/
CREATE TYPE [dbo].[IndexedFileType] AS TABLE(
    [Hash] [bigint] NOT NULL,
    [Sha256] [binary](32) NOT NULL,
    [Sha1] [binary](20) NOT NULL,
    [Md5] [binary](16) NOT NULL,
    [Crc32] [int] NOT NULL,
    [Size] [bigint] NOT NULL
    )
    GO
/****** Object:  UserDefinedFunction [dbo].[Base64ToLong]    Script Date: 12/29/2020 8:55:04 PM ******/
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
SELECT @ResultVar = CAST(@Input as varbinary(max)) FOR XML PATH(''), BINARY BASE64

       -- Return the result of the function
    RETURN @ResultVar

END
GO
/****** Object:  UserDefinedFunction [dbo].[MaxMetricDate]    Script Date: 12/29/2020 8:55:04 PM ******/
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
/****** Object:  UserDefinedFunction [dbo].[MinMetricDate]    Script Date: 12/29/2020 8:55:04 PM ******/
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
/****** Object:  Table [dbo].[ModLists]    Script Date: 12/29/2020 8:55:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ModLists](
    [MachineURL] [nvarchar](50) NOT NULL,
    [Hash] [bigint] NOT NULL,
    [Metadata] [nvarchar](max) NOT NULL,
    [Modlist] [nvarchar](max) NOT NULL,
    [BrokenDownload] [tinyint] NOT NULL,
    CONSTRAINT [PK_ModLists] PRIMARY KEY CLUSTERED
(
[MachineURL] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
    GO
/****** Object:  View [dbo].[AllModListArchives]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
CREATE VIEW [dbo].[AllModListArchives]
AS
SELECT ml.MachineURL, p.Name, p.Size, p.Hash, p.State
FROM [ModLists] ml
    CROSS APPLY
    OPENJSON(ModList, '$.Archives')
WITH (
    Name nvarchar(max) '$.Name',
    Size nvarchar(max) '$.Size',
    Hash nvarchar(max) '$.Hash',
    State nvarchar(max) as json

    ) p
    GO
/****** Object:  Table [dbo].[IndexedFile]    Script Date: 12/29/2020 8:55:04 PM ******/
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
/****** Object:  Table [dbo].[ArchiveContent]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
CREATE TABLE [dbo].[ArchiveContent](
    [Parent] [bigint] NOT NULL,
    [Child] [bigint] NOT NULL,
    [Path] [nvarchar](max) NULL,
    [PathHash]  AS (CONVERT([binary](32),hashbytes('SHA2_256',replace(lower([Path]),'/','\')))) PERSISTED NOT NULL
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
    GO
/****** Object:  Index [Child_Parent_IDX]    Script Date: 12/29/2020 8:55:04 PM ******/
CREATE CLUSTERED INDEX [Child_Parent_IDX] ON [dbo].[ArchiveContent]
(
	[Child] ASC,
	[Parent] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AllFilesInArchive]    Script Date: 12/29/2020 8:55:04 PM ******/
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
/****** Object:  View [dbo].[AllArchiveContent]    Script Date: 12/29/2020 8:55:04 PM ******/
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
/****** Object:  Table [dbo].[DownloadStates]    Script Date: 12/29/2020 8:55:04 PM ******/
SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
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
/****** Object:  View [dbo].[GameFiles]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO

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
SET ARITHABORT ON
    SET CONCAT_NULL_YIELDS_NULL ON
    SET QUOTED_IDENTIFIER ON
    SET ANSI_NULLS ON
    SET ANSI_PADDING ON
    SET ANSI_WARNINGS ON
    SET NUMERIC_ROUNDABORT OFF
        GO
/****** Object:  Index [ByGameAndVersion]    Script Date: 12/29/2020 8:55:04 PM ******/
CREATE UNIQUE CLUSTERED INDEX [ByGameAndVersion] ON [dbo].[GameFiles]
(
	[Game] ASC,
	[GameVersion] ASC,
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AuthoredFiles]    Script Date: 12/29/2020 8:55:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
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
/****** Object:  View [dbo].[AuthoredFilesSummaries]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
/****** Script for SelectTopNRows command from SSMS  ******/
CREATE VIEW [dbo].[AuthoredFilesSummaries]
AS
SELECT
    [ServerAssignedUniqueId]
        ,[LastTouched]
        ,[Finalized]
        ,JSON_VALUE(CDNFileDefinition, '$.OriginalFileName') as OriginalFileName
        ,JSON_VALUE(CDNFileDefinition, '$.MungedName') as MungedName
        ,JSON_VALUE(CDNFileDefinition, '$.Author') as Author
        ,JSON_VALUE(CDNFILEDefinition, '$.Size') as Size
FROM [wabbajack_prod].[dbo].[AuthoredFiles]
    GO
/****** Object:  Table [dbo].[AccessLog]    Script Date: 12/29/2020 8:55:04 PM ******/
SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
CREATE TABLE [dbo].[AccessLog](
    [Id] [bigint] IDENTITY(1,1) NOT NULL,
    [Timestamp] [datetime] NOT NULL,
    [Action] [nvarchar](max) NOT NULL,
    [Ip] [nvarchar](50) NOT NULL,
    [MetricsKey]  AS (json_value([Action],'$.Headers."x-metrics-key"[0]')),
    CONSTRAINT [PK_AccessLog] PRIMARY KEY CLUSTERED
(
[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
    GO
/****** Object:  Table [dbo].[ApiKeys]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
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
/****** Object:  Table [dbo].[ArchiveDownloads]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
CREATE TABLE [dbo].[ArchiveDownloads](
    [Id] [uniqueidentifier] NOT NULL,
    [PrimaryKeyString] [nvarchar](255) NOT NULL,
    [Size] [bigint] NULL,
    [Hash] [bigint] NULL,
    [IsFailed] [tinyint] NULL,
    [DownloadFinished] [datetime] NULL,
    [DownloadState] [nvarchar](max) NOT NULL,
    [Downloader] [nvarchar](50) NOT NULL,
    [FailMessage] [nvarchar](max) NULL,
    CONSTRAINT [PK_ArchiveDownloads] PRIMARY KEY CLUSTERED
(
[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
    GO
/****** Object:  Table [dbo].[ArchivePatches]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
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
/****** Object:  Table [dbo].[GameMetadata]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
CREATE TABLE [dbo].[GameMetadata](
    [NexusGameId] [bigint] NULL,
    [WabbajackName] [nvarchar](50) NOT NULL,
    CONSTRAINT [PK_GameMetadata] PRIMARY KEY CLUSTERED
(
[WabbajackName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY]
    GO
/****** Object:  Table [dbo].[Jobs]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
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
/****** Object:  Index [ClusteredIndex-20200506-123511]    Script Date: 12/29/2020 8:55:04 PM ******/
CREATE CLUSTERED INDEX [ClusteredIndex-20200506-123511] ON [dbo].[Jobs]
(
	[Priority] DESC,
	[Started] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Metrics]    Script Date: 12/29/2020 8:55:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Metrics](
    [Id] [bigint] IDENTITY(1,1) NOT NULL,
    [Timestamp] [datetime] NOT NULL,
    [Action] [varchar](64) NOT NULL,
    [Subject] [varchar](max) NOT NULL,
    [MetricsKey] [varchar](64) NULL,
    [GroupingSubject]  AS (case when [Action]='started_wabbajack' then [Subject] else substring([Subject],(0),case when patindex('%[0-9].%',[Subject])=(0) then len([Subject])+(1) else patindex('%[0-9].%',[Subject]) end) end),
    CONSTRAINT [PK_Metrics] PRIMARY KEY CLUSTERED
(
[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
    GO
/****** Object:  Table [dbo].[MirroredArchives]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
CREATE TABLE [dbo].[MirroredArchives](
    [Hash] [bigint] NOT NULL,
    [Created] [datetime] NOT NULL,
    [Uploaded] [datetime] NULL,
    [Rationale] [nvarchar](max) NOT NULL,
    [FailMessage] [nvarchar](max) NULL,
    CONSTRAINT [PK_MirroredArchives] PRIMARY KEY CLUSTERED
(
[Hash] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
    GO
/****** Object:  Table [dbo].[ModListArchives]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
CREATE TABLE [dbo].[ModListArchives](
    [MachineUrl] [nvarchar](50) NOT NULL,
    [Hash] [bigint] NOT NULL,
    [PrimaryKeyString] [nvarchar](max) NOT NULL,
    [Size] [bigint] NOT NULL,
    [State] [nvarchar](max) NOT NULL,
    [Name] [nvarchar](max) NULL,
    CONSTRAINT [PK_ModListArchive] PRIMARY KEY CLUSTERED
(
    [MachineUrl] ASC,
[Hash] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
    GO
/****** Object:  Table [dbo].[ModListArchiveStatus]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
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
/****** Object:  Table [dbo].[NexusFileInfos]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
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
/****** Object:  Table [dbo].[NexusKeys]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
CREATE TABLE [dbo].[NexusKeys](
    [ApiKey] [nvarchar](162) NOT NULL,
    [DailyRemain] [int] NOT NULL,
    [HourlyRemain] [int] NOT NULL,
    CONSTRAINT [PK_NexusKeys] PRIMARY KEY CLUSTERED
(
[ApiKey] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY]
    GO
/****** Object:  Table [dbo].[NexusModFiles]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
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
/****** Object:  Table [dbo].[NexusModFilesSlow]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
CREATE TABLE [dbo].[NexusModFilesSlow](
    [GameId] [bigint] NOT NULL,
    [FileId] [bigint] NOT NULL,
    [ModId] [bigint] NOT NULL,
    [LastChecked] [datetime] NOT NULL,
     CONSTRAINT [PK_NexusModFilesSlow] PRIMARY KEY CLUSTERED
    (
    [GameId] ASC,
    [FileId] ASC,
[ModId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY]
    GO
/****** Object:  Table [dbo].[NexusModInfos]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
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
/****** Object:  Table [dbo].[NexusModPermissions]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
CREATE TABLE [dbo].[NexusModPermissions](
    [NexusGameID] [int] NOT NULL,
    [ModID] [bigint] NOT NULL,
    [Permissions] [int] NOT NULL,
     CONSTRAINT [PK_NexusModPermissions] PRIMARY KEY CLUSTERED
    (
    [NexusGameID] ASC,
[ModID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY]
    GO
/****** Object:  Table [dbo].[NoPatch]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
CREATE TABLE [dbo].[NoPatch](
    [Hash] [bigint] NOT NULL,
    [Created] [datetime] NOT NULL,
    [Rationale] [nvarchar](max) NOT NULL,
    CONSTRAINT [PK_NoPatch] PRIMARY KEY CLUSTERED
(
[Hash] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
    GO
/****** Object:  Table [dbo].[Patches]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
CREATE TABLE [dbo].[Patches](
    [SrcId] [uniqueidentifier] NOT NULL,
    [DestId] [uniqueidentifier] NOT NULL,
    [PatchSize] [bigint] NULL,
    [Finished] [datetime] NULL,
    [IsFailed] [tinyint] NULL,
    [FailMessage] [varchar](max) NULL,
    [LastUsed] [datetime] NULL,
    [Downloads] [bigint] NOT NULL,
    CONSTRAINT [PK_Patches] PRIMARY KEY CLUSTERED
(
    [SrcId] ASC,
[DestId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
    GO
/****** Object:  Table [dbo].[TarKey]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
CREATE TABLE [dbo].[TarKey](
    [MetricsKey] [nvarchar](64) NOT NULL,
    CONSTRAINT [PK_TarKey] PRIMARY KEY CLUSTERED
(
[MetricsKey] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY]
    GO
/****** Object:  Table [dbo].[toDeleteTemp]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
CREATE TABLE [dbo].[toDeleteTemp](
    [Parent] [bigint] NULL,
    [Child] [bigint] NULL,
    [Path] [varchar](max) NULL,
    [PathHash] [binary](32) NULL
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
    GO
/****** Object:  Table [dbo].[UploadedFiles]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
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
/****** Object:  Table [dbo].[VirusScanResults]    Script Date: 12/29/2020 8:55:04 PM ******/
    SET ANSI_NULLS ON
    GO
    SET QUOTED_IDENTIFIER ON
    GO
CREATE TABLE [dbo].[VirusScanResults](
    [Hash] [bigint] NOT NULL,
    [IsMalware] [tinyint] NOT NULL,
     CONSTRAINT [PK_VirusScanResults] PRIMARY KEY CLUSTERED
    (
[Hash] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY]
    GO
    SET ANSI_PADDING ON
    GO
/****** Object:  Index [AccessLogByIP]    Script Date: 12/29/2020 8:55:04 PM ******/
CREATE NONCLUSTERED INDEX [AccessLogByIP] ON [dbo].[AccessLog]
(
	[Ip] ASC
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
/****** Object:  Index [AccessLogByMetricsKey]    Script Date: 12/29/2020 8:55:04 PM ******/
CREATE NONCLUSTERED INDEX [AccessLogByMetricsKey] ON [dbo].[AccessLog]
(
	[MetricsKey] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Child]    Script Date: 12/29/2020 8:55:04 PM ******/
CREATE NONCLUSTERED INDEX [IX_Child] ON [dbo].[AllFilesInArchive]
(
	[Child] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [ByAPIKey]    Script Date: 12/29/2020 8:55:04 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [ByAPIKey] ON [dbo].[ApiKeys]
(
	[APIKey] ASC
)
INCLUDE([Owner]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [_dta_index_ArchiveDownloads_5_1058102810__K6_1_3_4_7]    Script Date: 12/29/2020 8:55:04 PM ******/
CREATE NONCLUSTERED INDEX [_dta_index_ArchiveDownloads_5_1058102810__K6_1_3_4_7] ON [dbo].[ArchiveDownloads]
(
	[DownloadFinished] ASC
)
INCLUDE([Id],[Size],[Hash],[DownloadState]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [ByDownloaderAndFinished]    Script Date: 12/29/2020 8:55:04 PM ******/
CREATE NONCLUSTERED INDEX [ByDownloaderAndFinished] ON [dbo].[ArchiveDownloads]
(
	[DownloadFinished] ASC,
	[Downloader] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [ByPrimaryKeyAndHash]    Script Date: 12/29/2020 8:55:04 PM ******/
CREATE NONCLUSTERED INDEX [ByPrimaryKeyAndHash] ON [dbo].[ArchiveDownloads]
(
	[PrimaryKeyString] ASC,
	[Hash] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IDX_ID_HASH]    Script Date: 12/29/2020 8:55:04 PM ******/
CREATE NONCLUSTERED INDEX [IDX_ID_HASH] ON [dbo].[ArchiveDownloads]
(
	[Id] ASC,
	[Hash] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [ByHash]    Script Date: 12/29/2020 8:55:04 PM ******/
CREATE NONCLUSTERED INDEX [ByHash] ON [dbo].[DownloadStates]
(
	[Hash] ASC
)
INCLUDE([IniState]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [GameAndName-20200804-164236]    Script Date: 12/29/2020 8:55:04 PM ******/
CREATE NONCLUSTERED INDEX [GameAndName-20200804-164236] ON [dbo].[GameMetadata]
(
	[NexusGameId] ASC,
	[WabbajackName] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [JobsByEnded]    Script Date: 12/29/2020 8:55:04 PM ******/
CREATE NONCLUSTERED INDEX [JobsByEnded] ON [dbo].[Jobs]
(
	[Ended] ASC
)
INCLUDE([Id],[Priority],[Started],[Created],[Payload]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IDX_MetricsKey]    Script Date: 12/29/2020 8:55:04 PM ******/
CREATE NONCLUSTERED INDEX [IDX_MetricsKey] ON [dbo].[Metrics]
(
	[MetricsKey] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IDX_MirroredArchives_HashUploaded]    Script Date: 12/29/2020 8:55:04 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IDX_MirroredArchives_HashUploaded] ON [dbo].[MirroredArchives]
(
	[Hash] ASC,
	[Uploaded] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IDX_HASH]    Script Date: 12/29/2020 8:55:04 PM ******/
CREATE NONCLUSTERED INDEX [IDX_HASH] ON [dbo].[ModListArchives]
(
	[Hash] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[ModLists] ADD  DEFAULT ((0)) FOR [BrokenDownload]
    GO
ALTER TABLE [dbo].[Patches] ADD  DEFAULT ((0)) FOR [Downloads]
    GO
ALTER TABLE [dbo].[Patches]  WITH CHECK ADD  CONSTRAINT [FK_DestId] FOREIGN KEY([DestId])
    REFERENCES [dbo].[ArchiveDownloads] ([Id])
    GO
ALTER TABLE [dbo].[Patches] CHECK CONSTRAINT [FK_DestId]
    GO
ALTER TABLE [dbo].[Patches]  WITH CHECK ADD  CONSTRAINT [FK_SrcId] FOREIGN KEY([SrcId])
    REFERENCES [dbo].[ArchiveDownloads] ([Id])
    GO
ALTER TABLE [dbo].[Patches] CHECK CONSTRAINT [FK_SrcId]
    GO
/****** Object:  StoredProcedure [dbo].[MergeAllFilesInArchive]    Script Date: 12/29/2020 8:55:04 PM ******/
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
/****** Object:  StoredProcedure [dbo].[MergeIndexedFiles]    Script Date: 12/29/2020 8:55:04 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
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
    ON (TARGET.Parent = SOURCE.Parent AND TARGET.PathHash = CAST(HASHBYTES('SHA2_256', REPLACE(LOWER(SOURCE.Path), '/', '\')) as binary(32)))
    WHEN NOT MATCHED
    THEN INSERT (Parent, Child, Path)
    VALUES (Source.Parent, Source.Child, Source.Path);


COMMIT;

END
GO
USE [master]
GO
ALTER DATABASE [wabbajack_prod] SET  READ_WRITE 
GO
