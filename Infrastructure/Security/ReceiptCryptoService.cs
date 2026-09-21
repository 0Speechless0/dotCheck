using System.Security.Cryptography;
using System.Text;

namespace dotCheck.Infrastructure.Security;

public sealed class ReceiptCryptoService
{
    public const string SignatureAlgorithm = "RSA-PSS-SHA256";
    private readonly object _sync = new();
    private readonly RSA _rsa;

    public ReceiptCryptoService(IConfiguration configuration)
    {
        KeyId = configuration["ReceiptCrypto:KeyId"]?.Trim();
        if (string.IsNullOrWhiteSpace(KeyId))
            KeyId = "dotcheck-2026-01";

        var configured = configuration["ReceiptCrypto:PrivateKeyPath"]?.Trim();
        var path = ResolvePath(configured);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        _rsa = RSA.Create(3072);
        if (File.Exists(path))
        {
            _rsa.ImportFromPem(File.ReadAllText(path));
        }
        else
        {
            File.WriteAllText(path, _rsa.ExportPkcs8PrivateKeyPem(), new UTF8Encoding(false));
        }

        PublicKey = _rsa.ExportSubjectPublicKeyInfoPem();
    }

    public string KeyId { get; }
    public string PublicKey { get; }

    public string Sign(string payload)
    {
        var data = Encoding.UTF8.GetBytes(payload);
        lock (_sync)
        {
            var signature = _rsa.SignData(
                data,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pss);

            return Convert.ToBase64String(signature);
        }
    }

    public bool Verify(string payload, string signature, string keyId)
    {
        if (!string.Equals(keyId, KeyId, StringComparison.Ordinal))
            return false;

        if (string.IsNullOrWhiteSpace(signature))
            return false;

        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(signature);
        }
        catch (FormatException)
        {
            return false;
        }

        var data = Encoding.UTF8.GetBytes(payload);
        lock (_sync)
        {
            return _rsa.VerifyData(
                data,
                signatureBytes,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pss);
        }
    }

    private static string ResolvePath(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (configured.StartsWith("~/", StringComparison.Ordinal))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, configured[2..]);
            }

            return Path.GetFullPath(configured);
        }

        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userHome, ".dotCheck", "receipt-private.pem");
    }
}
