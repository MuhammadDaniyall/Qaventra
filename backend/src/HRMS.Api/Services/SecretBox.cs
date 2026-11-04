using System.Security.Cryptography;
using System.Text;

namespace HRMS.Api.Services;

/// <summary>
/// Encrypts small secrets kept in the database (a company's SMTP password) with AES-GCM. The key comes from
/// "Secrets:Key" when set, otherwise it is derived from the JWT signing key; changing either means the secrets
/// must be entered again.
/// </summary>
public class SecretBox
{
    private const string Prefix = "v1:";
    private readonly byte[] _key;

    public SecretBox(IConfiguration config)
    {
        var material = config["Secrets:Key"] is { Length: >= 32 } own ? own : config["Jwt:Key"] ?? "";
        if (material.Length < 32)
            throw new InvalidOperationException("Secrets:Key or Jwt:Key must be at least 32 characters.");
        _key = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(material), 32, info: Encoding.UTF8.GetBytes("hrms-secret-box"));
    }

    public string Protect(string plain)
    {
        var data = Encoding.UTF8.GetBytes(plain);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var cipher = new byte[data.Length];
        using (var aes = new AesGcm(_key, 16))
            aes.Encrypt(nonce, data, cipher, tag);
        return Prefix + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    /// <summary>The plain text, or null when it cannot be read (wrong key, damaged).</summary>
    public string? Unprotect(string? box)
    {
        if (string.IsNullOrEmpty(box) || !box.StartsWith(Prefix)) return null;
        try
        {
            var all = Convert.FromBase64String(box[Prefix.Length..]);
            if (all.Length < 28) return null;
            var plain = new byte[all.Length - 28];
            using var aes = new AesGcm(_key, 16);
            aes.Decrypt(all.AsSpan(0, 12), all.AsSpan(28), all.AsSpan(12, 16), plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return null;
        }
    }
}
