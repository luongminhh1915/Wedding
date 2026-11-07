using System;
using Wedding.Infrastructure.Services;

class Check {
    static void Main() {
        var h = new PasswordHasher();
        string hash = " .O359oFVCTosJq\;
 string[] candidates = new[] { \123456\, \1234567890\, \Password123!\, \admin123\, \12345678\, \wedding123\, \password\, \Admin@123\ };
 foreach (var c in candidates) {
 Console.WriteLine(c + \: \ + h.Verify(c, hash));
 }
 }
}
