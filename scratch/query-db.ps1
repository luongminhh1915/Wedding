$connStr = "Server=localhost;Database=WeddingMatch_Dev;Integrated Security=true;TrustServerCertificate=true"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT Email, Role, FullName FROM Users"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Output "$($reader['Email']) | $($reader['Role']) | $($reader['FullName'])"
    }
} catch {
    Write-Output "Error: $_"
} finally {
    $conn.Close()
}

