USE [pig3]
GO

/****** Object:  Table [dbo].[Config]    Script Date: 2/3/2026 5:22:00 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Config](
	[ConfigId] [int] IDENTITY(1,1) NOT NULL,
	[AppDirectory] [nvarchar](max) NOT NULL,
	[ConfigDirectory] [nvarchar](max) NOT NULL,
	[MusicDirectory] [nvarchar](max) NOT NULL,
	[LogFile] [nvarchar](50) NULL,
	[PlayListDirectory] [nvarchar](max) NOT NULL,
	[Creator] [nvarchar](50) NULL,
	[Editor] [nvarchar](50) NULL,
	[Created] [datetime2](7) NOT NULL,
	[Edited] [datetime2](7) NULL,
 CONSTRAINT [PK__Table__C3BC335CFD09F8AA] PRIMARY KEY CLUSTERED 
(
	[ConfigId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO


USE [pig3]
GO

/****** Object:  Table [dbo].[ImportError]    Script Date: 2/3/2026 5:22:15 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[ImportError](
	[ImportErrorId] [bigint] IDENTITY(1,1) NOT NULL,
	[source] [nvarchar](50) NOT NULL,
	[error] [nvarchar](max) NOT NULL,
	[Created] [datetime2](7) NOT NULL,
	[Edited] [datetime2](7) NULL,
	[Creator] [nvarchar](50) NULL,
	[Editor] [nvarchar](50) NULL,
 CONSTRAINT [PK_ImprtErrorId] PRIMARY KEY CLUSTERED 
(
	[ImportErrorId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO


USE [pig3]
GO

/****** Object:  Table [dbo].[Log]    Script Date: 2/3/2026 5:22:38 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Log](
	[LogIdentifier] [bigint] IDENTITY(1,1) NOT NULL,
	[Type] [nvarchar](50) NOT NULL,
	[Message] [nvarchar](max) NOT NULL,
	[Exception] [nvarchar](max) NULL,
	[Severity] [int] NOT NULL,
	[ClassName] [nvarchar](100) NULL,
	[MethodName] [nvarchar](100) NULL,
	[Creator] [nvarchar](50) NULL,
	[Created] [datetime2](0) NOT NULL,
	[Editor] [nvarchar](50) NULL,
	[Edited] [datetime2](0) NULL,
 CONSTRAINT [PrimaryKeyLogLogIdentifier] PRIMARY KEY CLUSTERED 
(
	[LogIdentifier] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO


USE [pig3]
GO

/****** Object:  Table [dbo].[MP3Genre]    Script Date: 2/3/2026 5:23:02 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[MP3Genre](
	[GenreId] [int] IDENTITY(1,1) NOT NULL,
	[GenreName] [nvarchar](50) NULL,
	[Created] [datetime2](7) NULL,
	[Edited] [datetime2](7) NULL,
	[Creator] [nvarchar](50) NULL,
	[Editor] [nvarchar](50) NULL,
 CONSTRAINT [PK__MP3Genre__0385057E1F8C17DC] PRIMARY KEY CLUSTERED 
(
	[GenreId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO



USE [pig3]
GO

/****** Object:  Table [dbo].[Playlist]    Script Date: 2/3/2026 5:23:17 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Playlist](
	[PlaylistId] [int] IDENTITY(1,1) NOT NULL,
	[Title] [nvarchar](50) NOT NULL,
	[Minimum] [int] NOT NULL,
	[StartYear] [int] NULL,
	[EndYear] [int] NULL,
	[Creator] [nvarchar](50) NOT NULL,
	[Created] [datetime2](0) NOT NULL,
	[Editor] [nvarchar](50) NULL,
	[Edited] [datetime2](0) NULL,
 CONSTRAINT [PK__Playlist__B30167A0E70170F7] PRIMARY KEY CLUSTERED 
(
	[PlaylistId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO




USE [pig3]
GO

/****** Object:  Table [dbo].[Song]    Script Date: 2/3/2026 5:23:50 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Song](
	[SongId] [int] IDENTITY(1,1) NOT NULL,
	[Artist] [nvarchar](255) NULL,
	[Title] [nvarchar](255) NULL,
	[Genre] [nvarchar](255) NULL,
	[Seconds] [int] NULL,
	[Folder] [nvarchar](4000) NULL,
	[File] [nvarchar](4000) NOT NULL,
	[FileAddress] [nvarchar](4000) NULL,
	[FileSize] [bigint] NULL,
	[Album] [nvarchar](255) NULL,
	[Year] [int] NULL,
	[BPM] [int] NULL,
	[FileDate] [datetime2](0) NULL,
	[Creator] [nvarchar](50) NOT NULL,
	[Created] [datetime2](0) NOT NULL,
	[Editor] [nvarchar](50) NULL,
	[Edited] [datetime2](0) NULL,
 CONSTRAINT [PK_Song2] PRIMARY KEY CLUSTERED 
(
	[SongId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO



USE [pig3]
GO

/****** Object:  Table [dbo].[SongFilter]    Script Date: 2/3/2026 5:24:09 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[SongFilter](
	[SongFilterId] [int] IDENTITY(1,1) NOT NULL,
	[PlaylistId] [int] NOT NULL,
	[SongId] [int] NOT NULL,
	[HasArtist] [bit] NULL,
	[HasTitle] [bit] NULL,
	[HasGenre] [bit] NULL,
	[Creator] [nvarchar](50) NOT NULL,
	[Created] [datetime2](0) NOT NULL,
	[Editor] [nvarchar](50) NULL,
	[Edited] [datetime2](0) NULL,
 CONSTRAINT [PK__SongFilt__646B6747C9B85233] PRIMARY KEY CLUSTERED 
(
	[SongFilterId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[SongFilter]  WITH CHECK ADD  CONSTRAINT [FK_SongFilter_Playlist] FOREIGN KEY([PlaylistId])
REFERENCES [dbo].[Playlist] ([PlaylistId])
GO

ALTER TABLE [dbo].[SongFilter] CHECK CONSTRAINT [FK_SongFilter_Playlist]
GO

ALTER TABLE [dbo].[SongFilter]  WITH CHECK ADD  CONSTRAINT [FK_SongFilter_Song] FOREIGN KEY([SongId])
REFERENCES [dbo].[Song] ([SongId])
GO

ALTER TABLE [dbo].[SongFilter] CHECK CONSTRAINT [FK_SongFilter_Song]
GO






