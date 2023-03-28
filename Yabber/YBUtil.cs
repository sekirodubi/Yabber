using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using SoulsFormats;
using System.Linq;

namespace Yabber
{
    static class YBUtil
    {
        private static readonly Regex DriveRx = new Regex(@"^(\w\:\\)(.+)$");
        private static readonly Regex TraversalRx = new Regex(@"^([(..)\\\/]+)(.+)?$");
        private static readonly Regex SlashRx = new Regex(@"^(\\+)(.+)$");

        /// <summary>
        /// Removes common network path roots if present.
        /// </summary>
        public static string UnrootBNDPath(string path, string root)
        {
            path = path.Substring(root.Length);

            Match drive = DriveRx.Match(path);
            if (drive.Success)
            {
                path = drive.Groups[2].Value;
            }
            
            Match traversal = TraversalRx.Match(path);
            if (traversal.Success)
            {
                path = traversal.Groups[2].Value;
            }
            if (path.Contains("..\\") || path.Contains("../")) throw new InvalidDataException($"the path {path} contains invalid data, attempting to extract to a different folder. Please report this bnd to Nordgaren.");
            return RemoveLeadingBackslashes(path);
        }

        private static string RemoveLeadingBackslashes(string path)
        {

            Match slash = SlashRx.Match(path);
            if (slash.Success)
            {
                path = slash.Groups[2].Value;
            }
            return path;
        }

        public static void Backup(string path)
        {
            if (File.Exists(path) && !File.Exists(path + ".bak"))
                File.Move(path, path + ".bak");
        }

        private static byte[] ds2RegulationKey = { 0x40, 0x17, 0x81, 0x30, 0xDF, 0x0A, 0x94, 0x54, 0x33, 0x09, 0xE1, 0x71, 0xEC, 0xBF, 0x25, 0x4C };

        /// <summary>
        /// Decrypts and unpacks DS2's regulation BND4 from the specified path.
        /// </summary>
        public static BND4 DecryptDS2Regulation(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            byte[] iv = new byte[16];
            iv[0] = 0x80;
            Array.Copy(bytes, 0, iv, 1, 11);
            iv[15] = 1;
            byte[] input = new byte[bytes.Length - 32];
            Array.Copy(bytes, 32, input, 0, bytes.Length - 32);
            using (var ms = new MemoryStream(input))
            {
                byte[] decrypted = CryptographyUtil.DecryptAesCtr(ms, ds2RegulationKey, iv);
                return BND4.Read(decrypted);
            }
        }
        
        static (string, string)[] _pathValueTuple = new (string, string)[]
        {
            (@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath"),
            (@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath"),
            (@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath"),
            (@"HKEY_CURRENT_USER\SOFTWARE\Wow6432Node\Valve\Steam", "SteamPath"),
        };

        public static string TryGetGameInstallLocation(string gamePath)
        {
            if (!gamePath.StartsWith("\\") && !gamePath.StartsWith("/"))
                return null;

            string steamPath = GetSteamInstallPath();

            if (string.IsNullOrWhiteSpace(steamPath))
                return null;

            string[] libraryFolders = File.ReadAllLines($@"{steamPath}/SteamApps/libraryfolders.vdf");
            char[] seperator = { '\t' };

            foreach (string line in libraryFolders)
            {
                if (!line.Contains("\"path\""))
                    continue;

                string[] split = line.Split(seperator, StringSplitOptions.RemoveEmptyEntries);
                string libpath = split.FirstOrDefault(x => x.ToLower().Contains("steam")).Replace("\"", "").Replace("\\\\", "\\");
                string libraryPath = libpath + gamePath;

                if (File.Exists(libraryPath))
                    return libraryPath.Replace("\\\\", "\\");
            }

            return null;
        }

        public static string GetSteamInstallPath()
        {
            string installPath = null;

            foreach ((string Path, string Value) pathValueTuple in _pathValueTuple)
            {
                string registryKey = pathValueTuple.Path;
                installPath = (string)Registry.GetValue(registryKey, pathValueTuple.Value, null);

                if (installPath != null)
                    break;
            }

            return installPath;
        }

        private static string[] OodleGames = 
        {
            "Sekiro",
            "ELDEN RING",
        };
        public static string GetOodlePath()
        {
            foreach (string game in OodleGames) {
                string path = TryGetGameInstallLocation($"\\steamapps\\common\\{game}\\Game\\oo2core_6_win64.dll");
                if (path != null) 
                    return path;
            }

            return null;
        }

    }
}
