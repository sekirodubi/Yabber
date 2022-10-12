using SoulsFormats;
using SoulsFormats.AC4;
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
#if DEBUG
            args = new string[] { @"C:\Users\Nord\source\repos\CSharp\Yabber\Security Test\deep\folder\gameparam.parambnd"};
#endif
            if (args.Length == 0)
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                Console.WriteLine(
                    $"{assembly.GetName().Name} {assembly.GetName().Version}\n\n" +
                    "Yabber+ has no GUI.\n" +
                    "Drag and drop a file onto the exe to unpack it,\n" +
                    "or an unpacked folder to repack it.\n\n" +
                    "DCX files will be transparently decompressed and recompressed;\n" +
                    "If you need to decompress or recompress an unsupported format,\n" +
                    "use Yabber+.DCX instead.\n\n" +
                    "Press any key to exit."
                );
                Console.ReadKey();
                return;
            }

            bool pause = false;

            foreach (string path in args)
            {
                try
                {
                    int maxProgress = Console.WindowWidth - 1;
                    int lastProgress = 0;

                    void report(float value)
                    {
                        int nextProgress = (int)Math.Ceiling(value * maxProgress);
                        if (nextProgress > lastProgress)
                        {
                            for (int i = lastProgress; i < nextProgress; i++)
                            {
                                if (i == 0)
                                    Console.Write('[');
                                else if (i == maxProgress - 1)
                                    Console.Write(']');
                                else
                                    Console.Write('=');
                            }

                            lastProgress = nextProgress;
                        }
                    }

                    IProgress<float> progress = new Progress<float>(report);

                    if (Directory.Exists(path))
                    {
                        pause |= RepackDir(path, progress);

                    }
                    else if (File.Exists(path))
                    {
                        pause |= UnpackFile(path, progress);
                    }
                    else
                    {
                        Console.WriteLine($"File or directory not found: {path}");
                        pause = true;
                    }

                    if (lastProgress > 0)
                    {
                        progress.Report(1);
                        Console.WriteLine();
                    }
                }
                catch (DllNotFoundException ex) when (ex.Message.Contains("oo2core_6_win64.dll"))
                {
                    Console.WriteLine(
                        "In order to decompress .dcx files from games, starting with Sekiro, you must copy ANY oo2core_6_win64.dll into Yabber's lib folder from a game that has it (hint: Elden Ring).");
                    pause = true;
                }
                catch (UnauthorizedAccessException)
                {
                    using (Process current = Process.GetCurrentProcess())
                    {
                        var admin = new Process();
                        admin.StartInfo = current.StartInfo;
                        admin.StartInfo.FileName = current.MainModule.FileName;
                        admin.StartInfo.Arguments =
                            Environment.CommandLine.Replace($"\"{Environment.GetCommandLineArgs()[0]}\"", "");
                        admin.StartInfo.Verb = "runas";
                        admin.Start();
                        return;
                    }
                }
                catch (FriendlyException ex)
                {
                    Console.WriteLine();
                    Console.WriteLine($"Error: {ex.Message}");
                    pause = true;
                }
                //catch (Exception ex)
                //{
                //    Console.WriteLine();
                //    Console.WriteLine($"Unhandled exception: {ex}");
                //    pause = true;
                //}

                Console.WriteLine();
            }

            if (pause)
            {
                Console.WriteLine("One or more errors were encountered and displayed above.\nPress any key to exit.");
                Console.ReadKey();
            }
        }

        private static bool UnpackFile(string sourceFile, IProgress<float> progress)
        {
            string sourceDir = Path.GetDirectoryName(sourceFile);
            string fileName = Path.GetFileName(sourceFile);
            string targetDir = $"{sourceDir}\\{fileName.Replace('.', '-')}";
            if (File.Exists(targetDir))
                targetDir += "-ybr";

            if (fileName.Contains("regulation.bnd.dcx") || fileName.Contains("Data0") || fileName.Contains("regulation.bin") || fileName.Contains("regulation.bnd"))
                return UnpackRegulationFile(fileName, sourceDir, targetDir, progress);



            if (DCX.Is(sourceFile))
            {
                Console.WriteLine($"Decompressing DCX: {fileName}...");
                byte[] bytes = DCX.Decompress(sourceFile, out DCX.Type compression);
                if (BND3.Is(bytes))
                {
                    Console.WriteLine($"Unpacking BND3: {fileName}...");
                    using (var bnd = new BND3Reader(bytes))
                    {
                        bnd.Compression = compression;
                        bnd.Unpack(fileName, targetDir, progress);
                    }
                }
                else if (BND4.Is(bytes))
                {
                    Console.WriteLine($"Unpacking BND4: {fileName}...");
                    using (var bnd = new BND4Reader(bytes))
                    {
                        bnd.Compression = compression;
                        bnd.Unpack(fileName, targetDir, progress);
                    }
                }
                else if (FFXDLSE.Is(bytes))
                {
                    Console.WriteLine($"Unpacking FFX: {fileName}...");
                    var ffx = FFXDLSE.Read(bytes);
                    ffx.Compression = compression;
                    ffx.Unpack(sourceFile);
                }
                else if (sourceFile.EndsWith(".fmg.dcx"))
                {
                    Console.WriteLine($"Unpacking FMG: {fileName}...");
                    FMG fmg = FMG.Read(bytes);
                    fmg.Compression = compression;
                    fmg.Unpack(sourceFile);
                }
                else if (GPARAM.Is(bytes))
                {
                    Console.WriteLine($"Unpacking GPARAM: {fileName}...");
                    GPARAM gparam = GPARAM.Read(bytes);
                    gparam.Compression = compression;
                    gparam.Unpack(sourceFile);
                }
                else if (TPF.Is(bytes))
                {
                    Console.WriteLine($"Unpacking TPF: {fileName}...");
                    TPF tpf = TPF.Read(bytes);
                    tpf.Compression = compression;
                    tpf.Unpack(fileName, targetDir, progress);
                }
                else
                {
                    Console.WriteLine($"File format not recognized: {fileName}");
                    return true;
                }
            }
            else
            {
                if (BND3.Is(sourceFile))
                {
                    Console.WriteLine($"Unpacking BND3: {fileName}...");
                    using (var bnd = new BND3Reader(sourceFile))
                    {
                        bnd.Unpack(fileName, targetDir, progress);
                    }
                }
                else if (BND4.Is(sourceFile))
                {
                    Console.WriteLine($"Unpacking BND4: {fileName}...");
                    using (var bnd = new BND4Reader(sourceFile))
                    {
                        bnd.Unpack(fileName, targetDir, progress);
                    }
                }
                else if (BXF3.IsBHD(sourceFile))
                {
                    string bdtExtension = Path.GetExtension(fileName).Replace("bhd", "bdt");
                    string bdtFilename = $"{Path.GetFileNameWithoutExtension(fileName)}{bdtExtension}";
                    string bdtPath = $"{sourceDir}\\{bdtFilename}";
                    if (File.Exists(bdtPath))
                    {
                        Console.WriteLine($"Unpacking BXF3: {fileName}...");
                        using (var bxf = new BXF3Reader(sourceFile, bdtPath))
                        {
                            bxf.Unpack(fileName, bdtFilename, targetDir, progress);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"BDT not found for BHD: {fileName}");
                        return true;
                    }
                }
                else if (BXF4.IsBHD(sourceFile))
                {
                    string bdtExtension = Path.GetExtension(fileName).Replace("bhd", "bdt");
                    string bdtFilename = $"{Path.GetFileNameWithoutExtension(fileName)}{bdtExtension}";
                    string bdtPath = $"{sourceDir}\\{bdtFilename}";
                    if (File.Exists(bdtPath))
                    {
                        Console.WriteLine($"Unpacking BXF4: {fileName}...");
                        using (var bxf = new BXF4Reader(sourceFile, bdtPath))
                        {
                            bxf.Unpack(fileName, bdtFilename, targetDir, progress);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"BDT not found for BHD: {fileName}");
                        return true;
                    }
                }
                else if (FFXDLSE.Is(sourceFile))
                {
                    Console.WriteLine($"Unpacking FFX: {fileName}...");
                    var ffx = FFXDLSE.Read(sourceFile);
                    ffx.Unpack(sourceFile);
                }
                else if (sourceFile.EndsWith(".ffx.xml") || sourceFile.EndsWith(".ffx.dcx.xml"))
                {
                    Console.WriteLine($"Repacking FFX: {fileName}...");
                    YFFX.Repack(sourceFile);
                }
                else if (sourceFile.EndsWith(".fmg"))
                {
                    Console.WriteLine($"Unpacking FMG: {fileName}...");
                    FMG fmg = FMG.Read(sourceFile);
                    fmg.Unpack(sourceFile);
                }
                else if (sourceFile.EndsWith(".fmg.xml") || sourceFile.EndsWith(".fmg.dcx.xml"))
                {
                    Console.WriteLine($"Repacking FMG: {fileName}...");
                    YFMG.Repack(sourceFile);
                }
                else if (GPARAM.Is(sourceFile))
                {
                    Console.WriteLine($"Unpacking GPARAM: {fileName}...");
                    GPARAM gparam = GPARAM.Read(sourceFile);
                    gparam.Unpack(sourceFile);
                }
                else if (sourceFile.EndsWith(".gparam.xml") || sourceFile.EndsWith(".gparam.dcx.xml")
                                                            || sourceFile.EndsWith(".fltparam.xml") ||
                                                            sourceFile.EndsWith(".fltparam.dcx.xml"))
                {
                    Console.WriteLine($"Repacking GPARAM: {fileName}...");
                    YGPARAM.Repack(sourceFile);
                }
                else if (sourceFile.EndsWith(".luagnl"))
                {
                    Console.WriteLine($"Unpacking LUAGNL: {fileName}...");
                    LUAGNL gnl = LUAGNL.Read(sourceFile);
                    gnl.Unpack(sourceFile);
                }
                else if (sourceFile.EndsWith(".luagnl.xml"))
                {
                    Console.WriteLine($"Repacking LUAGNL: {fileName}...");
                    YLUAGNL.Repack(sourceFile);
                }
                else if (LUAINFO.Is(sourceFile))
                {
                    Console.WriteLine($"Unpacking LUAINFO: {fileName}...");
                    LUAINFO info = LUAINFO.Read(sourceFile);
                    info.Unpack(sourceFile);
                }
                else if (sourceFile.EndsWith(".luainfo.xml"))
                {
                    Console.WriteLine($"Repacking LUAINFO: {fileName}...");
                    YLUAINFO.Repack(sourceFile);
                }
                else if (TPF.Is(sourceFile))
                {
                    Console.WriteLine($"Unpacking TPF: {fileName}...");
                    TPF tpf = TPF.Read(sourceFile);
                    tpf.Unpack(fileName, targetDir, progress);
                }
                else if (Zero3.Is(sourceFile))
                {
                    Console.WriteLine($"Unpacking 000: {fileName}...");
                    Zero3 z3 = Zero3.Read(sourceFile);
                    z3.Unpack(targetDir);
                }
                else
                {
                    Console.WriteLine($"File format not recognized: {fileName}");
                    return true;
                }
            }

            return false;
        }

        private static bool UnpackRegulationFile(string fileName, string sourceDir, string targetDir, IProgress<float> progress)
        {

            if (fileName.Contains("regulation.bin"))
            {
                string destPath = Path.Combine(sourceDir, fileName);
                BND4 bnd = SFUtil.DecryptERRegulation(destPath);
                Console.WriteLine($"ER Regulation Bin: {fileName}...");
                using (var bndReader = new BND4Reader(bnd.Write()))
                {
                    bndReader.Unpack(fileName, targetDir, progress);
                }

                return false;
            }

            if (fileName.Contains("Data0"))
            {
                string destPath = Path.Combine(sourceDir, "Data0.bdt");
                try 
                {
                    BND4 bnd = SFUtil.DecryptDS3Regulation(destPath);
                    Console.WriteLine($"Unpacking DS3 Regulation Bin: {fileName}...");
                    using (var bndReader = new BND4Reader(bnd.Write())) {
                        bndReader.Unpack(fileName, targetDir, progress);
                    }
                    
                } catch (Exception e) 
                {
                    
                }

                return false;
            }

            if (fileName.Contains("enc_regulation.bnd.dcx"))
            {
                string destPath = Path.Combine(sourceDir, fileName);
                BND4 bnd = YBUtil.DecryptDS2Regulation(destPath);
                Console.WriteLine($"Unpacking DS2 Regulation Bin: {fileName}...");
                using (var bndReader = new BND4Reader(bnd.Write()))
                {
                    bndReader.Unpack(fileName, targetDir, progress);
                }

                return false;
            }

            throw new InvalidOperationException("This state is unreachable. Please contact Nordgaren about this regulation.bin.");
        }
        
        public static bool Confirm(string message)
        {
            ConsoleKey response;
            do
            {
                Console.Write($"{message} [y/n] ");
                response = Console.ReadKey(false).Key;
                if (response != ConsoleKey.Enter)
                {
                    Console.WriteLine();
                }
            } while (response != ConsoleKey.Y && response != ConsoleKey.N);

            return (response == ConsoleKey.Y);
        }

        private static bool RepackDir(string sourceDir, IProgress<float> progress)
        {
            string sourceName = new DirectoryInfo(sourceDir).Name;
            string targetDir = new DirectoryInfo(sourceDir).Parent.FullName;


            if (File.Exists($"{sourceDir}\\_yabber-bnd3.xml"))
            {
                Console.WriteLine($"Repacking BND3: {sourceName}...");
                YBND3.Repack(sourceDir, targetDir);
            }
            else if (File.Exists($"{sourceDir}\\_yabber-bnd4.xml"))
            {
                Console.WriteLine($"Repacking BND4: {sourceName}...");
                YBND4.Repack(sourceDir, targetDir);
            }
            else if (File.Exists($"{sourceDir}\\_yabber-bxf3.xml"))
            {
                Console.WriteLine($"Repacking BXF3: {sourceName}...");
                YBXF3.Repack(sourceDir, targetDir);
            }
            else if (File.Exists($"{sourceDir}\\_yabber-bxf4.xml"))
            {
                Console.WriteLine($"Repacking BXF4: {sourceName}...");
                YBXF4.Repack(sourceDir, targetDir);
            }
            else if (File.Exists($"{sourceDir}\\_yabber-tpf.xml"))
            {
                Console.WriteLine($"Repacking TPF: {sourceName}...");
                YTPF.Repack(sourceDir, targetDir);
            }
            else
            {
                Console.WriteLine($"Yabber XML not found in: {sourceName}");
                return true;
            }

            if (sourceName.Contains("regulation-bnd-dcx") || sourceName.Contains("Data0") || sourceName.Contains("regulation-bin"))
                return ReEncryptRegulationFile(sourceName, sourceDir, targetDir, progress);

            return false;
        }

        private static bool ReEncryptRegulationFile(string sourceName, string sourceDir, string targetDir, IProgress<float> progress)
        {
            XmlDocument xml = new XmlDocument();
            xml.Load($"{sourceDir}\\_yabber-tpf.xml");

            string filename = xml.SelectSingleNode("tpf/filename").InnerText;
            string regFile = $"{sourceDir}\\{filename}";

            if (sourceName.Contains("regulation.bin"))
            {
                BND4 bnd = BND4.Read(regFile);
                SFUtil.EncryptERRegulation(regFile, bnd);
                return false;
            }

            if (sourceName.Contains("Data0"))
            {
                BND4 bnd = BND4.Read(regFile);
                SFUtil.EncryptDS3Regulation(regFile, bnd);
                return false;
            }

            if (sourceName.Contains("enc_regulation.bnd.dcx"))
            {
                if (!Confirm("DS2 files cannot be re-encrypted, yet, so re-packing this folder might ruin your encrypted bnd.")) {
                    return false;
                }
                
                string destPath = Path.Combine(sourceDir, sourceName);
                BND4 bnd = BND4.Read(destPath);//YBUtil.DecryptDS2Regulation(destPath); I will have to investigate re-encrypting DS2 regulation later.  
                Console.WriteLine($"Repacking DS2 Regulation Bin: {sourceName}...");
                YBND4.Repack(sourceDir, targetDir);
                return false;
            }

            throw new InvalidOperationException("This state is unreachable. If your regulation bin is named correctly, please contact Nordgaren about this regulation.bin. Otherwise" +
                                                "make sure your bnd contains the original bnd name.");
        }
    }
}
