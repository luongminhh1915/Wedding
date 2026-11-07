using System;
using Wedding.Infrastructure.Services;

class Program
{
    static void Main()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("123456");
        Console.WriteLine($"HASH_123456: {hash}");
        Console.WriteLine($"VERIFY: {hasher.Verify("123456", hash)}");
    }
}

