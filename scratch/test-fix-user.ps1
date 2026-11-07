$connProd = "Server=sql1004.site4now.net;Database=db_ad0009_weddingmatch;User Id=db_ad0009_weddingmatch_admin;Password=T@i2101200622122005;Encrypt=true;TrustServerCertificate=true;Connect Timeout=15"
$cn = New-Object System.Data.SqlClient.SqlConnection($connProd)
$cn.Open()
$cmd = $cn.CreateCommand()
$cmd.CommandText = "UPDATE Users SET PasswordHash = @hash WHERE Email = @email"
$targetHash = '$2a$11$jaWuWOwGuPi4M97wBtPUT.SFlnN/jCKtNthzVK92YFQx8QuGG5k12'
[void]$cmd.Parameters.AddWithValue("@hash", $targetHash)
[void]$cmd.Parameters.AddWithValue("@email", "vy.nguyen93@gmail.com")
$rows = $cmd.ExecuteNonQuery()
$cn.Close()
Write-Host "Updated rows: $rows"

