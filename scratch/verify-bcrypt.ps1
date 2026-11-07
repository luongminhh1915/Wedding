Add-Type -Path "C:\Users\Admin\.nuget\packages\bcrypt.net-next\4.2.0\lib\net48\BCrypt-Net-Next.dll"
$adminHash = "$2a$11$KU06IGpqFV7/EPrMG67TKeU9mqwGjaCkimdlvSVIliDt3Hq5.VodW"
$testPasswords = @("123456", "admin", "admin123", "Admin123", "Admin@123", "Admin@123!", "Admin@123456", "Password123!", "12345678", "wedding123", "password", "SuperAdmin", "superadmin", "wedding", "Wedding123!", "wedding@123", "123456aA@")
foreach ($p in $testPasswords) {
    $match = [BCrypt.Net.BCrypt]::Verify($p, $adminHash)
    if ($match) {
        Write-Output "FOUND! Password is: $p"
        break
    }
}
Write-Output "Check finished."