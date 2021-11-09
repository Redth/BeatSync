﻿using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SongFeedReaders.Logging;

namespace BeatSyncLib.Utilities
{
    public static class Util
    {
        public static readonly string Base64Prefix = "base64,";

        private static char[]? _invalidPathChars;

        public static char[] InvalidPathChars
        {
            get { 
                if(_invalidPathChars == null)
                {
                    _invalidPathChars = _baseInvalidPathChars.Concat(Path.GetInvalidPathChars()).Distinct().ToArray();
                }
                return _invalidPathChars; 
            }
        }


        private static readonly char[] _baseInvalidPathChars = new char[]
            {
                '<', '>', ':', '/', '\\', '|', '?', '*', '"',
                '\u0000', '\u0001', '\u0002', '\u0003', '\u0004', '\u0005', '\u0006', '\u0007',
                '\u0008', '\u0009', '\u000a', '\u000b', '\u000c', '\u000d', '\u000e', '\u000d',
                '\u000f', '\u0010', '\u0011', '\u0012', '\u0013', '\u0014', '\u0015', '\u0016',
                '\u0017', '\u0018', '\u0019', '\u001a', '\u001b', '\u001c', '\u001d', '\u001f',
            };


        #region IPA Utilities
        /// <summary>
        /// Converts a hex string to a byte array.
        /// </summary>
        /// <param name="hex">the hex stream</param>
        /// <param name="throwOnBadFormat">Throw FormatException if the hex string is invalid.</param>
        /// <returns>the corresponding byte array</returns>
        public static byte[] StringToByteArray(string hex, bool throwOnBadFormat = false)
        {
            if (hex.Length % 2 == 1)
                return throwOnBadFormat ? throw new FormatException("Hex string cannot have an odd number of characters.") : Array.Empty<byte>();
            int numberChars = hex.Length;
            byte[] bytes = new byte[numberChars / 2];
            for (int i = 0; i < numberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        /// <summary>
        /// Converts a byte array to a hex string.
        /// </summary>
        /// <param name="ba">the byte array</param>
        /// <returns>the hex form of the array</returns>
        public static string? ByteArrayToString(byte[] ba)
        {
            if (ba == null)
                return null;
            else if (ba.Length == 0)
                return string.Empty;
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }
        #endregion

        #region Number Conversion

        /// <summary>
        /// Outputs a TimeSpan in hours, minutes, and seconds.
        /// </summary>
        /// <param name="timeSpan"></param>
        /// <returns></returns>
        public static string TimeSpanToString(TimeSpan timeSpan)
        {
            StringBuilder sb = new StringBuilder();
            if (timeSpan.Days > 0)
                if (timeSpan.Days == 1)
                    sb.Append("1 day ");
                else
                    sb.Append($"{(int)Math.Floor(timeSpan.TotalDays)} days ");
            if (timeSpan.Hours > 0)
                if (timeSpan.Hours == 1)
                    sb.Append("1 hour ");
                else
                    sb.Append($"{timeSpan.Hours} hours ");
            if (timeSpan.Minutes > 0)
                if (timeSpan.Minutes == 1)
                    sb.Append("1 min ");
                else
                    sb.Append($"{timeSpan.Minutes} mins ");
            if (timeSpan.Seconds > 0)
                if (timeSpan.Seconds == 1)
                    sb.Append("1 sec ");
                else
                    sb.Append($"{timeSpan.Seconds} secs ");
            return sb.ToString().Trim();
        }

        public static double ConvertByteValue(long byteVal, ByteUnit byteUnit, int decimalPrecision = 2, ByteUnit startingUnit = ByteUnit.Byte)
        {
            if (byteUnit == startingUnit || byteVal == 0)
                return byteVal;

            int byteUnitInt = (int)byteUnit;
            int startingUnitInt = (int)startingUnit;
            double newVal = byteVal;
            while (startingUnitInt < byteUnitInt)
            {
                newVal /= 1024;
                startingUnitInt++;
            }
            return Math.Round(newVal, decimalPrecision);
        }

        public static double ConvertByteValue(double byteVal, ByteUnit byteUnit, int decimalPrecision = 2, ByteUnit startingUnit = ByteUnit.Byte)
        {
            if (byteUnit == startingUnit || byteVal == 0)
                return Math.Round(byteVal, decimalPrecision);

            int byteUnitInt = (int)byteUnit;
            int startingUnitInt = (int)startingUnit;
            double newVal = byteVal;
            while (startingUnitInt < byteUnitInt)
            {
                newVal /= 1024;
                startingUnitInt++;
            }
            return Math.Round(newVal, decimalPrecision);
        }

        public enum ByteUnit
        {
            Byte = 0,
            Kilobyte = 1,
            Megabyte = 2
        }
        #endregion

        #region Hashing
        /// <summary>
        /// Generates a hash for the song and assigns it to the SongHash field. Returns null if info.dat doesn't exist.
        /// Uses Kylemc1413's implementation from SongCore.
        /// TODO: Handle/document exceptions (such as if the files no longer exist when this is called).
        /// https://github.com/Kylemc1413/SongCore
        /// </summary>
        /// <returns>Hash of the song files. Null if the info.dat file doesn't exist</returns>
        /// <exception cref="DirectoryNotFoundException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="JsonException"
        public static string? GenerateHash(string songDirectory, string existingHash = "")
        {
            if (string.IsNullOrEmpty(songDirectory))
                throw new ArgumentNullException(nameof(songDirectory));
            DirectoryInfo directory = new DirectoryInfo(songDirectory);
            if (!directory.Exists)
                throw new DirectoryNotFoundException($"Directory doesn't exist: '{songDirectory}'");
            byte[] combinedBytes = Array.Empty<byte>();
            FileInfo[] files = directory.GetFiles();
            // Could theoretically get the wrong hash if there are multiple 'info.dat' files with different cases on linux.
            string? infoFileName = files.FirstOrDefault(f => f.Name.Equals("info.dat", StringComparison.OrdinalIgnoreCase))?.FullName;
            if(infoFileName == null)
            {
                Logger.log?.Debug($"'{songDirectory}' does not have an 'info.dat' file.");
                return null;
            }
            string infoFile = Path.Combine(songDirectory, infoFileName);
            if (!File.Exists(infoFile))
                return null;
            combinedBytes = combinedBytes.Concat(File.ReadAllBytes(infoFile)).ToArray();
            JToken? token = JToken.Parse(File.ReadAllText(infoFile));
            JToken? beatMapSets = token["_difficultyBeatmapSets"];
            int numChars = beatMapSets?.Children().Count() ?? 0;
            for (int i = 0; i < numChars; i++)
            {
                JToken? diffs = beatMapSets.ElementAt(i);
                int numDiffs = diffs["_difficultyBeatmaps"]?.Children().Count() ?? 0; 
                for (int i2 = 0; i2 < numDiffs; i2++)
                {
                    JToken? diff = diffs["_difficultyBeatmaps"].ElementAt(i2);
                    string? beatmapFileName = diff["_beatmapFilename"]?.Value<string>();
                    string? beatmapFile = files.FirstOrDefault(f => f.Name.Equals(beatmapFileName, StringComparison.OrdinalIgnoreCase))?.FullName;
                    if (beatmapFile != null)
                    {
                        string beatmapPath = Path.Combine(songDirectory, beatmapFile);
                        if (File.Exists(beatmapPath))
                            combinedBytes = combinedBytes.Concat(File.ReadAllBytes(beatmapPath)).ToArray();
                        else
                            Logger.log?.Debug($"Missing difficulty file {beatmapPath.Split('\\', '/').LastOrDefault()}");
                    }
                    else
                        Logger.log?.Warning($"_beatmapFilename property is null in {infoFile}");
                }
            }

            string hash = CreateSha1FromBytes(combinedBytes.ToArray());
            if (!string.IsNullOrEmpty(existingHash) && existingHash != hash)
                Logger.log?.Warning($"Hash doesn't match the existing hash for {songDirectory}");
            return hash;
        }

        /// <summary>
        /// Returns the Sha1 hash of the provided byte array.
        /// Uses Kylemc1413's implementation from SongCore.
        /// https://github.com/Kylemc1413/SongCore
        /// </summary>
        /// <param name="input">Byte array to hash.</param>
        /// <returns>Sha1 hash of the byte array.</returns>
        public static string CreateSha1FromBytes(byte[] input)
        {
            using SHA1 sha1 = SHA1.Create();
            byte[] inputBytes = input;
            byte[] hashBytes = sha1.ComputeHash(inputBytes);

            return BitConverter.ToString(hashBytes).Replace("-", string.Empty);
        }

        /// <summary>
        /// Generates a quick hash of a directory's contents. Does NOT match SongCore.
        /// Uses most of Kylemc1413's implementation from SongCore.
        /// </summary>
        /// <param name="path"></param>
        /// <exception cref="ArgumentNullException">Thrown when path is null or empty.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when path's directory doesn't exist.</exception>
        /// <returns></returns>
        public static long GenerateDirectoryHash(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path), "Path cannot be null or empty for GenerateDirectoryHash");
            DirectoryInfo directoryInfo = new DirectoryInfo(path);
            if (!directoryInfo.Exists)
                throw new DirectoryNotFoundException($"GenerateDirectoryHash couldn't find {path}");
            long dirHash = 0L;
            foreach (FileInfo file in directoryInfo.GetFiles())
            {
                dirHash ^= file.CreationTimeUtc.ToFileTimeUtc();
                dirHash ^= file.LastWriteTimeUtc.ToFileTimeUtc();
                dirHash ^= file.Name.GetHashCode();
                //dirHash ^= SumCharacters(file.Name); // Replacement for if GetHashCode stops being predictable.
                dirHash ^= file.Length;
            }
            return dirHash;
        }
        #endregion

