using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;

namespace Joy
{
    public static class MathUtility
    {
        public static string ByteToMemorySize(long byteLength)
        {
            int count = 0;
            while (byteLength > 1024 && count < 3)
            {
                count++;
                byteLength = byteLength >> 10;
            }

            switch (count)
            {
                case 0:
                    return $"{byteLength}b";
                case 1:
                    return $"{byteLength}KB";
                case 2:
                    return $"{byteLength}MB";
                case 3:
                    return $"{byteLength}GB";
                default:
                    return $"{byteLength}GB";
            }
        }
    }
}