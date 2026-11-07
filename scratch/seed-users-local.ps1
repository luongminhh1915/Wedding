$connStr = "Server=localhost;Database=WeddingMatch_Dev;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=10"
$usersJson = Get-Content -Raw "scratch/seed-users.json" | ConvertFrom-Json

$cn = New-Object System.Data.SqlClient.SqlConnection($connStr)
try {
    $cn.Open()
    Write-Host "Connected to Local SQL Server (WeddingMatch_Dev) successfully!"
    $inserted = 0
    $skipped = 0

    foreach ($u in $usersJson) {
        $checkCmd = $cn.CreateCommand()
        $checkCmd.CommandText = "SELECT COUNT(*) FROM Users WHERE Email = @email OR PhoneNumber = @phone"
        [void]$checkCmd.Parameters.AddWithValue("@email", $u.email)
        [void]$checkCmd.Parameters.AddWithValue("@phone", $u.phoneNumber)
        $count = [int]$checkCmd.ExecuteScalar()

        if ($count -eq 0) {
            $insertCmd = $cn.CreateCommand()
            $insertCmd.CommandText = @"
INSERT INTO Users (Id, FullName, Email, PhoneNumber, PasswordHash, Role, IsActive, CreatedAt, UpdatedAt, LastLoginAt, SecurityVersion)
VALUES (@id, @fullName, @email, @phone, @pwdHash, @role, @isActive, @createdAt, @updatedAt, @lastLoginAt, 1)
"@
            [void]$insertCmd.Parameters.AddWithValue("@id", [Guid]::NewGuid())
            [void]$insertCmd.Parameters.AddWithValue("@fullName", $u.fullName)
            [void]$insertCmd.Parameters.AddWithValue("@email", $u.email)
            [void]$insertCmd.Parameters.AddWithValue("@phone", $u.phoneNumber)
            [void]$insertCmd.Parameters.AddWithValue("@pwdHash", $u.passwordHash)
            [void]$insertCmd.Parameters.AddWithValue("@role", $u.role)
            [void]$insertCmd.Parameters.AddWithValue("@isActive", [bool]$u.isActive)
            [void]$insertCmd.Parameters.AddWithValue("@createdAt", [DateTime]::Parse($u.createdAt))
            [void]$insertCmd.Parameters.AddWithValue("@updatedAt", [DateTime]::Parse($u.createdAt))
            
            if ($u.lastLoginAt) {
                [void]$insertCmd.Parameters.AddWithValue("@lastLoginAt", [DateTime]::Parse($u.lastLoginAt))
            } else {
                [void]$insertCmd.Parameters.AddWithValue("@lastLoginAt", [DBNull]::Value)
            }

            [void]$insertCmd.ExecuteNonQuery()
            $inserted++
        } else {
            $skipped++
        }
    }

    Write-Host "Insertion completed: Inserted: $inserted | Skipped: $skipped"

    # Query statistics
    $statCmd = $cn.CreateCommand()
    $statCmd.CommandText = "SELECT Role, COUNT(*) as Total, SUM(CASE WHEN IsActive=1 THEN 1 ELSE 0 END) as Active FROM Users GROUP BY Role"
    $r = $statCmd.ExecuteReader()
    Write-Host "`n--- CURRENT USERS BY ROLE ---"
    while ($r.Read()) {
        Write-Host "Role: $($r['Role']) | Total: $($r['Total']) | Active: $($r['Active'])"
    }
    $r.Close()

    $totalCmd = $cn.CreateCommand()
    $totalCmd.CommandText = "SELECT COUNT(*) as Total, SUM(CASE WHEN IsActive=1 THEN 1 ELSE 0 END) as Active FROM Users"
    $rTotal = $totalCmd.ExecuteReader()
    if ($rTotal.Read()) {
        Write-Host ">> OVERALL: Total Accounts: $($rTotal['Total']) | Total Active Users: $($rTotal['Active'])"
    }
    $cn.Close()
} catch {
    Write-Host "Error: $($_.Exception.Message)"
}

