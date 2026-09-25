using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using dotCheck.Domain.Entities;

namespace dotCheck.Infrastructure.Security;

public sealed class UserAsymmetricKeyService(IDataProtectionProvider protectionProvider)
{
    private const string ProtectorPurpose = "dotCheck.UserAsymmetricPrivateKey.v1";
    private readonly IDataProtector _protector =
        protectionProvider.CreateProtector(ProtectorPurpose);

    public const string SignatureAlgorithm = "RSA-PSS-SHA256";

    public UserKeyPair GenerateKeyPair()
    {
        using var rsa = RSA.Create(3072);
        var publicKey = rsa.ExportRSAPublicKeyPem();
        var privateKey = rsa.ExportRSAPrivateKeyPem();

        return new UserKeyPair(
            Guid.NewGuid().ToString("N"),
            publicKey,
            _protector.Protect(privateKey));
    }

    public string SignHash(User user, string payloadHash)
    {
        ValidateKey(user);

        var hash = Convert.FromHexString(payloadHash);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(_protector.Unprotect(user.PrivateKeyProtectedPem));

        var signature = rsa.SignHash(
            hash,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pss);

        return Convert.ToBase64String(signature);
    }

    public bool VerifyHash(User user, string payloadHash, string signature) =>
        VerifyHash(user.PublicKeyPem, payloadHash, signature);

    public bool VerifyHash(
        string publicKeyPem,
        string payloadHash,
        string signature)
    {
        if (string.IsNullOrWhiteSpace(publicKeyPem)
            || string.IsNullOrWhiteSpace(payloadHash)
            || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        try
        {
            var hash = Convert.FromHexString(payloadHash);
            var signatureBytes = Convert.FromBase64String(signature);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);

            return rsa.VerifyHash(
                hash,
                signatureBytes,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pss);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    public static string ComputeSha256(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static void ValidateKey(User user)
    {
        if (string.IsNullOrWhiteSpace(user.PrivateKeyProtectedPem)
            || string.IsNullOrWhiteSpace(user.PublicKeyPem)
            || string.IsNullOrWhiteSpace(user.AsymmetricKeyId))
        {
            throw new InvalidOperationException("使用者尚未建立非對稱金鑰。請重新建立使用者或執行金鑰補建。" );
        }
    }
}

public sealed record UserKeyPair(
    string KeyId,
    string PublicKeyPem,
    string ProtectedPrivateKeyPem);
