[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$connStr = "Server=localhost;Database=WeddingMatch_Dev;Integrated Security=true;TrustServerCertificate=true"
$jsonRaw = [System.IO.File]::ReadAllText("$PSScriptRoot/transformed-customers.json", [System.Text.Encoding]::UTF8)
$customers = $jsonRaw | ConvertFrom-Json

$cn = New-Object System.Data.SqlClient.SqlConnection($connStr)
try {
    $cn.Open()
    Write-Host "Connected to Local SQL Server (WeddingMatch_Dev)."

    # Get local customer IDs
    $getCmd = $cn.CreateCommand()
    $getCmd.CommandText = "SELECT Id FROM Users WHERE Role = 'Customer' ORDER BY CreatedAt, Id"
    $r = $getCmd.ExecuteReader()
    $localIds = @()
    while ($r.Read()) {
        $localIds += $r["Id"]
    }
    $r.Close()

    Write-Host "Found $($localIds.Count) customer rows in Local DB."

    $tran = $cn.BeginTransaction("ApplyCustomerUpdateLocal")

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
    for ($i = 0; $i -lt $localIds.Count; $i++) {
        $target = $customers[$i % $customers.Count]
        $paramId.Value = $localIds[$i]
        $pName.Value = $target.fullName
        $pEmail.Value = if ($i -lt $customers.Count) { $target.email } else { "user$i.$($target.email)" }
        $pPhone.Value = $target.phoneNumber
        $pHash.Value = $target.passwordHash
        $pActive.Value = [bool]$target.isActive

        $rows = $updateCmd.ExecuteNonQuery()
        if ($rows -gt 0) { $updated++ }
    }

    $tran.Commit()
    Write-Host "Transaction committed! Successfully updated $updated customer accounts on Local Dev!"
} catch {
    if ($tran) { $tran.Rollback() }
    Write-Host "ERROR: $_"
} finally {
    $cn.Close()
}
