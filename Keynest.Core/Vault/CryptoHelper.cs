using System.Security.Cryptography;

namespace Keynest.Core.Vault;

public static class CryptoHelper
{
    public static byte[] GenerateFernetKey() => RandomNumberGenerator.GetBytes(32);

    public static byte[] DerivePbkdf2(string password, byte[] salt, int iterations = 480_000)
        => Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);

    /// <summary>
    /// Encrypts plaintext using Fernet (Python cryptography-compatible).
    /// Token layout: base64url(version[1] | timestamp[8 BE] | iv[16] | AES-128-CBC(pt)[n] | HMAC-SHA256[32])
    /// key[0:16] = signing key, key[16:32] = AES encryption key.
    /// </summary>
    public static string FernetEncrypt(byte[] key, byte[] plaintext)
    {
        var signingKey = key[..16];
        var encKey = key[16..];

        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var tsBytes = new byte[8];
        for (int i = 7; i >= 0; i--) { tsBytes[i] = (byte)(ts & 0xFF); ts >>= 8; }

        var iv = RandomNumberGenerator.GetBytes(16);

        byte[] ciphertext;
        using (var aes = Aes.Create())
        {
            aes.Key = encKey; aes.IV = iv;
            aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
            using var enc = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, enc, CryptoStreamMode.Write))
            { cs.Write(plaintext); cs.FlushFinalBlock(); }
            ciphertext = ms.ToArray();
        }

        // payload = version | timestamp | iv | ciphertext
        var payload = new byte[1 + 8 + 16 + ciphertext.Length];
        payload[0] = 0x80;
        Buffer.BlockCopy(tsBytes, 0, payload, 1, 8);
        Buffer.BlockCopy(iv, 0, payload, 9, 16);
        Buffer.BlockCopy(ciphertext, 0, payload, 25, ciphertext.Length);

        byte[] hmac;
        using (var h = new HMACSHA256(signingKey)) hmac = h.ComputeHash(payload);

        var token = new byte[payload.Length + 32];
        Buffer.BlockCopy(payload, 0, token, 0, payload.Length);
        Buffer.BlockCopy(hmac, 0, token, payload.Length, 32);

        return Base64UrlEncode(token);
    }

    /// <summary>
    /// Decrypts a Fernet token. Verifies HMAC before decrypting.
    /// Throws CryptographicException if verification fails.
    /// </summary>
    public static byte[] FernetDecrypt(byte[] key, string token)
    {
        var signingKey = key[..16];
        var encKey = key[16..];

        var raw = Base64UrlDecode(token);
        if (raw.Length < 57) throw new CryptographicException("Invalid Fernet token: too short.");
        if (raw[0] != 0x80) throw new CryptographicException("Invalid Fernet token: unknown version.");

        var payloadLen = raw.Length - 32;
        var payload = raw[..payloadLen];
        var storedHmac = raw[payloadLen..];

        byte[] computedHmac;
        using (var h = new HMACSHA256(signingKey)) computedHmac = h.ComputeHash(payload);
        if (!CryptographicOperations.FixedTimeEquals(storedHmac, computedHmac))
            throw new CryptographicException("Fernet token HMAC verification failed.");

        var iv = payload[9..25];
        var ct = payload[25..];

        using var aes = Aes.Create();
        aes.Key = encKey; aes.IV = iv;
        aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
        using var dec = aes.CreateDecryptor();
        using var input = new MemoryStream(ct);
        using var cs = new CryptoStream(input, dec, CryptoStreamMode.Read);
        using var output = new MemoryStream();
        cs.CopyTo(output);
        return output.ToArray();
    }

    public static bool FixedTimeEquals(byte[] a, byte[] b)
        => CryptographicOperations.FixedTimeEquals(a, b);

    private static string Base64UrlEncode(byte[] data)
        => Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        s += new string('=', (4 - s.Length % 4) % 4);
        return Convert.FromBase64String(s);
    }
}