        public static string GetSongDirectoryName(string? songKey, string songName, string levelAuthorName)
        {
            // BeatSaverDownloader's method of naming the directory.
            string basePath;
            string nameAuthor;
            if (string.IsNullOrEmpty(levelAuthorName))
                nameAuthor = songName;
            else
                nameAuthor = $"{songName} - {levelAuthorName}";
            songKey = songKey?.Trim();
            if (songKey != null && songKey.Length > 0)
                basePath = songKey + " (" + nameAuthor + ")";
            else
                basePath = nameAuthor;
            basePath = string.Concat(basePath.Trim().Split(Util.InvalidPathChars));
            return basePath;
        }

        private static int SumCharacters(string str)
        {
            unchecked
            {
                int charSum = 0;
                for (int i = 0; i < str.Count(); i++)
                {
                    charSum += str[i];
                }
                return charSum;
            }
        }

        internal static Regex oldKeyRX = new Regex(@"^\d+-(\d+)$", RegexOptions.Compiled);
        internal static Regex newKeyRX = new Regex(@"^[0-9a-f]+$", RegexOptions.Compiled);

        /// <summary>
        /// Converts an old-style key to the current style. Key is returned in lowercase form.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        internal static string? ParseKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            if (newKeyRX.IsMatch(key))
            {
                return key.ToLower();
            }
            Match isOld = oldKeyRX.Match(key);
            if (isOld.Success)
            {
                string oldKey = isOld.Groups[1].Value;
                int oldKeyInt = int.Parse(oldKey);
                return oldKeyInt.ToString("x").ToLower();
            }
            else
                return null;
        }

