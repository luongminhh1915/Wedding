Add-Type -Path "C:\Users\Admin\.nuget\packages\bcrypt.net-next\4.2.0\lib\net48\BCrypt-Net-Next.dll"
$h = "$2a$11$soA1kT9hP0O2BMzRpO1zme4h6lHgaoSgYIUGJie.O359oFVCTosJq"
$testPasswords = @("123456", "admin", "admin123", "Admin123", "Admin@123", "Admin@123!", "Admin@123456", "Password123!", "12345678", "wedding123", "password", "Customer123!", "Customer@123", "nguoidung1", "Nguoidung1@")
foreach ($p in $testPasswords) {
    $match = [BCrypt.Net.BCrypt]::Verify($p, $h)
    if ($match) {
        Write-Output "FOUND! Password is: $p"
        break
    }
}
Write-Output "Check finished."

