$connProd = "Server=sql1004.site4now.net;Database=db_ad0009_weddingmatch;User Id=db_ad0009_weddingmatch_admin;Password=T@i2101200622122005;Encrypt=true;TrustServerCertificate=true;Connect Timeout=15"
$cn = New-Object System.Data.SqlClient.SqlConnection($connProd)
$cn.Open()
$cmd = $cn.CreateCommand()
$cmd.CommandText = "SELECT TOP 10 Id, FullName, Email, PhoneNumber, Role, IsActive, CreatedAt, LastLoginAt, SecurityVersion FROM Users WHERE Role='Customer' ORDER BY CreatedAt DESC"
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    Write-Host "$($r['FullName']) | $($r['Email']) | $($r['PhoneNumber']) | $($r['Role']) | Active: $($r['IsActive']) | Created: $($r['CreatedAt']) | LastLogin: $($r['LastLoginAt'])"
}
$cn.Close()

