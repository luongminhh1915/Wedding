$connStr = "Server=localhost;Database=WeddingMatch_Dev;Integrated Security=true;TrustServerCertificate=true"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT Id, Email, FullName FROM Users WHERE Role = 'SuperAdmin'"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Output "$($reader['Id']) | $($reader['Email']) | $($reader['FullName'])"
    }
} catch {
    Write-Output "Error: $_"
} finally {
    $conn.Close()
}

