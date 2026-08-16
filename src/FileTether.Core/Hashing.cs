using System.Security.Cryptography;
using System.Text;

namespace FileTether.Core;

public static class Hashing
{
    public static string Sha256Hex(byte[] content)
    {
        return Convert.ToHexStringLower(SHA256.HashData(content));
    }

    public static string Sha256Hex(string content)
    {
        return Sha256Hex(Encoding.UTF8.GetBytes(content));
    }
}
