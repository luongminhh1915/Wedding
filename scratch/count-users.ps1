$connLocal = "Server=localhost;Database=WeddingMatch_Dev;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5"
try {
    $cn = New-Object System.Data.SqlClient.SqlConnection($connLocal)
    $cn.Open()
    $cmd = $cn.CreateCommand()
    $cmd.CommandText = "SELECT Role, COUNT(*) as Total, SUM(CASE WHEN IsActive=1 THEN 1 ELSE 0 END) as Active FROM Users GROUP BY Role"
    $r = $cmd.ExecuteReader()
    Write-Host "--- LOCAL SQL SERVER (WeddingMatch_Dev) ---"
    while ($r.Read()) {
        Write-Host "Role: $($r['Role']) | Total: $($r['Total']) | Active: $($r['Active'])"
    }
    $r.Close()
    $cmdTotal = $cn.CreateCommand()
    $cmdTotal.CommandText = "SELECT COUNT(*) as Total, SUM(CASE WHEN IsActive=1 THEN 1 ELSE 0 END) as Active FROM Users"
    $rTotal = $cmdTotal.ExecuteReader()
    if ($rTotal.Read()) {
        Write-Host ">> OVERALL: Total: $($rTotal['Total']) | Active: $($rTotal['Active'])"
    }
    $cn.Close()
} catch {
    Write-Host "Local SQL error: $($_.Exception.Message)"
}

$connProd = "Server=sql1004.site4now.net;Database=db_ad0009_weddingmatch;User Id=db_ad0009_weddingmatch_admin;Password=T@i2101200622122005;Encrypt=true;TrustServerCertificate=true;Connect Timeout=15"
try {
    $cn2 = New-Object System.Data.SqlClient.SqlConnection($connProd)
    $cn2.Open()
    $cmd2 = $cn2.CreateCommand()
    $cmd2.CommandText = "SELECT Role, COUNT(*) as Total, SUM(CASE WHEN IsActive=1 THEN 1 ELSE 0 END) as Active FROM Users GROUP BY Role"
    $r2 = $cmd2.ExecuteReader()
    Write-Host "`n--- PROD SQL SERVER (sql1004.site4now.net) ---"
    while ($r2.Read()) {
        Write-Host "Role: $($r2['Role']) | Total: $($r2['Total']) | Active: $($r2['Active'])"
    }
    $r2.Close()
    $cmdTotal2 = $cn2.CreateCommand()
    $cmdTotal2.CommandText = "SELECT COUNT(*) as Total, SUM(CASE WHEN IsActive=1 THEN 1 ELSE 0 END) as Active FROM Users"
    $rTotal2 = $cmdTotal2.ExecuteReader()
    if ($rTotal2.Read()) {
        Write-Host ">> OVERALL: Total: $($rTotal2['Total']) | Active: $($rTotal2['Active'])"
    }
    $cn2.Close()
} catch {
    Write-Host "Prod SQL error: $($_.Exception.Message)"
}

