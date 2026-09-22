using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;
using Cysharp.Threading.Tasks;

namespace Joy
{
    public static class FileUtility
    {
        public static bool CopyDirectory(string srcPath, string destPath, bool lowerPath = false, string dirPattern = "*", string filePattern = "*.*")
        {
            CreateDirectoryIfNotExist(destPath);
            try
            {
                foreach (string dirPath in Directory.GetDirectories(srcPath, dirPattern, SearchOption.AllDirectories))
                {
                    var realDirPath = dirPath.Replace(srcPath, destPath);
                    realDirPath = lowerPath ? realDirPath.ToLower() : realDirPath;
                    CreateDirectoryIfNotExist(realDirPath);
                }

                foreach (string newPath in Directory.GetFiles(srcPath, filePattern, SearchOption.AllDirectories))
                {
                    var realNewPath = newPath.Replace(srcPath, destPath);
                    realNewPath = lowerPath ? realNewPath.ToLower() : realNewPath;
                    File.Copy(newPath, realNewPath, true);
                }
            }
            catch (Exception ex)
            {
                Joy.Debug.LogError(ex);
                return false;
            }

            return true;
        }

        public static void DeleteFilesFromDir(string dir, Func<string, bool> checkFunc)
        {
            foreach (string newPath in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
            {
                if (checkFunc(newPath))
                {
                    File.Delete(newPath);
                    Joy.Debug.LogFormat("delete file:{0}", newPath);
                }
            }
        }

        public static void DeleteTopFileOrDirectoryFromDir(string dir, Func<string, bool, bool> checkFunc)
        {
            foreach (string topDir in Directory.GetDirectories(dir, "*", SearchOption.TopDirectoryOnly))
            {
                if (checkFunc(topDir, false))
                {
                    DeleteExistDirectory(topDir);
                    Joy.Debug.LogFormat("delete dir:{0}", topDir);
                }
            }

            foreach (string newPath in Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
            {
                if (checkFunc(newPath, true))
                {
                    File.Delete(newPath);
                    Joy.Debug.LogFormat("delete file:{0}", newPath);
                }
            }
        }

        public static void DeleteExistFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public static void DeleteExistDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        public static bool IsDirectory(string path)
        {
            FileAttributes attr = File.GetAttributes(path);

            return (attr & FileAttributes.Directory) == FileAttributes.Directory;
        }

        public static void CreateDirectoryIfNotExist(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public static void CreateFileDirectoryIfNoExist(string filePath)
        {
            CreateDirectoryIfNotExist(Path.GetDirectoryName(filePath));
        }

        public static long GetFileSize(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            return fileInfo.Length;
        }

        public static string GetFileMD5(string filePath, bool isLowerCase = true)
        {
            var hash = MD5.Create();
            byte[] bytes = File.ReadAllBytes(filePath);
            bytes = hash.ComputeHash(bytes);
            var hashStr = BitConverter.ToString(bytes).Replace("-", "");
            if (isLowerCase)
            {
                hashStr = hashStr.ToLower();
            }
            return hashStr;
        }

        // public static void Zip(string srcPath, string destPath)
        // {
        //     if (File.Exists(destPath))
        //     {
        //         File.Delete(destPath);
        //     }
        //     if (Directory.Exists(srcPath))
        //     {
        //         ZipConstants.DefaultCodePage = Encoding.UTF8.CodePage;
        //         FastZip fastZip = new FastZip();
        //         fastZip.CreateZip(destPath, srcPath, true, "");
        //     }
        // }

        // public static void Unzip(string zipFile, string toDir)
        // {
        //     if (File.Exists(zipFile))
        //     {
        //         ZipConstants.DefaultCodePage = Encoding.UTF8.CodePage;
        //         FastZip fastZip = new FastZip();
        //         fastZip.ExtractZip(zipFile, toDir, "");
        //     }
        // }

        public static async UniTask<byte[]> LoadFile(string filePath)
        {
            byte[] data = null;
            try
            {
                if (File.Exists(filePath))
                {
                    data = await File.ReadAllBytesAsync(filePath);
                }
            }
            catch (Exception ex)
            {
                Joy.Debug.LogError($"load file error: fileName = {filePath}, error = {ex.Message}.");
            }
            return data;
        }

        public static async UniTask<string> LoadFileByText(string filePath)
        {
            string data = null;
            try
            {
                if (File.Exists(filePath))
                {
                    data = await File.ReadAllTextAsync(filePath);
                }
            }
            catch (Exception ex)
            {
                Joy.Debug.LogError($"load file error: fileName = {filePath}, error = {ex.Message}.");
            }
            return data;
        }

        public static bool IsPresistentFileExists(string fileName)
        {
            string fullPath = PathUtility.GetPresistentDataFullPath(fileName);
            return File.Exists(fullPath);
        }

        public static async UniTask<byte[]> LoadPresistentFile(string fileName)
        {
            byte[] data = null;
            string fullPath = PathUtility.GetPresistentDataFullPath(fileName);
            try
            {
                if (File.Exists(fullPath))
                {
                    data = await File.ReadAllBytesAsync(fullPath);
                }
            }
            catch(Exception ex)
            {
                Joy.Debug.LogError($"load file error: fileName = {fullPath}, error = {ex.Message}.");
            }
            return data;
        }

        public static async UniTask<string> LoadPresistentFileByText(string fileName)
        {
            string data = null;
            string fullPath = PathUtility.GetPresistentDataFullPath(fileName);
            try
            {
                if (File.Exists(fullPath))
                {
                    data = await File.ReadAllTextAsync(fullPath);
                }
            }
            catch (Exception ex)
            {
                Joy.Debug.LogError($"load file error: fileName = {fullPath}, error = {ex.Message}.");
            }
            return data;
        }

        public static async UniTask SavePresistentFileAsync(string text, string fileName)
        {
            string fullPath = PathUtility.GetPresistentDataFullPath(fileName);
            await File.WriteAllTextAsync(fullPath, text);
        }

        public static async UniTask SavePresistentFileAsync(byte[] bytes, string fileName)
        {
            string fullPath = PathUtility.GetPresistentDataFullPath(fileName);
            await File.WriteAllBytesAsync(fullPath, bytes);
        }

        public static async UniTask<byte[]> LoadStreamingFile(string fileName)
        {
            byte[] data = null;
            UnityWebRequest request = null;
            string fullPath = PathUtility.GetStreamingDataFullPath3W(fileName);
            try
            {
                request = UnityWebRequest.Get(fileName);
                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    data = request.downloadHandler.data;
                    request.Dispose();
                }
                else
                {
                    Joy.Debug.LogError($"load file error: fileName = {fullPath}, error = {request.error}.");
                }
            }
            catch (Exception ex)
            {
                Joy.Debug.LogError($"load file error: fileName = {fullPath}, error = {ex.Message}.");
            }
            finally
            {
                if (request != null)
                {
                    request.Dispose();
                }
            }
            return data;
        }

        public static async UniTask<string> LoadStreamingFileByText(string fileName)
        {
            string data = null;
            UnityWebRequest request = null;
            string fullPath = PathUtility.GetStreamingDataFullPath3W(fileName);
            try
            {
                request = UnityWebRequest.Get(fileName);
                await request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    data = request.downloadHandler.text;
                    request.Dispose();
                }
                else
                {
                    Joy.Debug.LogError($"load file error: fileName = {fullPath}, error = {request.error}.");
                }
            }
            catch (Exception ex)
            {
                Joy.Debug.LogError($"load file error: fileName = {fullPath}, error = {ex.Message}.");
            }
            finally
            {
                if (request != null)
                {
                    request.Dispose();
                }
            }
            return data;
        }

        public static async UniTask CopyStreamingFileToPresistentDir(string srcFileName, string destFileName)
        {
            byte[] bytes = await LoadStreamingFile(srcFileName);
            string destFullPath = PathUtility.GetPresistentDataFullPath(destFileName);
            await File.WriteAllBytesAsync(destFullPath, bytes);
        }
    }
}
