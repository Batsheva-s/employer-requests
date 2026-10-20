/*
    Performance measurements on the test data (100,000 requests).

    Every query below is the SQL that EF Core sends for an API call, copied from the application log
    (log level Information for Microsoft.EntityFrameworkCore.Database.Command), with realistic parameter values.
    Queries with parameters run through sp_executesql, exactly as the application sends them, so SQL Server
    compiles them the same way.

    Some queries run a second time WITH (INDEX(0)): that forces a scan of the whole table (the clustered index)
    and shows what the same query costs without the index that serves it.

    How to run (SSMS):
      1. Create the test data:  dotnet run --project src/EmployerRequests.Api -- seed
      2. Connect to (localdb)\MSSQLLocalDB, open this file, and turn on Include Actual Execution Plan (Ctrl+M).
      3. Run it twice (F5). The second run has the data in memory, like a server that is in use.
      Messages tab:       logical reads (pages read) and CPU / elapsed time of every query.
      Execution plan tab: how SQL Server executed every query – Index Seek, Index Scan, Key Lookup, Sort.

    The results and their analysis are in docs/PERFORMANCE.md.
*/
USE EmployerRequests;
GO

SET NOCOUNT ON;
SET STATISTICS IO, TIME ON;

DECLARE @now datetimeoffset(7) = SYSDATETIMEOFFSET();
DECLARE @ninetyDaysAgo datetimeoffset(7) = DATEADD(DAY, -90, @now);


PRINT N'=== 1.1 List, no filters: count';
SELECT COUNT(*)
FROM [Requests] AS [r];

PRINT N'=== 1.2 List, no filters: first page, newest first';
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC
OFFSET @__p_0 ROWS FETCH NEXT @__p_1 ROWS ONLY', N'@__p_0 int,@__p_1 int', @__p_0 = 0, @__p_1 = 20;

PRINT N'=== 1.3 List, no filters: first page – without the index';
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r] WITH (INDEX(0))
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC
OFFSET @__p_0 ROWS FETCH NEXT @__p_1 ROWS ONLY', N'@__p_0 int,@__p_1 int', @__p_0 = 0, @__p_1 = 20;


PRINT N'=== 2.1 List, status Waiting: count';
SELECT COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[Status] = CAST(3 AS tinyint);

PRINT N'=== 2.2 List, status Waiting: first page';
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
WHERE [r].[Status] = CAST(3 AS tinyint)
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC
OFFSET @__p_1 ROWS FETCH NEXT @__p_2 ROWS ONLY', N'@__p_1 int,@__p_2 int', @__p_1 = 0, @__p_2 = 20;

PRINT N'=== 2.3 List, status Waiting: count – without the index';
SELECT COUNT(*)
FROM [Requests] AS [r] WITH (INDEX(0))
WHERE [r].[Status] = CAST(3 AS tinyint);


PRINT N'=== 3.1 List, status New or Waiting: count';
SELECT COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[Status] IN (CAST(1 AS tinyint), CAST(3 AS tinyint));

PRINT N'=== 3.2 List, status New or Waiting: first page';
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
WHERE [r].[Status] IN (CAST(1 AS tinyint), CAST(3 AS tinyint))
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC
OFFSET @__p_1 ROWS FETCH NEXT @__p_2 ROWS ONLY', N'@__p_1 int,@__p_2 int', @__p_1 = 0, @__p_2 = 20;


PRINT N'=== 4.1 List, organization starts with "אלון": count';
EXEC sp_executesql N'SELECT COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[OrganizationName] LIKE @__prefix_0_startswith ESCAPE N''\''', N'@__prefix_0_startswith nvarchar(200)', @__prefix_0_startswith = N'אלון%';

PRINT N'=== 4.2 List, organization starts with "אלון": first page';
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
WHERE [r].[OrganizationName] LIKE @__prefix_0_startswith ESCAPE N''\''
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC
OFFSET @__p_1 ROWS FETCH NEXT @__p_2 ROWS ONLY', N'@__prefix_0_startswith nvarchar(200),@__p_1 int,@__p_2 int', @__prefix_0_startswith = N'אלון%', @__p_1 = 0, @__p_2 = 20;

