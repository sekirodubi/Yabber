﻿using SoulsFormats;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Xml;

namespace Yabber
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                Console.WriteLine(
                    $"{assembly.GetName().Name} {assembly.GetName().Version}\n\n" +
                    "Yabber+.DCX has no GUI.\n" +
                    "Drag and drop a DCX onto the exe to decompress it,\n" +
                    "or a decompressed file to recompress it.\n\n" +
                    "Press any key to exit."
                    );
                Console.ReadKey();
                return;
            }

            bool error = false;
            int errorcode = 0;

            foreach (string path in args)
            {
                try
                {
                    if (DCX.Is(path))
                    {
                        error |= Decompress(path);
                    }
                    else
                    {
                        error |= Compress(path);
                    }
                }
                catch (DllNotFoundException ex) when (ex.Message.Contains("oo2core_6_win64.dll"))
                {
                    Console.Error.WriteLine(
                        "ERROR: oo2core_6_win64.dll not found. Please copy this library from the game directory to Yabber's directory.");
                    errorcode = 3;
                    error = true;
                }
                catch (UnauthorizedAccessException)
                {
                    using (Process current = Process.GetCurrentProcess())
                    {
                        var admin = new Process();
                        admin.StartInfo = current.StartInfo;
                        admin.StartInfo.FileName = current.MainModule.FileName;
                        admin.StartInfo.Arguments = Environment.CommandLine.Replace($"\"{Environment.GetCommandLineArgs()[0]}\"", "");
                        admin.StartInfo.Verb = "runas";
                        admin.Start();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    Console.Error.WriteLine($"ERROR: Unhandled exception: {ex}");
                    errorcode = 1;
                    error = true;
                }

                Console.WriteLine();
            }

            if (error)
            {
                Console.WriteLine("One or more errors were encountered and displayed above.\nPress any key to exit.");
                Console.ReadKey();
                Environment.Exit(errorcode);
            }
        }

        private static bool Decompress(string sourceFile)
        {
            Console.WriteLine($"Decompressing DCX: {Path.GetFileName(sourceFile)}...");

            string sourceDir = new FileInfo(sourceFile).Directory.FullName;
            string outPath;
            if (sourceFile.EndsWith(".dcx"))
                outPath = $"{sourceDir}\\{Path.GetFileNameWithoutExtension(sourceFile)}";
            else
                outPath = $"{sourceFile}.undcx";

            byte[] bytes = DCX.Decompress(sourceFile, out DCX.Type compression);
            File.WriteAllBytes(outPath, bytes);

            XmlWriterSettings xws = new XmlWriterSettings();
            xws.Indent = true;
            XmlWriter xw = XmlWriter.Create($"{outPath}-yabber-dcx.xml", xws);

            xw.WriteStartElement("dcx");
            xw.WriteElementString("compression", compression.ToString());
            xw.WriteEndElement();
            xw.Close();

            return false;
        }

        private static bool Compress(string path)
        {
            string xmlPath = $"{path}-yabber-dcx.xml";
            if (!File.Exists(xmlPath))
            {
                Console.WriteLine($"XML file not found: {xmlPath}");
                return true;
            }

            Console.WriteLine($"Compressing file: {Path.GetFileName(path)}...");
            XmlDocument xml = new XmlDocument();
            xml.Load(xmlPath);
            DCX.Type compression = (DCX.Type)Enum.Parse(typeof(DCX.Type), xml.SelectSingleNode("dcx/compression").InnerText);

            string outPath;
            if (path.EndsWith(".undcx"))
                outPath = path.Substring(0, path.Length - 6);
            else
                outPath = path + ".dcx";

            if (File.Exists(outPath) && !File.Exists(outPath + ".bak"))
                File.Move(outPath, outPath + ".bak");

            DCX.Compress(File.ReadAllBytes(path), compression, outPath);

            return false;
        }
    }
}
