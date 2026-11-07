$connProd = "Server=sql1004.site4now.net;Database=db_ad0009_weddingmatch;User Id=db_ad0009_weddingmatch_admin;Password=T@i2101200622122005;Encrypt=true;TrustServerCertificate=true;Connect Timeout=30"
$jsonRaw = [System.IO.File]::ReadAllText("$PSScriptRoot/seed-users.json", [System.Text.Encoding]::UTF8)
$users = $jsonRaw | ConvertFrom-Json

$cn = New-Object System.Data.SqlClient.SqlConnection($connProd)
$cn.Open()
$cmd = $cn.CreateCommand()
$cmd.CommandText = "UPDATE Users SET FullName = @fullName WHERE Email = @email"
$pName = $cmd.Parameters.Add("@fullName", [System.Data.SqlDbType]::NVarChar, 150)
$pEmail = $cmd.Parameters.Add("@email", [System.Data.SqlDbType]::NVarChar, 150)

$updated = 0
foreach ($u in $users) {
    $pName.Value = $u.fullName
    $pEmail.Value = $u.email
    $rows = $cmd.ExecuteNonQuery()
    if ($rows -gt 0) { $updated++ }
}
$cn.Close()
Write-Host "Updated $updated users with clean Vietnamese FullName on Production!"

