using System.Security.Cryptography;
using System.Text;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using Javax.Crypto.Spec;

namespace Plants.Services;

public sealed class SecureDataService
{
    private const string KeyAlias = "plants.user.data.key";
    private const string AndroidKeyStore = "AndroidKeyStore";

    public string HashStableId(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public string Encrypt(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var cipher = Cipher.GetInstance("AES/GCM/NoPadding");
        cipher.Init(Javax.Crypto.CipherMode.EncryptMode, GetOrCreateSecretKey());
        var cipherBytes = cipher.DoFinal(Encoding.UTF8.GetBytes(value));
        var iv = cipher.GetIV();
        var payload = new byte[iv.Length + cipherBytes.Length];
        Buffer.BlockCopy(iv, 0, payload, 0, iv.Length);
        Buffer.BlockCopy(cipherBytes, 0, payload, iv.Length, cipherBytes.Length);
        return Convert.ToBase64String(payload);
    }

    public string Decrypt(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        try
        {
            var payload = Convert.FromBase64String(value);
            if (payload.Length <= 12)
            {
                return value;
            }

            var iv = payload[..12];
            var cipherBytes = payload[12..];
            var cipher = Cipher.GetInstance("AES/GCM/NoPadding");
            cipher.Init(Javax.Crypto.CipherMode.DecryptMode, GetOrCreateSecretKey(), new GCMParameterSpec(128, iv));
            var plainBytes = cipher.DoFinal(cipherBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return value;
        }
    }

    private static IKey GetOrCreateSecretKey()
    {
        var keyStore = KeyStore.GetInstance(AndroidKeyStore);
        keyStore.Load(null);

        if (!keyStore.ContainsAlias(KeyAlias))
        {
            var generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, AndroidKeyStore);
            var spec = new KeyGenParameterSpec.Builder(KeyAlias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
                .SetBlockModes(KeyProperties.BlockModeGcm)
                .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
                .SetKeySize(256)
                .Build();
            generator.Init(spec);
            generator.GenerateKey();
        }

        var entry = keyStore.GetEntry(KeyAlias, null) as KeyStore.SecretKeyEntry;
        return entry?.SecretKey ?? throw new InvalidOperationException("Не удалось получить ключ шифрования.");
    }
}
