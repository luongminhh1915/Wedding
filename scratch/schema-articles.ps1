$connStr = "Server=localhost;Database=WeddingMatch_Dev;Integrated Security=true;TrustServerCertificate=true"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Articles'"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Output "$($reader['COLUMN_NAME']) | $($reader['DATA_TYPE']) ($($reader['CHARACTER_MAXIMUM_LENGTH'])) | $($reader['IS_NULLABLE'])"
    }
} catch {
    Write-Output "Error: $_"
} finally {
    $conn.Close()
}

