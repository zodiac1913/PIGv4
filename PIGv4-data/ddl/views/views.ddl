USE [pig3]
GO

/****** Object:  View [dbo].[DbInfoColumns]    Script Date: 2/3/2026 5:15:58 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO






CREATE VIEW [dbo].[DbInfoColumns]
AS
select * from (select AC.object_id ObjectId,
AC.name ColumnName,
AC.column_id ColumnId,
DB_NAME() DatabaseName,
TBL.object_id TableId,
TBL.name TableName,
TBL.type_desc TableType,
SCH.name TableSchema,
object_definition(AC.default_object_id) DefaultValue,
AC.is_nullable IsNullable,
IIF(AC.is_nullable=1,'YES','NO') IS_NULLABLE,
CAST(AC.system_type_Id as int) DataTypeId,
TYP.name DataType,
dbo.GetCSharpType(TYP.name,IIF(AC.is_nullable=1,'YES','NO')) CodeType,
CAST(ISNULL(KY.is_primary_key,0) as bit) IsKey,
CAST(IIF(KY.name is null,0,1) as bit) IsIndex,
ISNULL(KY.name,'') IndexName,
IIF(ISNULL(KY.TYPE_DESC,'')='','',IIF(KY.IS_PRIMARY_KEY=0,KY.TYPE_DESC,'PRIMARY KEY')) IndexType,
AC.is_identity IsAutoNumber,
CAST(IIF(TYP.name in ('nvarchar','ntext') and AC.max_length is not null,(AC.max_length/2),IIF(TYP.name in ('char','varchar','text') and AC.max_length is not null,AC.max_length,null)) as int) MaximumLength,
CAST(IIF(TYP.name in ('char','varchar','text','nchar','nvarchar','ntext') and AC.max_length is not null,AC.max_length,null) as int) MaximumOctetLength,
CAST(IIF(TYP.name in ('bigint','numeric','bit','smallint','decimal','smallmoney','int','tinyint','money','float','real') and AC.precision is not null,AC.precision,null) as int) NumericPrecision,
CAST(IIF(TYP.name in ('bigint','numeric','smallint','decimal','smallmoney','int','tinyint','money','float','real') and AC.precision is not null,IIF(TYP.name in ('bigint','numeric','smallint','decimal','smallmoney','int','tinyint','money'),10,2),null) as int) NumericPrecisionRadix,
CAST(IIF(TYP.name in ('bigint','numeric','bit','smallint','decimal','smallmoney','int','tinyint','money','float','real') and AC.scale is not null,AC.scale,null) as int) NumericScale,
CAST(IIF(TYP.name in ('date','datetimeoffset','datetime2','smalldatetime','datetime','time') and AC.precision is not null,0,null) as int) DateTimePrecision,
'' CHARACTER_SET_CATALOG,
'' CHARACTER_SET_SCHEMA,
iif(TYP.collation_name is not null,'iso_1','') CHARACTER_SET_NAME,
'' COLLATION_CATALOG,
'' COLLATION_SCHEMA,
iif(TYP.collation_name is not null,TYP.collation_name,'') COLLATION_NAME,
'' DOMAIN_CATALOG,
'' DOMAIN_SCHEMA,
'' DOMAIN_NAME,
OBJECT_NAME(FK.parent_object_id) FKTable,
FKAC.name FKColumn,
IIF(OBJECT_NAME(FK.parent_object_id)<>TBL.name and OBJECT_NAME(FK.parent_object_id)<>'','One-To-Many','') FKRelationship 
from sys.all_columns AC 
INNER JOIN  (select * from SYS.objects where type_desc='USER_TABLE' or type_desc='VIEW') TBL ON AC.object_id = TBL.object_id 
INNER JOIN sys.schemas SCH ON SCH.schema_id = TBL.schema_id 
inner join sys.types TYP on TYP.system_type_id=AC.system_type_id and TYP.NAME<>'sysname' 
left join sys.index_columns IC  ON AC.object_id = IC.object_id AND AC.column_id = IC.column_id and IC.index_id = IC.column_id 
left join sys.indexes KY on KY.object_id=IC.object_id and KY.index_id=IC.index_id 
left join sys.foreign_key_columns FKC on FKC.referenced_object_id = AC.object_id and FKC.parent_column_id=AC.column_id 
left join sys.foreign_keys FK on FK.object_id=FKC.constraint_object_id 
left join sys.all_columns FKAC on FKAC.object_id=FKC.parent_object_id and FKAC.column_id=FKC.parent_column_id) a

GO


USE [pig3]
GO

/****** Object:  View [dbo].[DbInfoTables]    Script Date: 2/3/2026 5:16:13 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO




CREATE VIEW [dbo].[DbInfoTables]
AS
SELECT        TOP (100) PERCENT TBL.object_id AS ObjectId, DB_NAME() AS DatabaseName, TBL.object_id AS TableId, TBL.name AS TableName, TBL.type_desc AS TableType, SCH.name AS TableSchema,
                             (SELECT        COL_NAME(ic.object_id, ic.column_id) AS ColumnName
                               FROM            sys.indexes AS i INNER JOIN
                                                         sys.index_columns AS ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                               WHERE        (i.is_primary_key = 1) AND (ic.object_id = TBL.object_id)) AS PrimaryKey
FROM            sys.objects AS TBL INNER JOIN
                         sys.schemas AS SCH ON SCH.schema_id = TBL.schema_id
WHERE        (TBL.type_desc = 'USER_TABLE') OR
                         (TBL.type_desc = 'VIEW')
GO


USE [pig3]
GO

/****** Object:  View [dbo].[SongFiltersView]    Script Date: 2/3/2026 5:16:24 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


CREATE VIEW [dbo].[SongFiltersView]
AS
SELECT   sf.SongFilterId, sf.PlaylistId, sf.HasArtist, sf.HasTitle, sf.HasGenre, sf.SongId, 
	(select Title from dbo.Playlist where sf.PlaylistId = PlaylistId) Title,
	(select Minimum from dbo.Playlist where sf.PlaylistId = PlaylistId) Minimum,
	s.Artist, s.Title AS SongTitle, s.Genre, s.Seconds, s.Folder, s.FileAddress, s.[File], s.FileDate, s.FileSize, s.Album, s.Year, s.BPM
FROM            dbo.SongFilter AS sf LEFT JOIN
                         dbo.Song AS s ON sf.SongId = s.SongId
UNION ALL
SELECT   -1 SongFilterId, p.PlaylistId,  CAST(0 as bit) HasArtist, CAST(0 as bit) HasTitle, CAST(1 as bit) HasGenre, s.SongId, 
	p.Title,
	p.Minimum,
	s.Artist, s.Title AS SongTitle, s.Genre, s.Seconds, s.Folder, s.FileAddress, s.[File], s.FileDate, s.FileSize, s.Album, s.Year, s.BPM
FROM            dbo.Song AS s LEFT JOIN
                dbo.Playlist as p ON UPPER(s.Genre)=UPPER(p.Title)
				where songId not in (select songid from SongFilter where PlaylistId=p.PlaylistId)
GO

EXEC sys.sp_addextendedproperty @name=N'MS_DiagramPane1', @value=N'[0E232FF0-B466-11cf-A24F-00AA00A3EFFF, 1.00]
Begin DesignProperties = 
   Begin PaneConfigurations = 
      Begin PaneConfiguration = 0
         NumPanes = 4
         Configuration = "(H (1[40] 4[20] 2[20] 3) )"
      End
      Begin PaneConfiguration = 1
         NumPanes = 3
         Configuration = "(H (1 [50] 4 [25] 3))"
      End
      Begin PaneConfiguration = 2
         NumPanes = 3
         Configuration = "(H (1 [50] 2 [25] 3))"
      End
      Begin PaneConfiguration = 3
         NumPanes = 3
         Configuration = "(H (4 [30] 2 [40] 3))"
      End
      Begin PaneConfiguration = 4
         NumPanes = 2
         Configuration = "(H (1 [56] 3))"
      End
      Begin PaneConfiguration = 5
         NumPanes = 2
         Configuration = "(H (2 [66] 3))"
      End
      Begin PaneConfiguration = 6
         NumPanes = 2
         Configuration = "(H (4 [50] 3))"
      End
      Begin PaneConfiguration = 7
         NumPanes = 1
         Configuration = "(V (3))"
      End
      Begin PaneConfiguration = 8
         NumPanes = 3
         Configuration = "(H (1[56] 4[18] 2) )"
      End
      Begin PaneConfiguration = 9
         NumPanes = 2
         Configuration = "(H (1 [75] 4))"
      End
      Begin PaneConfiguration = 10
         NumPanes = 2
         Configuration = "(H (1[66] 2) )"
      End
      Begin PaneConfiguration = 11
         NumPanes = 2
         Configuration = "(H (4 [60] 2))"
      End
      Begin PaneConfiguration = 12
         NumPanes = 1
         Configuration = "(H (1) )"
      End
      Begin PaneConfiguration = 13
         NumPanes = 1
         Configuration = "(V (4))"
      End
      Begin PaneConfiguration = 14
         NumPanes = 1
         Configuration = "(V (2))"
      End
      ActivePaneConfig = 0
   End
   Begin DiagramPane = 
      Begin Origin = 
         Top = 0
         Left = 0
      End
      Begin Tables = 
         Begin Table = "sf"
            Begin Extent = 
               Top = 6
               Left = 38
               Bottom = 136
               Right = 208
            End
            DisplayFlags = 280
            TopColumn = 0
         End
         Begin Table = "s"
            Begin Extent = 
               Top = 157
               Left = 942
               Bottom = 287
               Right = 1112
            End
            DisplayFlags = 280
            TopColumn = 7
         End
         Begin Table = "p"
            Begin Extent = 
               Top = 15
               Left = 944
               Bottom = 128
               Right = 1114
            End
            DisplayFlags = 280
            TopColumn = 0
         End
      End
   End
   Begin SQLPane = 
   End
   Begin DataPane = 
      Begin ParameterDefaults = ""
      End
      Begin ColumnWidths = 14
         Width = 284
         Width = 1500
         Width = 1500
         Width = 1500
         Width = 1500
         Width = 1500
         Width = 1500
         Width = 1500
         Width = 1500
         Width = 1500
         Width = 1500
         Width = 1500
         Width = 1500
         Width = 1500
      End
   End
   Begin CriteriaPane = 
      Begin ColumnWidths = 11
         Column = 1440
         Alias = 900
         Table = 1170
         Output = 720
         Append = 1400
         NewValue = 1170
         SortType = 1350
         SortOrder = 1410
         GroupBy = 1350
         Filter = 1350
         Or = 1350
         Or = 1350
         Or = 1350
      End
   End
End
' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'VIEW',@level1name=N'SongFiltersView'
GO

EXEC sys.sp_addextendedproperty @name=N'MS_DiagramPaneCount', @value=1 , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'VIEW',@level1name=N'SongFiltersView'
GO


