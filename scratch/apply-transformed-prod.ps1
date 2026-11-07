[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$connStr = "Server=sql1004.site4now.net;Database=db_ad0009_weddingmatch;User Id=db_ad0009_weddingmatch_admin;Password=T@i2101200622122005;Encrypt=true;TrustServerCertificate=true;Connect Timeout=30"
$jsonRaw = [System.IO.File]::ReadAllText("$PSScriptRoot/transformed-customers.json", [System.Text.Encoding]::UTF8)
$customers = $jsonRaw | ConvertFrom-Json

$cn = New-Object System.Data.SqlClient.SqlConnection($connStr)
try {
    $cn.Open()
    Write-Host "Connected to Production SQL Server."

    $tran = $cn.BeginTransaction("ApplyCustomerUpdate")
    
    # Step 1: Set temporary unique emails to avoid any unique constraint collision during in-place swap
    $tempCmd = $cn.CreateCommand()
    $tempCmd.Transaction = $tran
    $tempCmd.CommandText = "UPDATE Users SET Email = 'temp_' + CAST(Id AS NVARCHAR(36)) + '@temp.com' WHERE Role = 'Customer'"
    $tempRows = $tempCmd.ExecuteNonQuery()
    Write-Host "Set temporary emails on $tempRows customers."

    # Step 2: Apply final clean details
    $updateCmd = $cn.CreateCommand()
    $updateCmd.Transaction = $tran
    $updateCmd.CommandText = @"
UPDATE Users 
SET FullName = @fullName,
    Email = @email,
    PhoneNumber = @phone,
    PasswordHash = @pwdHash,
    IsActive = @isActive,
    UpdatedAt = GETUTCDATE()
WHERE Id = @id
"@
    $paramId = $updateCmd.Parameters.Add("@id", [System.Data.SqlDbType]::UniqueIdentifier)
    $pName = $updateCmd.Parameters.Add("@fullName", [System.Data.SqlDbType]::NVarChar, 150)
    $pEmail = $updateCmd.Parameters.Add("@email", [System.Data.SqlDbType]::NVarChar, 150)
    $pPhone = $updateCmd.Parameters.Add("@phone", [System.Data.SqlDbType]::NVarChar, 20)
    $pHash = $updateCmd.Parameters.Add("@pwdHash", [System.Data.SqlDbType]::NVarChar, -1)
    $pActive = $updateCmd.Parameters.Add("@isActive", [System.Data.SqlDbType]::Bit)

    $updated = 0
    foreach ($c in $customers) {
        $paramId.Value = [Guid]::Parse($c.id)
        $pName.Value = $c.fullName
        $pEmail.Value = $c.email
        $pPhone.Value = $c.phoneNumber
        $pHash.Value = $c.passwordHash
        $pActive.Value = [bool]$c.isActive

        $rows = $updateCmd.ExecuteNonQuery()
        if ($rows -gt 0) { $updated++ }
    }

    $tran.Commit()
    Write-Host "Transaction committed! Successfully updated $updated customer accounts on Production!"

    # Verification query
    $verifyCmd = $cn.CreateCommand()
    $verifyCmd.CommandText = @"
SELECT 
    COUNT(*) as TotalUsers,
    SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END) as ActiveUsers,
    SUM(CASE WHEN Role = 'Customer' THEN 1 ELSE 0 END) as TotalCustomers,
    SUM(CASE WHEN Role = 'Customer' AND IsActive = 1 THEN 1 ELSE 0 END) as ActiveCustomers,
    SUM(CASE WHEN FullName LIKE N'%Lý%' THEN 1 ELSE 0 END) as LyCount,
    SUM(CASE WHEN Role = 'Customer' AND Email NOT LIKE '%@gmail.com' AND Email NOT LIKE '%@fpt.edu.vn' THEN 1 ELSE 0 END) as InvalidDomainCount,
    SUM(CASE WHEN Role = 'Customer' AND Email LIKE '%@gmail.com' THEN 1 ELSE 0 END) as GmailCount,
    SUM(CASE WHEN Role = 'Customer' AND Email LIKE '%@fpt.edu.vn' THEN 1 ELSE 0 END) as FptCount
FROM Users
"@
    $r = $verifyCmd.ExecuteReader()
    if ($r.Read()) {
        Write-Host "`n--- FINAL PRODUCTION STATS ---"
        Write-Host "Total Users on platform: $($r['TotalUsers'])"
        Write-Host "Total Active Users: $($r['ActiveUsers'])"
        Write-Host "Total Customers: $($r['TotalCustomers'])"
        Write-Host "Active Customers: $($r['ActiveCustomers'])"
        Write-Host "Remaining 'Lý': $($r['LyCount'])"
        Write-Host "Invalid Email Domains: $($r['InvalidDomainCount'])"
        Write-Host "Gmail Customers: $($r['GmailCount'])"
        Write-Host "FPT Customers: $($r['FptCount'])"
    }
    $r.Close()
} catch {
    if ($tran) { $tran.Rollback() }
    Write-Host "ERROR: $_"
} finally {
    $cn.Close()
}