        /// <summary>
        /// Converts a Base64 string to a byte array.
        /// </summary>
        /// <param name="base64Str"></param>
        /// <returns></returns>
        /// <exception cref="FormatException">Thrown when the provided string isn't a valid Base64 string.</exception>
        public static byte[] Base64ToByteArray(ref string base64Str)
        {
            if (string.IsNullOrEmpty(base64Str))
            {
                return Array.Empty<byte>();
            }
            int tagIndex = base64Str.IndexOf(Base64Prefix);
            if (tagIndex >= 0)
            {
                int firstNonWhitespace = 0;
                for (int i = 0; i <= tagIndex; i++)
                {
                    firstNonWhitespace = i;
                    if (!char.IsWhiteSpace(base64Str[i]))
                        break;
                }
                if (firstNonWhitespace == tagIndex)
                {
                    int startIndex = tagIndex + Base64Prefix.Length;
                    for (int i = startIndex; i < base64Str.Length; i++)
                    {
                        startIndex = i;
                        if (!char.IsWhiteSpace(base64Str[i]))
                            break;
                    }
                    return Convert.FromBase64String(base64Str.Substring(startIndex));
                }
            }

            return Convert.FromBase64String(base64Str);
        }

        public static string ByteArrayToBase64(byte[] byteArray)
        {
            if (byteArray == null || byteArray.Length == 0)
                return string.Empty;
            return Base64Prefix + Convert.ToBase64String(byteArray);
        }

        #region Image converting

        public static string ImageToBase64(string imagePath)
        {
            try
            {
                byte[] resource = GetResource(Assembly.GetCallingAssembly(), imagePath);
                if (resource.Length == 0)
                {
                    Logger.log?.Warning($"Unable to load image from path: {imagePath}");
                    return string.Empty;
                }
                return Convert.ToBase64String(resource);
            }
            catch (Exception ex)
            {
                Logger.log?.Warning($"Unable to load image from path: {imagePath}");
                Logger.log?.Debug(ex);
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets a resource and returns it as a byte array.
        /// From https://github.com/brian91292/BeatSaber-CustomUI/blob/master/Utilities/Utilities.cs
        /// </summary>
        /// <param name="asm"></param>
        /// <param name="ResourceName"></param>
        /// <returns></returns>
        public static byte[] GetResource(Assembly asm, string ResourceName)
        {
            try
            {
                using Stream stream = asm.GetManifestResourceStream(ResourceName);
                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, (int)stream.Length);
                return data;
            }
            catch (NullReferenceException)
            {
                Logger.log?.Debug($"Resource {ResourceName} was not found.");
            }
            return Array.Empty<byte>();
        }
        #endregion
    }
}
