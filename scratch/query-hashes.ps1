$connStr = "Server=localhost;Database=WeddingMatch_Dev;Integrated Security=true;TrustServerCertificate=true"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT TOP 5 Email, Role, FullName, PasswordHash FROM Users"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Output "$($reader['Email']) | $($reader['Role']) | $($reader['PasswordHash'])"
    }
} catch {
    Write-Output "Error: $_"
} finally {
    $conn.Close()
}

