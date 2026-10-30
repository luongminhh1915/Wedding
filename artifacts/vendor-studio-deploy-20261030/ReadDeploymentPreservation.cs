using Microsoft.Data.SqlClient;
using System.Security.Cryptography;
using System.Text.Json;

var phase="Arguments";
try {
    if(args.Length!=2 || args[0] is not("snapshot" or "verify")) throw new InvalidOperationException("snapshot|verify privateSnapshotPath");
    var privateRoot=Path.GetFullPath(".deployment/private/vendor-studio-release-20261030")+Path.DirectorySeparatorChar;
    var snapshotPath=Path.GetFullPath(args[1]);
    if(!snapshotPath.StartsWith(privateRoot,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Private snapshot path required");
    if(args[0]=="snapshot" && File.Exists(snapshotPath))throw new InvalidOperationException("Snapshot already exists; original preserved");
    using var config=JsonDocument.Parse(File.ReadAllText(Path.Combine(privateRoot,"wedding.production.before.json")));
    var b=new SqlConnectionStringBuilder(config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString());
    if(b.DataSource!="sql1004.site4now.net"||b.InitialCatalog!="db_ad0009_weddingmatch")throw new InvalidOperationException("Target mismatch");
    b.ApplicationIntent=ApplicationIntent.ReadOnly;b.ConnectTimeout=20;
    phase="ReadTablesOnly";
    await using var sql=new SqlConnection(b.ConnectionString);await sql.OpenAsync();
    var names=new List<string>();
    await using(var cmd=new SqlCommand("SELECT name FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo') AND is_ms_shipped=0 ORDER BY name",sql))
    await using(var rd=await cmd.ExecuteReaderAsync()){while(await rd.ReadAsync())names.Add(rd.GetString(0));}
    var tables=new Dictionary<string,TableSnapshot>();
    foreach(var name in names){
        var columns=new List<string>();var keys=new List<string>();var ignored=new List<string>();
        await using(var cmd=new SqlCommand("SELECT c.name,c.system_type_id,ISNULL(ic.key_ordinal,0) FROM sys.columns c LEFT JOIN sys.indexes i ON i.object_id=c.object_id AND i.is_primary_key=1 LEFT JOIN sys.index_columns ic ON ic.object_id=c.object_id AND ic.index_id=i.index_id AND ic.column_id=c.column_id WHERE c.object_id=OBJECT_ID(@table) ORDER BY c.column_id",sql)){
            cmd.Parameters.AddWithValue("@table","dbo."+name);
            await using var rd=await cmd.ExecuteReaderAsync();
            var orderedKeys=new SortedDictionary<int,string>();
            while(await rd.ReadAsync()){
                var col=rd.GetString(0);var type=rd.GetByte(1);var keyOrdinal=Convert.ToInt32(rd.GetValue(2));
                if(keyOrdinal>0)orderedKeys[keyOrdinal]=col;
                if(type==189 || (name=="Users"&&col=="LastLoginAt") || (name=="Notifications" && new[]{"ReadAt","SentAt","NextAttemptAt","Attempts","DeliveryError","UpdatedAt"}.Contains(col)))ignored.Add(col);
                else columns.Add(col);
            }
            keys.AddRange(orderedKeys.Values);
        }
        if(keys.Count==0)throw new InvalidOperationException("No stable primary key for "+name);
        var rows=new Dictionary<string,string>();
        static string Q(string s)=>"["+s.Replace("]","]]")+"]";
        await using(var cmd=new SqlCommand("SELECT "+string.Join(",",columns.Select(Q))+" FROM dbo."+Q(name),sql))
        await using(var rd=await cmd.ExecuteReaderAsync()){
            while(await rd.ReadAsync()){
                var values=new object?[columns.Count];for(var i=0;i<values.Length;i++)values[i]=rd.IsDBNull(i)?null:rd.GetValue(i);
                var keyValues=keys.Select(k=>values[columns.IndexOf(k)]).ToArray();
                var key=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(keyValues)));
                var hash=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(values)));
                if(!rows.TryAdd(key,hash))throw new InvalidOperationException("Duplicate key digest");
            }
        }
        tables[name]=new(columns,ignored,rows);
    }
    var current=new Snapshot(DateTime.UtcNow,tables);
    if(args[0]=="snapshot"){
        await File.WriteAllTextAsync(snapshotPath,JsonSerializer.Serialize(current));
        Console.WriteLine(JsonSerializer.Serialize(new{status="PrivateHashSnapshotCreated",tableCount=tables.Count,totalRows=tables.Values.Sum(t=>t.Rows.Count),readOnlyQueries=true}));
    }else{
        var before=JsonSerializer.Deserialize<Snapshot>(await File.ReadAllTextAsync(snapshotPath))??throw new InvalidOperationException("Invalid snapshot");
        var differences=new List<object>();var rowsVerified=0;var allowedAddedRows=0;
        foreach(var(table,old)in before.Tables){
            if(!tables.TryGetValue(table,out var now)){differences.Add(new{table,kind="MissingTable"});continue;}
            if(!old.Columns.SequenceEqual(now.Columns)||!old.IgnoredColumns.SequenceEqual(now.IgnoredColumns)){differences.Add(new{table,kind="ColumnScopeChanged"});continue;}
            var missing=old.Rows.Keys.Count(k=>!now.Rows.ContainsKey(k));
            var changed=old.Rows.Count(p=>now.Rows.TryGetValue(p.Key,out var hash)&&hash!=p.Value);
            var added=now.Rows.Keys.Count(k=>!old.Rows.ContainsKey(k));
            rowsVerified+=old.Rows.Count-missing-changed;
            if(missing>0||changed>0)differences.Add(new{table,kind="ExistingRowsDiffer",missing,changed});
            if(added>0){if(table is "AuditLogs" or "Notifications")allowedAddedRows+=added;else differences.Add(new{table,kind="NewRows",added});}
        }
        foreach(var name in tables.Keys.Except(before.Tables.Keys))differences.Add(new{table=name,kind="AddedTable"});
        var result=new{status=differences.Count==0?"ExistingRowsPreserved":"DifferencesRequireReview",readOnlyQueries=true,beforeUtc=before.Utc,afterUtc=current.Utc,tablesVerified=before.Tables.Count,rowsVerified,allowedAddedRows,ignoredMutableColumns=new{Users="LastLoginAt",Notifications="ReadAt/SentAt/NextAttemptAt/Attempts/DeliveryError/UpdatedAt",AllTables="rowversion"},differences};
        var output=JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true});
        await File.WriteAllTextAsync("artifacts/vendor-studio-deploy-20261030/deployment-preservation-result.json",output);Console.WriteLine(output);
        if(differences.Count>0)Environment.ExitCode=1;
    }
}catch(Exception ex){Console.Error.WriteLine(JsonSerializer.Serialize(new{status="ReadOnlyPreservationFailed",phase,exceptionType=ex.GetType().Name}));Environment.ExitCode=1;}
record TableSnapshot(List<string> Columns,List<string> IgnoredColumns,Dictionary<string,string> Rows);
record Snapshot(DateTime Utc,Dictionary<string,TableSnapshot> Tables);
