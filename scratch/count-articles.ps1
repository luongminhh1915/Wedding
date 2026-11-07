$connStr = "Server=localhost;Database=WeddingMatch_Dev;Integrated Security=true;TrustServerCertificate=true"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT COUNT(*) FROM Articles"
    $count = $cmd.ExecuteScalar()
    Write-Output "Articles count in SQL Server: $count"
} catch {
    Write-Output "Error: $_"
} finally {
    $conn.Close()
}
