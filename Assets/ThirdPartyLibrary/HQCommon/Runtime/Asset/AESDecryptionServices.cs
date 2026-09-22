using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using YooAsset;

public class AESDecryptionServices : IDecryptionServices
{
    public DecryptResult LoadAssetBundle(DecryptFileInfo fileInfo)
    {
        MemoryStream stream = CreateDecryptedStream(fileInfo.FileLoadPath);
        return new DecryptResult
        {
            ManagedStream = stream,
            Result = AssetBundle.LoadFromStream(stream, fileInfo.FileLoadCRC, GetManagedReadBufferSize())
        };
    }

    public DecryptResult LoadAssetBundleAsync(DecryptFileInfo fileInfo)
    {
        MemoryStream stream = CreateDecryptedStream(fileInfo.FileLoadPath);
        return new DecryptResult
        {
            ManagedStream = stream,
            CreateRequest = AssetBundle.LoadFromStreamAsync(stream, fileInfo.FileLoadCRC, GetManagedReadBufferSize())
        };
    }

    public DecryptResult LoadAssetBundleFallback(DecryptFileInfo fileInfo)
    {
        byte[] decryptedData = ReadFileData(fileInfo);
        return new DecryptResult
        {
            Result = AssetBundle.LoadFromMemory(decryptedData, fileInfo.FileLoadCRC)
        };
    }

    public byte[] ReadFileData(DecryptFileInfo fileInfo)
    {
        byte[] encryptedData = File.ReadAllBytes(fileInfo.FileLoadPath);
        return Decrypt(encryptedData);
    }

    public string ReadFileText(DecryptFileInfo fileInfo)
    {
        return Encoding.UTF8.GetString(ReadFileData(fileInfo));
    }

    private static byte[] Decrypt(byte[] encryptedData)
    {
        using (var aes = new RijndaelManaged
        {
            Key = Encoding.UTF8.GetBytes(AESEncryption.Key),
            Mode = CipherMode.ECB,
            Padding = PaddingMode.PKCS7
        })
        using (ICryptoTransform decryptor = aes.CreateDecryptor())
        {
            return decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
        }
    }

    private static MemoryStream CreateDecryptedStream(string filePath)
    {
        byte[] encryptedData = File.ReadAllBytes(filePath);
        return new MemoryStream(Decrypt(encryptedData), false);
    }

    private static uint GetManagedReadBufferSize()
    {
        return 1024;
    }
}
