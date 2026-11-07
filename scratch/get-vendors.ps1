$connProd = "Server=sql1004.site4now.net;Database=db_ad0009_weddingmatch;User Id=db_ad0009_weddingmatch_admin;Password=T@i2101200622122005;Encrypt=true;TrustServerCertificate=true;Connect Timeout=15"
$cn = New-Object System.Data.SqlClient.SqlConnection($connProd)
$cn.Open()
$cmd = $cn.CreateCommand()
$cmd.CommandText = "SELECT TOP 5 Email, PasswordHash FROM Users WHERE Role='VendorOwner'"
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    Write-Host "Vendor Email: $($r['Email']) | Hash: $($r['PasswordHash'])"
}
$cn.Close()

