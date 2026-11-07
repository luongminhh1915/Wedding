$connProd = "Server=sql1004.site4now.net;Database=db_ad0009_weddingmatch;User Id=db_ad0009_weddingmatch_admin;Password=T@i2101200622122005;Encrypt=true;TrustServerCertificate=true;Connect Timeout=30"
$cn = New-Object System.Data.SqlClient.SqlConnection($connProd)
$cn.Open()
$cmd = $cn.CreateCommand()
$cmd.CommandText = "SELECT u.Id, u.FullName, u.Email, u.LoginName, u.Role, u.PhoneNumber, u.IsActive, u.PasswordHash, v.BrandName FROM Users u LEFT JOIN Vendors v ON u.Id = v.UserId ORDER BY u.Role, u.CreatedAt"
$r = $cmd.ExecuteReader()
$users = @()
while ($r.Read()) {
    $users += [PSCustomObject]@{
        Id = $r["Id"].ToString()
        FullName = $r["FullName"].ToString()
        Email = $r["Email"].ToString()
        LoginName = $r["LoginName"].ToString()
        Role = $r["Role"].ToString()
        PhoneNumber = $r["PhoneNumber"].ToString()
        IsActive = [bool]$r["IsActive"]
        PasswordHash = $r["PasswordHash"].ToString()
        BrandName = $r["BrandName"].ToString()
    }
}
$cn.Close()

$users | ConvertTo-Json -Depth 5 | Set-Content "scratch/all-users-detail.json" -Encoding UTF8
Write-Host "Done dumping $($users.Count) users to scratch/all-users-detail.json"

