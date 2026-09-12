using System.Security.Cryptography;

namespace LicenseIssuer;

public static class RsaKeyHelper
{
    public static (string PrivatePem, string PublicPem) GenerateKeyPair(int keySize = 2048)
    {
        using var rsa = RSA.Create(keySize);
        var privatePem = rsa.ExportPkcs8PrivateKeyPem();
        var publicPem = rsa.ExportSubjectPublicKeyInfoPem();
        return (privatePem, publicPem);
    }

    public static RSA LoadPrivateKeyFromPem(string pemContent)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(pemContent);
        return rsa;
    }

    public static RSA LoadPublicKeyFromPem(string pemContent)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(pemContent);
        return rsa;
    }
}
