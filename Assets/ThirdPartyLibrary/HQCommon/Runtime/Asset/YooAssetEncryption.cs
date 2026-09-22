using System;
using System.IO;
using System.Text;
using YooAsset;
using System.Security.Cryptography;

public class FileOffsetEncryption : IEncryptionServices
{
    public EncryptResult Encrypt(EncryptFileInfo fileInfo)
    {
        int offset = 32;
        byte[] fileData = File.ReadAllBytes(fileInfo.FileLoadPath);
        var encryptedData = new byte[fileData.Length + offset];
        Buffer.BlockCopy(fileData, 0, encryptedData, offset, fileData.Length);

        return new EncryptResult
        {
            Encrypted = true,
            EncryptedData = encryptedData
        };
    }
}
public class AESEncryption : IEncryptionServices
{
    public const string Key = "asasBhyhVyqAzxaw";
    public EncryptResult Encrypt(EncryptFileInfo fileInfo)
    {
        byte[] sourceData = File.ReadAllBytes(fileInfo.FileLoadPath);
        using (var aes = new RijndaelManaged
        {
            Key = Encoding.UTF8.GetBytes(Key),
            Mode = CipherMode.ECB,
            Padding = PaddingMode.PKCS7
        })
        using (ICryptoTransform encryptor = aes.CreateEncryptor())
        {
            byte[] encryptedData = encryptor.TransformFinalBlock(sourceData, 0, sourceData.Length);
            return new EncryptResult
            {
                Encrypted = true,
                EncryptedData = encryptedData
            };
        }
    }
}
