$connDev = "Server=localhost;Database=WeddingMatch_Dev;Integrated Security=true;TrustServerCertificate=true"
$cn = New-Object System.Data.SqlClient.SqlConnection($connDev)
$cn.Open()
$cmd = $cn.CreateCommand()
$cmd.CommandText = "UPDATE Users SET PasswordHash = @newHash WHERE PasswordHash = @oldHash"
$newHash = '$2a$11$jaWuWOwGuPi4M97wBtPUT.SFlnN/jCKtNthzVK92YFQx8QuGG5k12'
$oldHash = '$2a$11$soA1kT9hP0O2BMzRpO1zme4h6lHgaoSgYIUGJie.O359oFVCTosJq'
[void]$cmd.Parameters.AddWithValue("@newHash", $newHash)
[void]$cmd.Parameters.AddWithValue("@oldHash", $oldHash)
$rows = $cmd.ExecuteNonQuery()
$cn.Close()
Write-Host "Updated $rows users on Local Dev to working hash for 123456."

