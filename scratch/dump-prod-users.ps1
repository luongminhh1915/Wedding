$connProd = "Server=sql1004.site4now.net;Database=db_ad0009_weddingmatch;User Id=db_ad0009_weddingmatch_admin;Password=T@i2101200622122005;Encrypt=true;TrustServerCertificate=true;Connect Timeout=15"
$cn = New-Object System.Data.SqlClient.SqlConnection($connProd)
$cn.Open()
$cmd = $cn.CreateCommand()
$cmd.CommandText = "SELECT u.Id, u.FullName, u.Email, u.LoginName, u.Role, u.IsActive, u.CreatedAt, v.BrandName FROM Users u LEFT JOIN Vendors v ON u.Id = v.UserId ORDER BY u.Role, u.CreatedAt"
$r = $cmd.ExecuteReader()
$users = @()
while ($r.Read()) {
    $users += [PSCustomObject]@{
        Id = $r["Id"].ToString()
        FullName = $r["FullName"].ToString()
        Email = $r["Email"].ToString()
        LoginName = $r["LoginName"].ToString()
        Role = $r["Role"].ToString()
        IsActive = [bool]$r["IsActive"]
        CreatedAt = $r["CreatedAt"].ToString()
        BrandName = $r["BrandName"].ToString()
    }
}
$cn.Close()
Write-Host "Total users in production: $($users.Count)"
$users | Group-Object Role | ForEach-Object { Write-Host "$($_.Name): $($_.Count)" }

# Export to json for analysis
$users | ConvertTo-Json -Depth 5 | Set-Content "scratch/all-prod-users.json" -Encoding UTF8
Write-Host "Exported to scratch/all-prod-users.json"

