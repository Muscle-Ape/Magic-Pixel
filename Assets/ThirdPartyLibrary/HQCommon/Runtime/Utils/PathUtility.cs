using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Joy
{
    public static class PathUtility
    {
        public static string ProjectRootDir = Application.dataPath.Replace("\\", "/").Replace("/Assets", "");

        public static string streamingDataPath
        {
            get
            {
                if (Application.platform == RuntimePlatform.IPhonePlayer)
                {
                    return Application.dataPath + "/Raw";
                }
                else if (Application.platform == RuntimePlatform.Android)
                {
                    return Application.dataPath + "!assets";
                }
                return Application.dataPath + "/StreamingAssets";
            }
        }

        public static string streamingDataPath3W
        {
            get
            {
                if (Application.platform == RuntimePlatform.IPhonePlayer)
                {
                    return "file://" + Application.dataPath + "/Raw";
                }
                else if (Application.platform == RuntimePlatform.Android)
                {
                    return "jar:file://" + Application.dataPath + "!/assets";
                }
                return "file:///" + Application.dataPath + "/StreamingAssets";
            }
        }

        public static string presistentDataPath
        {
            get
            {
                return Application.persistentDataPath;
            }
        }

        public static string presistentDataPath3W
        {
            get
            {
                if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
                    return "file:///" + Application.persistentDataPath;
                return "file://" + Application.persistentDataPath;
            }
        }

        public static string GetStreamingDataFullPath(string fileName)
        {
            return Path.Combine(streamingDataPath, fileName);
        }

        public static string GetStreamingDataFullPath3W(string fileName)
        {
            return Path.Combine(streamingDataPath3W, fileName);
        }

        public static string GetPresistentDataFullPath(string fileName)
        {
            return Path.Combine(presistentDataPath, fileName);
        }

        public static string GetPresistentDataFullPath3W(string fileName)
        {
            return Path.Combine(presistentDataPath3W, fileName);
        }
    }
}
