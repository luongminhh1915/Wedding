SET NOCOUNT ON;
DECLARE @q nvarchar(max);
SELECT @q=STRING_AGG(CONVERT(nvarchar(max),
N'SELECT N'''+s.name+N'.'+t.name+N''' AS [TableName],COUNT_BIG(*) AS [RecordCount],CONVERT(varchar(64),HASHBYTES(''SHA2_256'',(SELECT '+cols.names+N' FROM '+QUOTENAME(s.name)+N'.'+QUOTENAME(t.name)+N' ORDER BY '+CASE WHEN t.name=N'VendorBusinessGroups' THEN N'[VendorId],[BusinessGroupId]' ELSE N'[Id]' END+N' FOR JSON PATH,INCLUDE_NULL_VALUES)),2) AS [RowsSha256] FROM '+QUOTENAME(s.name)+N'.'+QUOTENAME(t.name)),N' UNION ALL ') WITHIN GROUP (ORDER BY s.name,t.name)
FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
CROSS APPLY (SELECT STRING_AGG(CONVERT(nvarchar(max),QUOTENAME(c.name)),N',') WITHIN GROUP(ORDER BY c.column_id) AS names FROM sys.columns c WHERE c.object_id=t.object_id
  AND NOT(t.name=N'Listings' AND c.name=N'PricingServiceItemId')
  AND NOT(t.name=N'ComboTemplates' AND c.name IN(N'PricingMode',N'ComboPrice',N'OriginalPrice'))
  AND NOT(t.name IN(N'ComboTemplateItems',N'ComboOrderItems') AND c.name=N'ComponentSnapshotJson')) cols
WHERE t.is_ms_shipped=0 AND t.name NOT IN(N'__EFMigrationsHistory',N'PrivateServiceAgreements');
EXEC sys.sp_executesql @q;
