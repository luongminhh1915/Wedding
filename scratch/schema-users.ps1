$connProd = "Server=sql1004.site4now.net;Database=db_ad0009_weddingmatch;User Id=db_ad0009_weddingmatch_admin;Password=T@i2101200622122005;Encrypt=true;TrustServerCertificate=true;Connect Timeout=15"
$cn = New-Object System.Data.SqlClient.SqlConnection($connProd)
$cn.Open()
$cmd = $cn.CreateCommand()
$cmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' ORDER BY ORDINAL_POSITION"
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    Write-Host "$($r['COLUMN_NAME']) | $($r['DATA_TYPE']) | Nullable: $($r['IS_NULLABLE']) | MaxLen: $($r['CHARACTER_MAXIMUM_LENGTH'])"
}
$cn.Close()