PRINT N'=== 4.3 List, organization starts with "אלון": count – without the index';
EXEC sp_executesql N'SELECT COUNT(*)
FROM [Requests] AS [r] WITH (INDEX(0))
WHERE [r].[OrganizationName] LIKE @__prefix_0_startswith ESCAPE N''\''', N'@__prefix_0_startswith nvarchar(200)', @__prefix_0_startswith = N'אלון%';


PRINT N'=== 5.1 List, organization starts with "אלון", created in the last 90 days: count';
EXEC sp_executesql N'SELECT COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[OrganizationName] LIKE @__prefix_0_startswith ESCAPE N''\'' AND [r].[CreatedAt] >= @__from_1 AND [r].[CreatedAt] < @__to_2', N'@__prefix_0_startswith nvarchar(200),@__from_1 datetimeoffset(7),@__to_2 datetimeoffset(7)', @__prefix_0_startswith = N'אלון%', @__from_1 = @ninetyDaysAgo, @__to_2 = @now;

PRINT N'=== 5.2 List, organization starts with "אלון", created in the last 90 days: first page';
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
WHERE [r].[OrganizationName] LIKE @__prefix_0_startswith ESCAPE N''\'' AND [r].[CreatedAt] >= @__from_1 AND [r].[CreatedAt] < @__to_2
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC
OFFSET @__p_3 ROWS FETCH NEXT @__p_4 ROWS ONLY', N'@__prefix_0_startswith nvarchar(200),@__from_1 datetimeoffset(7),@__to_2 datetimeoffset(7),@__p_3 int,@__p_4 int', @__prefix_0_startswith = N'אלון%', @__from_1 = @ninetyDaysAgo, @__to_2 = @now, @__p_3 = 0, @__p_4 = 20;


PRINT N'=== 6.1 List, handler starts with "שרה": count';
EXEC sp_executesql N'SELECT COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[AssignedTo] LIKE @__prefix_0_startswith ESCAPE N''\''', N'@__prefix_0_startswith nvarchar(100)', @__prefix_0_startswith = N'שרה%';

PRINT N'=== 6.2 List, handler starts with "שרה": first page, highest priority first';
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
WHERE [r].[AssignedTo] LIKE @__prefix_0_startswith ESCAPE N''\''
ORDER BY [r].[Priority] DESC, [r].[Id] DESC
OFFSET @__p_1 ROWS FETCH NEXT @__p_2 ROWS ONLY', N'@__prefix_0_startswith nvarchar(100),@__p_1 int,@__p_2 int', @__prefix_0_startswith = N'שרה%', @__p_1 = 0, @__p_2 = 20;

PRINT N'=== 6.3 List, handler starts with "שרה": count – without the index';
EXEC sp_executesql N'SELECT COUNT(*)
FROM [Requests] AS [r] WITH (INDEX(0))
WHERE [r].[AssignedTo] LIKE @__prefix_0_startswith ESCAPE N''\''', N'@__prefix_0_startswith nvarchar(100)', @__prefix_0_startswith = N'שרה%';


PRINT N'=== 7.1 List, text search "אישור": count';
EXEC sp_executesql N'SELECT COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[Title] LIKE @__text_0_contains ESCAPE N''\'' OR [r].[OrganizationName] LIKE @__text_0_contains ESCAPE N''\''', N'@__text_0_contains nvarchar(200)', @__text_0_contains = N'%אישור%';

PRINT N'=== 7.2 List, text search "אישור": first page';
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
WHERE [r].[Title] LIKE @__text_0_contains ESCAPE N''\'' OR [r].[OrganizationName] LIKE @__text_0_contains ESCAPE N''\''
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC
OFFSET @__p_1 ROWS FETCH NEXT @__p_2 ROWS ONLY', N'@__text_0_contains nvarchar(200),@__p_1 int,@__p_2 int', @__text_0_contains = N'%אישור%', @__p_1 = 0, @__p_2 = 20;


PRINT N'=== 7.3 List, text search with no match ("קקקק"): count';
EXEC sp_executesql N'SELECT COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[Title] LIKE @__text_0_contains ESCAPE N''\'' OR [r].[OrganizationName] LIKE @__text_0_contains ESCAPE N''\''', N'@__text_0_contains nvarchar(200)', @__text_0_contains = N'%קקקק%';

PRINT N'=== 7.4 List, text search with no match ("קקקק"): first page';
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
WHERE [r].[Title] LIKE @__text_0_contains ESCAPE N''\'' OR [r].[OrganizationName] LIKE @__text_0_contains ESCAPE N''\''
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC
OFFSET @__p_1 ROWS FETCH NEXT @__p_2 ROWS ONLY', N'@__text_0_contains nvarchar(200),@__p_1 int,@__p_2 int', @__text_0_contains = N'%קקקק%', @__p_1 = 0, @__p_2 = 20;


PRINT N'=== 8.1 List, no filters: page 2,000 (OFFSET 39,980)';
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC
OFFSET @__p_0 ROWS FETCH NEXT @__p_1 ROWS ONLY', N'@__p_0 int,@__p_1 int', @__p_0 = 39980, @__p_1 = 20;


PRINT N'=== 9.1 Summary, no filters: counts by status and priority';
SELECT [r].[Status], [r].[Priority], COUNT(*)
FROM [Requests] AS [r]
GROUP BY [r].[Status], [r].[Priority];

PRINT N'=== 9.2 Summary, no filters: the five handlers with the most open requests';
EXEC sp_executesql N'SELECT TOP(@__p_0) [r].[AssignedTo], COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[AssignedTo] IS NOT NULL AND [r].[Status] <> CAST(4 AS tinyint)
GROUP BY [r].[AssignedTo]
ORDER BY COUNT(*) DESC, [r].[AssignedTo]', N'@__p_0 int', @__p_0 = 5;

PRINT N'=== 9.3 Summary, no filters: counts by status and priority – without the index';
SELECT [r].[Status], [r].[Priority], COUNT(*)
FROM [Requests] AS [r] WITH (INDEX(0))
GROUP BY [r].[Status], [r].[Priority];

PRINT N'=== 9.4 Summary, no filters: the five handlers with the most open requests – without the index';
EXEC sp_executesql N'SELECT TOP(@__p_0) [r].[AssignedTo], COUNT(*)
FROM [Requests] AS [r] WITH (INDEX(0))
WHERE [r].[AssignedTo] IS NOT NULL AND [r].[Status] <> CAST(4 AS tinyint)
GROUP BY [r].[AssignedTo]
ORDER BY COUNT(*) DESC, [r].[AssignedTo]', N'@__p_0 int', @__p_0 = 5;


PRINT N'=== 10.1 Summary, priority High: counts by status and priority';
SELECT [r].[Status], [r].[Priority], COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[Priority] = CAST(3 AS tinyint)
GROUP BY [r].[Status], [r].[Priority];

PRINT N'=== 10.2 Summary, priority High: the five handlers with the most open requests';
EXEC sp_executesql N'SELECT TOP(@__p_1) [r].[AssignedTo], COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[Priority] = CAST(3 AS tinyint) AND [r].[AssignedTo] IS NOT NULL AND [r].[Status] <> CAST(4 AS tinyint)
GROUP BY [r].[AssignedTo]
ORDER BY COUNT(*) DESC, [r].[AssignedTo]', N'@__p_1 int', @__p_1 = 5;


PRINT N'=== 11.1 Summary, text search "אישור": counts by status and priority';
EXEC sp_executesql N'SELECT [r].[Status], [r].[Priority], COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[Title] LIKE @__text_0_contains ESCAPE N''\'' OR [r].[OrganizationName] LIKE @__text_0_contains ESCAPE N''\''
GROUP BY [r].[Status], [r].[Priority]', N'@__text_0_contains nvarchar(200)', @__text_0_contains = N'%אישור%';

PRINT N'=== 11.2 Summary, text search "אישור": the five handlers with the most open requests';
EXEC sp_executesql N'SELECT TOP(@__p_1) [r].[AssignedTo], COUNT(*)
FROM [Requests] AS [r]
WHERE ([r].[Title] LIKE @__text_0_contains ESCAPE N''\'' OR [r].[OrganizationName] LIKE @__text_0_contains ESCAPE N''\'') AND [r].[AssignedTo] IS NOT NULL AND [r].[Status] <> CAST(4 AS tinyint)
GROUP BY [r].[AssignedTo]
ORDER BY COUNT(*) DESC, [r].[AssignedTo]', N'@__p_1 int,@__text_0_contains nvarchar(200)', @__p_1 = 5, @__text_0_contains = N'%אישור%';


PRINT N'=== 12.1 One request: details';
EXEC sp_executesql N'SELECT TOP(1) [r].[Id], [r].[AssignedTo], [r].[CreatedAt], [r].[OrganizationName], [r].[Priority], [r].[RowVersion], [r].[Status], [r].[Title], [r].[UpdatedAt]
FROM [Requests] AS [r]
WHERE [r].[Id] = @__id_0', N'@__id_0 int', @__id_0 = 1;

PRINT N'=== 12.2 One request: history, newest first';
EXEC sp_executesql N'SELECT [r].[Id], [r].[PreviousStatus], [r].[NewStatus], [r].[ChangedBy], [r].[ChangedAt]
FROM [RequestStatusChanges] AS [r]
WHERE [r].[RequestId] = @__requestId_0
ORDER BY [r].[ChangedAt] DESC, [r].[Id] DESC', N'@__requestId_0 int', @__requestId_0 = 1;


PRINT N'=== 13.1 Status change: history row and conditional UPDATE in one batch (rolled back)';
DECLARE @currentVersion binary(8), @currentStatus tinyint;
SELECT @currentVersion = [RowVersion], @currentStatus = [Status] FROM [Requests] WHERE [Id] = 1;

BEGIN TRANSACTION;
-- 0 rows updated would mean that someone changed the request after it was loaded: EF Core then throws
-- DbUpdateConcurrencyException, the transaction is rolled back, and the API answers 409.
EXEC sp_executesql N'SET NOCOUNT ON;
INSERT INTO [RequestStatusChanges] ([ChangedAt], [ChangedBy], [NewStatus], [PreviousStatus], [RequestId])
OUTPUT INSERTED.[Id]
VALUES (@p0, @p1, @p2, @p3, @p4);
UPDATE [Requests] SET [Status] = @p5, [UpdatedAt] = @p6
OUTPUT INSERTED.[RowVersion]
WHERE [Id] = @p7 AND [RowVersion] = @p8;', N'@p0 datetimeoffset(7),@p1 nvarchar(100),@p2 tinyint,@p3 tinyint,@p4 int,@p7 int,@p8 varbinary(8),@p5 tinyint,@p6 datetimeoffset(7)', @p0 = @now, @p1 = N'measure.sql', @p2 = 2, @p3 = @currentStatus, @p4 = 1, @p7 = 1, @p8 = @currentVersion, @p5 = 2, @p6 = @now;
ROLLBACK TRANSACTION;


/*
    14. Alternatives measured for the improvements proposed in docs/PERFORMANCE.md.
    The application does not send these queries.
*/

PRINT N'=== 14.1 Alternative: text search count with a binary collation';
EXEC sp_executesql N'SELECT COUNT(*)
FROM [Requests] AS [r]
WHERE [r].[Title] COLLATE Latin1_General_100_BIN2 LIKE @__text_0_contains ESCAPE N''\'' OR [r].[OrganizationName] COLLATE Latin1_General_100_BIN2 LIKE @__text_0_contains ESCAPE N''\''', N'@__text_0_contains nvarchar(200)', @__text_0_contains = N'%אישור%';

PRINT N'=== 14.2 Alternative: text search count with a binary collation, case-insensitive (UPPER on both sides)';
EXEC sp_executesql N'SELECT COUNT(*)
FROM [Requests] AS [r]
WHERE UPPER([r].[Title]) COLLATE Latin1_General_100_BIN2 LIKE UPPER(@__text_0_contains) ESCAPE N''\'' OR UPPER([r].[OrganizationName]) COLLATE Latin1_General_100_BIN2 LIKE UPPER(@__text_0_contains) ESCAPE N''\''', N'@__text_0_contains nvarchar(200)', @__text_0_contains = N'%אישור%';

-- The last row of page 1,999 – where page 2,000 starts. Not measured.
SET STATISTICS IO, TIME OFF;
DECLARE @lastCreatedAt datetimeoffset(7), @lastId int;
SELECT @lastCreatedAt = [CreatedAt], @lastId = [Id]
FROM [Requests]
ORDER BY [CreatedAt] DESC, [Id] DESC
OFFSET 39979 ROWS FETCH NEXT 1 ROWS ONLY;
SET STATISTICS IO, TIME ON;

PRINT N'=== 14.3 Alternative: page 2,000 with keyset paging (continue after the last row of the previous page)';
EXEC sp_executesql N'SELECT TOP(20) [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
WHERE [r].[CreatedAt] < @lastCreatedAt OR ([r].[CreatedAt] = @lastCreatedAt AND [r].[Id] < @lastId)
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC', N'@lastCreatedAt datetimeoffset(7),@lastId int', @lastCreatedAt = @lastCreatedAt, @lastId = @lastId;

PRINT N'=== 14.4 Alternative: page 2,000 – OFFSET on the index only, then the 20 rows';
EXEC sp_executesql N'SELECT [r].[Id], [r].[Title], [r].[OrganizationName], [r].[Status], [r].[Priority], [r].[AssignedTo], [r].[CreatedAt], [r].[UpdatedAt], [r].[RowVersion]
FROM [Requests] AS [r]
WHERE [r].[Id] IN (
    SELECT [p].[Id]
    FROM [Requests] AS [p]
    ORDER BY [p].[CreatedAt] DESC, [p].[Id] DESC
    OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY)
ORDER BY [r].[CreatedAt] DESC, [r].[Id] DESC', N'@skip int,@take int', @skip = 39980, @take = 20;


SET STATISTICS IO, TIME OFF;
