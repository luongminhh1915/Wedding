$connDev = "Server=localhost;Database=WeddingMatch_Dev;Integrated Security=true;TrustServerCertificate=true"
$jsonRaw = [System.IO.File]::ReadAllText("$PSScriptRoot/seed-users.json", [System.Text.Encoding]::UTF8)
$users = $jsonRaw | ConvertFrom-Json

$cn = New-Object System.Data.SqlClient.SqlConnection($connDev)
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
Write-Host "Updated $updated users with clean Vietnamese FullName on Local Dev!"

