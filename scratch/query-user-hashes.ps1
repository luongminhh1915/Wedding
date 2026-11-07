$connStr = "Server=localhost;Database=WeddingMatch_Dev;Integrated Security=true;TrustServerCertificate=true"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
try {
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT Email, Role, FullName, PasswordHash FROM Users WHERE Email IN ('anhduc21165@gmail.com', 'nguoidung1@gmail.com', 'moderator@wedding.com', 'finance@wedding.com')"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Output "$($reader['Email']) | $($reader['Role']) | $($reader['PasswordHash'])"
    }
} catch {
    Write-Output "Error: $_"
} finally {
    $conn.Close()
}

