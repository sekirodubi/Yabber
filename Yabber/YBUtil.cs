using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using SoulsFormats;

namespace Yabber
{
    static class YBUtil
    {
        private static List<string> pathRoots = new List<string>
        {
            // Demon's Souls
            @"N:\DemonsSoul\data\DVDROOT\",
            @"N:\DemonsSoul\data\",
            @"N:\DemonsSoul\",
            @"Z:\data\",
            // Ninja Blade
            @"I:\NinjaBlade\",
            // Dark Souls 1
            @"N:\FRPG\data\INTERROOT_win32\",
            @"N:\FRPG\data\INTERROOT_win64\",
            @"N:\FRPG\data\INTERROOT_x64\",
            @"N:\FRPG\data\INTERROOT\",
            @"N:\FRPG\data\",
            @"N:\FRPG\",
            // Dark Souls 2
            @"N:\FRPG2\data",
            @"N:\FRPG2\",
            @"N:\FRPG2_64\data\",
            @"N:\FRPG2_64\",
            // Dark Souls 3
            @"N:\FDP\data\INTERROOT_ps4\",
            @"N:\FDP\data\INTERROOT_win64\",
            @"N:\FDP\data\INTERROOT_xboxone\",
            @"N:\FDP\data\",
            @"N:\FDP\",
            // Bloodborne
            @"N:\SPRJ\data\DVDROOT_win64\",
            @"N:\SPRJ\data\INTERROOT_ps4\",
            @"N:\SPRJ\data\INTERROOT_ps4_havok\",
            @"N:\SPRJ\data\INTERROOT_win64\",
            @"N:\SPRJ\data\",
            @"N:\SPRJ\",
            // Sekiro
            @"N:\NTC\data\Target\INTERROOT_win64_havok\",
            @"N:\NTC\data\Target\INTERROOT_win64\",
            @"N:\NTC\data\Target\",
            @"N:\NTC\data\",
            @"N:\NTC\",
        };

        private static readonly Regex DriveRx = new Regex(@"^(\w\:\\)(.+)$");
        private static readonly Regex TraversalRx = new Regex(@"^([..\\]+)(.+)$");
        private static readonly Regex SlashRx = new Regex(@"^(\\+)(.+)$");

        /// <summary>
        /// Removes common network path roots if present.
        /// </summary>
        public static string UnrootBNDPath(string path, out string root)
        {
            root = "";
            foreach (string pathRoot in pathRoots)
            {
                if (path.ToLower().StartsWith(pathRoot.ToLower()))
                {
                    root = path.Substring(0, pathRoot.Length);
                    path = path.Substring(pathRoot.Length);
                    break;
                }
            }

            Match drive = DriveRx.Match(path);
            if (drive.Success)
            {
                root = drive.Groups[1].Value;
                path = drive.Groups[2].Value;
            }
            
            Match traversal = TraversalRx.Match(path);
            if (traversal.Success)
            {
                root += traversal.Groups[1].Value;
                path = traversal.Groups[2].Value;
            }
            if (path.Contains("..\\")) throw new InvalidDataException($"the path {path} contains invalid data. Please report this bnd to someone on ?ServerName?");
            return RemoveLeadingBackslashes(path, ref root);
        }

        private static string RemoveLeadingBackslashes(string path, ref string root)
        {

            Match slash = SlashRx.Match(path);
            if (slash.Success)
            {
                root += slash.Groups[1].Value;
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
    }
}
