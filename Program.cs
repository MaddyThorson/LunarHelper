using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace SMWPatcher
{
    class Program
    {
        static public Config Config { get; private set; }

        static private readonly Regex LevelRegex = new Regex("[0-9a-fA-F]{3}");
        static private Process RetroArchProcess;
        static private Process LunarMagicProcess;

        static void Main(string[] args)
        {
            bool running = true;
            while (running)
            {
                Log("Welcome to Lunar Helper ^_^", ConsoleColor.Cyan);
                Log("S - Save, B - Build, R - Run");
                Log("T - Test (Save -> Build -> Run)");
                Log("E - Edit (in Lunar Magic)");
                Log("P - Package, H - Help, ESC - Exit");
                Console.WriteLine();

                var key = Console.ReadKey(true);
                Console.Clear();

                switch (key.Key)
                {
                    case ConsoleKey.B:
                        if (Init())
                            Build();
                        break;

                    case ConsoleKey.T:
                        if (Init() && Save() && Build())
                            Test();
                        break;

                    case ConsoleKey.R:
                        if (Init())
                            Test();
                        break;

                    case ConsoleKey.E:
                        if (Init())
                            Edit();
                        break;

                    case ConsoleKey.S:
                        if (Init())
                            Save();
                        break;

                    case ConsoleKey.P:
                        if (Init() && Build())
                            Package();
                        break;

                    case ConsoleKey.H:
                        Help();
                        break;

                    case ConsoleKey.Escape:
                        running = false;
                        Log("Have a nice day!", ConsoleColor.Cyan);
                        Console.ForegroundColor = ConsoleColor.White;
                        break;

                    default:
                        string str = char.ToUpperInvariant(key.KeyChar).ToString().Trim();
                        if (str.Length > 0)
                            Log($"Key '{str}' is not a recognized option!", ConsoleColor.Red);
                        else
                            Log($"Key is not a recognized option!", ConsoleColor.Red);
                        Console.WriteLine();
                        break;
                }

                while (Console.KeyAvailable)
                    Console.ReadKey(true);
            }
        }

        static private bool Init()
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            // load config
            Config = Config.Load();
            if (Config == null)
            {
                Error("Could not open config.txt file(s)");
                return false;
            }

            // set the working directory
            if (!string.IsNullOrWhiteSpace(Config.WorkingDirectory))
            {
                if (!Directory.Exists(Config.WorkingDirectory))
                {
                    Error("The configured Working Directory doesn't exist");
                    return false;
                }

                Directory.SetCurrentDirectory(Config.WorkingDirectory);
            } 

            // some error checks
            if (string.IsNullOrWhiteSpace(Config.CleanPath))
            {
                Error("No Clean ROM path provided");
                return false;
            }
            else if (!File.Exists(Config.CleanPath))
            {
                Error($"Clean ROM file '{Config.CleanPath}' does not exist");
                return false;
            }
            else if (string.IsNullOrWhiteSpace(Config.OutputPath))
            {
                Error("No Output ROM path provided");
                return false;
            }
            else if (string.IsNullOrWhiteSpace(Config.TempPath))
            {
                Error("No Temp ROM path provided");
                return false;
            }

            return true;
        }

        static private bool Build()
        {
            // Lunar Magic required
            if (string.IsNullOrWhiteSpace(Config.LunarMagicPath))
            {
                Log("No path to Lunar Magic provided!", ConsoleColor.Red);
                return false;
            }
            else if (!File.Exists(Config.LunarMagicPath))
            {
                Log("Lunar Magic not found at provided path!", ConsoleColor.Red);
                return false;
            }

            // delete existing temp ROM
            if (File.Exists(Config.TempPath))
                File.Delete(Config.TempPath);

            // initial patch
            if (!string.IsNullOrWhiteSpace(Config.InitialPatch))
            {
                Log("Initial Patch", ConsoleColor.Cyan);

                var fullPatchPath = Path.GetFullPath(Config.InitialPatch);
                var fullCleanPath = Path.GetFullPath(Config.CleanPath);
                var fullTempPath = Path.GetFullPath(Config.TempPath);
                if (ApplyPatch(fullCleanPath, fullTempPath, fullPatchPath))
                    Log("Initial Patch Success!", ConsoleColor.Green);
                else
                {
                    Log("Initial Patch Failure!", ConsoleColor.Red);
                    return false;
                }

                Console.WriteLine();
            }
            else
            {
                File.Copy(Config.CleanPath, Config.TempPath);
            }

            // run GPS
            Log("GPS", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.GPSPath))
                Log("No path to GPS provided, no blocks will be inserted.", ConsoleColor.Red);
            else if (!File.Exists(Config.GPSPath))
                Log("GPS not found at provided path, no blocks will be inserted.", ConsoleColor.Red);
            else
            {
                var dir = Path.GetFullPath(Path.GetDirectoryName(Config.GPSPath));
                var rom = Path.GetRelativePath(dir, Path.GetFullPath(Config.TempPath));
                Console.ForegroundColor = ConsoleColor.Yellow;

                ProcessStartInfo psi = new ProcessStartInfo(Config.GPSPath, $"-l \"{dir}/list.txt\" \"{rom}\"");
                psi.RedirectStandardInput = true;
                psi.WorkingDirectory = dir;

                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("GPS Success!", ConsoleColor.Green);
                else
                {
                    Log("GPS Failure!", ConsoleColor.Red);
                    return false;
                }

                Console.WriteLine();
            }

            // run PIXI
            Log("PIXI", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.PixiPath))
                Log("No path to Pixi provided, no sprites will be inserted.", ConsoleColor.Red);
            else if (!File.Exists(Config.PixiPath))
                Log("Pixi not found at provided path, no sprites will be inserted.", ConsoleColor.Red);
            else
            {
                var dir = Path.GetFullPath(Path.GetDirectoryName(Config.PixiPath));
                var list = Path.Combine(dir, "list.txt");
                Console.ForegroundColor = ConsoleColor.Yellow;

                ProcessStartInfo psi = new ProcessStartInfo(Config.PixiPath, $"-l \"{list}\" \"{Config.TempPath}\"");
                psi.RedirectStandardInput = true;

                var p = Process.Start(psi);
                while (!p.HasExited)
                    p.StandardInput.Write('a');

                if (p.ExitCode == 0)
                    Log("Pixi Success!", ConsoleColor.Green);
                else
                {
                    Log("Pixi Failure!", ConsoleColor.Red);
                    return false;
                }

                Console.WriteLine();
            }

            // asar patches
            Log("Patches", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.AsarPath))
                Log("No path to Asar provided, not applying any patches.", ConsoleColor.Red);
            else if (!File.Exists(Config.AsarPath))
                Log("Asar not found at provided path, not applying any patches.", ConsoleColor.Red);
            else if (Config.Patches.Count == 0)
                Log("Path to Asar provided, but no patches were registerd to be applied.", ConsoleColor.Red);
            else
            {
                foreach (var patch in Config.Patches)
                {
                    Lognl($"- Applying patch '{patch}'...  ", ConsoleColor.Yellow);

                    ProcessStartInfo psi = new ProcessStartInfo(Config.AsarPath, $"\"{patch}\" \"{Config.TempPath}\"");

                    var p = Process.Start(psi);
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                        Log("Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Failure!", ConsoleColor.Red);
                        return false;
                    }
                }

                Log("Patching Success!", ConsoleColor.Green);
                Console.WriteLine();
            }

            // uber ASM
            Log("Uber ASM", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.UberASMPath))
                Log("No path to UberASMTool provided, no UberASM will be inserted.", ConsoleColor.Red);
            else if (!File.Exists(Config.UberASMPath))
                Log("UberASMTool not found at provided path, no UberASM will be inserted.", ConsoleColor.Red);
            else
            {
                var dir = Path.GetFullPath(Path.GetDirectoryName(Config.UberASMPath));

                // create work folder if missing
                {
                    string bin = Path.Combine(dir, "asm", "work");
                    if (!Directory.Exists(bin))
                        Directory.CreateDirectory(bin);
                }

                var rom = Path.GetRelativePath(dir, Path.GetFullPath(Config.TempPath));
                Console.ForegroundColor = ConsoleColor.Yellow;

                ProcessStartInfo psi = new ProcessStartInfo(Config.UberASMPath, $"list.txt \"{rom}\"");
                psi.RedirectStandardInput = true;
                psi.WorkingDirectory = dir;

                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("UberASM Success!", ConsoleColor.Green);
                else
                {
                    Log("UberASM Failure!", ConsoleColor.Red);
                    return false;
                }

                Console.WriteLine();
            }

            // run AddMusicK
            Log("AddMusicK", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.AddMusicKPath))
                Log("No path to AddMusicK provided, no music will be inserted.", ConsoleColor.Red);
            else if (!File.Exists(Config.AddMusicKPath))
                Log("AddMusicK not found at provided path, no music will be inserted.", ConsoleColor.Red);
            else
            {
                var dir = Path.GetFullPath(Path.GetDirectoryName(Config.AddMusicKPath));

                // create bin folder if missing
                {
                    string bin = Path.Combine(dir, "asm", "SNES", "bin");
                    if (!Directory.Exists(bin))
                        Directory.CreateDirectory(bin);
                }

                var rom = Path.GetRelativePath(dir, Path.GetFullPath(Config.TempPath));
                Console.ForegroundColor = ConsoleColor.Yellow;

                ProcessStartInfo psi = new ProcessStartInfo(Config.AddMusicKPath, $"\"{rom}\"");
                psi.RedirectStandardInput = true;
                psi.WorkingDirectory = dir;

                var p = Process.Start(psi);
                while (!p.HasExited)
                    p.StandardInput.Write('a');

                if (p.ExitCode == 0)
                    Log("AddMusicK Success!", ConsoleColor.Green);
                else
                {
                    Log("AddMusicK Failure!", ConsoleColor.Red);
                    return false;
                }

                Console.WriteLine();
            }

            // import gfx
            Log("Graphics", ConsoleColor.Cyan);
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"-ImportAllGraphics \"{Config.TempPath}\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Import Graphics Success!", ConsoleColor.Green);
                else
                {
                    Log("Import Graphics Failure!", ConsoleColor.Red);
                    return false;
                }

                // rename MSC file so music track names work in LM
                var msc_at = $"{Path.GetFileNameWithoutExtension(Config.TempPath)}.msc";
                var msc_to = $"{Path.GetFileNameWithoutExtension(Config.OutputPath)}.msc";
                if (File.Exists(msc_to))
                    File.Delete(msc_to);
                if (File.Exists(msc_at))
                    File.Move(msc_at, msc_to);

                Console.WriteLine();
            }

            // import map16
            Log("Map16", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.Map16Path))
                Log("No path to Map16 provided, no map16 will be imported.", ConsoleColor.Red);
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"-ImportAllMap16 \"{Config.TempPath}\" \"{Config.Map16Path}\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Map16 Import Success!", ConsoleColor.Green);
                else
                {
                    Log("Map16 Import Failure!", ConsoleColor.Red);
                    return false;
                }

                Console.WriteLine();
            }

            // import title moves
            Log("Title Moves", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.TitleMovesPath))
                Log("No path to Title Moves provided, no title moves will be imported.", ConsoleColor.Red);
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"-ImportTitleMoves \"{Config.TempPath}\" \"{Config.TitleMovesPath}\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Title Moves Import Success!", ConsoleColor.Green);
                else
                {
                    Log("Title Moves Import Failure!", ConsoleColor.Red);
                    return false;
                }

                Console.WriteLine();
            }

            // import shared palette
            Log("Shared Palette", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.SharedPalettePath))
                Log("No path to Shared Palette provided, no palette will be imported.", ConsoleColor.Red);
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"-ImportSharedPalette \"{Config.TempPath}\" \"{Config.SharedPalettePath}\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Shared Palette Import Success!", ConsoleColor.Green);
                else
                {
                    Log("Shared Palette Import Failure!", ConsoleColor.Red);
                    return false;
                }

                Console.WriteLine();
            }

            // import global data
            Log("Global Data", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.GlobalDataPath))
                Log("No path to Global Data BPS provided, no global data will be imported.", ConsoleColor.Red);
            else if (!File.Exists(Config.GlobalDataPath))
                Log("No path to Global Data BPS file found at the path provided, no global data will be imported.", ConsoleColor.Red);
            else
            {
                ProcessStartInfo psi;
                Process p;
                string globalDataROMPath = Path.Combine(
                    Path.GetFullPath(Path.GetDirectoryName(Config.GlobalDataPath)),
                    Path.GetFileNameWithoutExtension(Config.GlobalDataPath) + ".smc");

                //Apply patch to clean ROM
                {
                    var fullPatchPath = Path.GetFullPath(Config.GlobalDataPath);
                    var fullCleanPath = Path.GetFullPath(Config.CleanPath);

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    psi = new ProcessStartInfo(Config.FlipsPath,
                            $"--apply \"{fullPatchPath}\" \"{fullCleanPath}\" \"{globalDataROMPath}\"");
                    p = Process.Start(psi);
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                        Log("Global Data Patch Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Global Data Patch Failure!", ConsoleColor.Red);
                        return false;
                    }
                }

                //Overworld
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    psi = new ProcessStartInfo(Config.LunarMagicPath,
                                $"-TransferOverworld \"{Config.TempPath}\" \"{globalDataROMPath}\"");
                    p = Process.Start(psi);
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                        Log("Overworld Import Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Overworld Import Failure!", ConsoleColor.Red);
                        return false;
                    }
                }

                //Global EX Animations
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    psi = new ProcessStartInfo(Config.LunarMagicPath,
                                $"-TransferLevelGlobalExAnim \"{Config.TempPath}\" \"{globalDataROMPath}\"");
                    p = Process.Start(psi);
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                        Log("Global EX Animation Import Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Global EX Animation Import Failure!", ConsoleColor.Red);
                        return false;
                    }
                }

                //Title Screen
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    psi = new ProcessStartInfo(Config.LunarMagicPath,
                                $"-TransferTitleScreen \"{Config.TempPath}\" \"{globalDataROMPath}\"");
                    p = Process.Start(psi);
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                        Log("Title Screen Import Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Title Screen Import Failure!", ConsoleColor.Red);
                        return false;
                    }
                }

                //Credits
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    psi = new ProcessStartInfo(Config.LunarMagicPath,
                                $"-TransferCredits \"{Config.TempPath}\" \"{globalDataROMPath}\"");
                    p = Process.Start(psi);
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                        Log("Credits Import Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Credits Import Failure!", ConsoleColor.Red);
                        return false;
                    }
                }

                if (File.Exists(globalDataROMPath))
                    File.Delete(globalDataROMPath);

                Log("All Global Data Imported!", ConsoleColor.Green);
                Console.WriteLine();
            }

            // import levels
            if (!ImportLevels(false))
                return false;

            // output final ROM
            File.Copy(Config.TempPath, Config.OutputPath, true);

            // copy other generated files
            {
                var path = Path.GetDirectoryName(Path.GetFullPath(Config.TempPath));
                var to = Path.GetDirectoryName(Path.GetFullPath(Config.OutputPath));
                to = Path.Combine(to, Path.GetFileNameWithoutExtension(Config.OutputPath));

                foreach (var file in Directory.EnumerateFiles(path, "temp*"))
                    File.Move(file, $"{to}{Path.GetExtension(file)}", true);
            }

            Log($"ROM patched successfully to '{Config.OutputPath}'!", ConsoleColor.Cyan);
            Console.WriteLine();

            return true;
        }

        static private bool Test()
        {
            Console.WriteLine();
            Log("Initiating Test routine!", ConsoleColor.Magenta);

            // test level
            if (!string.IsNullOrWhiteSpace(Config.TestLevel) && !string.IsNullOrWhiteSpace(Config.TestLevelDest))
            {
                var files = Directory.GetFiles(Config.LevelsPath, $"*{Config.TestLevel}*.mwl");

                if (!LevelRegex.IsMatch(Config.TestLevel))
                    Log("Test Level ID must be a 3-character hex value", ConsoleColor.Red);
                else if (!LevelRegex.IsMatch(Config.TestLevelDest))
                    Log("Test Level Dest ID must be a 3-character hex value", ConsoleColor.Red);
                else if (files.Length == 0)
                    Log($"Test Level {Config.TestLevel} not found in {Config.LevelsPath}", ConsoleColor.Red);
                else
                {
                    var path = files[0];

                    Log($"Importing level {Config.TestLevel} to {Config.TestLevelDest} for testing...  ", ConsoleColor.Yellow);

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                        $"-ImportLevel \"{Config.OutputPath}\" \"{path}\" {Config.TestLevelDest}");
                    var p = Process.Start(psi);
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                        Log("Test Level Import Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Test Level Import Failure!", ConsoleColor.Red);
                        return false;
                    }
                }
            }

            // retroarch
            if (!string.IsNullOrWhiteSpace(Config.RetroArchPath))
            {
                Log("Launching RetroArch...", ConsoleColor.Yellow);
                var fullRom = Path.GetFullPath(Config.OutputPath);

                if (RetroArchProcess != null && !RetroArchProcess.HasExited)
                    RetroArchProcess.Kill(true);

                ProcessStartInfo psi = new ProcessStartInfo(Config.RetroArchPath,
                    $"-L \"{Config.RetroArchCore}\" \"{fullRom}\"");
                RetroArchProcess = Process.Start(psi);
            }

            Log("Test routine complete!", ConsoleColor.Magenta);
            Console.WriteLine();

            return true;
        }

        static private bool Save()
        {
            if (!File.Exists(Config.OutputPath))
            {
                Log("Output ROM does not exist! Build first!", ConsoleColor.Red);
                return false;
            }

            // save global data
            Log("Saving Global Data BPS...", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.GlobalDataPath))
                Log("No path for GlobalData BPS provided!", ConsoleColor.Red);
            else if (string.IsNullOrWhiteSpace(Config.CleanPath))
                Log("No path for Clean ROM provided!", ConsoleColor.Red);
            else if (!File.Exists(Config.CleanPath))
                Log("Clean ROM does not exist!", ConsoleColor.Red);
            else if (string.IsNullOrWhiteSpace(Config.FlipsPath))
                Log("No path to Flips provided!", ConsoleColor.Red);
            else if (!File.Exists(Config.FlipsPath))
                Log("Flips not found at the provided path!", ConsoleColor.Red);
            else
            {
                if (File.Exists(Config.GlobalDataPath))
                    File.Delete(Config.GlobalDataPath);

                var fullCleanPath = Path.GetFullPath(Config.CleanPath);
                var fullOutputPath = Path.GetFullPath(Config.OutputPath);
                var fullPackagePath = Path.GetFullPath(Config.GlobalDataPath);
                if (CreatePatch(fullCleanPath, fullOutputPath, fullPackagePath))
                    Log("Global Data Patch Success!", ConsoleColor.Green);
                else
                {
                    Log("Global Data Patch Failure!", ConsoleColor.Red);
                    return false;
                }
            }

            // export map16
            Log("Exporting Map16...", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.Map16Path))
                Log("No path for Map16 provided!", ConsoleColor.Red);
            else if (string.IsNullOrWhiteSpace(Config.LunarMagicPath))
                Log("No Lunar Magic Path provided!", ConsoleColor.Red);
            else if (!File.Exists(Config.LunarMagicPath))
                Log("Could not find Lunar Magic at the provided path!", ConsoleColor.Red);
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"-ExportAllMap16 \"{Config.OutputPath}\" \"{Config.Map16Path}\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Map16 Export Success!", ConsoleColor.Green);
                else
                {
                    Log("Map16 Export Failure!", ConsoleColor.Red);
                    return false;
                }
            }

            // export shared palette
            Log("Exporting Shared Palette...", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.SharedPalettePath))
                Log("No path for Shared Palette provided!", ConsoleColor.Red);
            else if (string.IsNullOrWhiteSpace(Config.LunarMagicPath))
                Log("No Lunar Magic Path provided!", ConsoleColor.Red);
            else if (!File.Exists(Config.LunarMagicPath))
                Log("Could not find Lunar Magic at the provided path!", ConsoleColor.Red);
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"-ExportSharedPalette \"{Config.OutputPath}\" \"{Config.SharedPalettePath}\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Shared Palette Export Success!", ConsoleColor.Green);
                else
                {
                    Log("Shared Palette Export Failure!", ConsoleColor.Red);
                    return false;
                }
            }

            // export title moves
            Log("Exporting Title Moves...", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.TitleMovesPath))
                Log("No path for Shared Palette provided!", ConsoleColor.Red);
            else if (string.IsNullOrWhiteSpace(Config.LunarMagicPath))
                Log("No Lunar Magic Path provided!", ConsoleColor.Red);
            else if (!File.Exists(Config.LunarMagicPath))
                Log("Could not find Lunar Magic at the provided path!", ConsoleColor.Red);
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"-ExportTitleMoves \"{Config.OutputPath}\" \"{Config.TitleMovesPath}\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Title Moves Export Success!", ConsoleColor.Green);
                else
                {
                    Log("Title Moves Export Failure!", ConsoleColor.Red);
                    return false;
                }
            }

            // save levels
            if (!ExportLevels())
                return false;

            Console.WriteLine();
            return true;
        }

        static private bool ExportLevels()
        {
            Log("Exporting All Levels...", ConsoleColor.Cyan);
            if (string.IsNullOrWhiteSpace(Config.LevelsPath))
                Log("No path for Levels provided!", ConsoleColor.Red);
            else if (string.IsNullOrWhiteSpace(Config.LunarMagicPath))
                Log("No Lunar Magic Path provided!", ConsoleColor.Red);
            else if (!File.Exists(Config.LunarMagicPath))
                Log("Could not find Lunar Magic at the provided path!", ConsoleColor.Red);
            else if (!File.Exists(Config.OutputPath))
                Log("Output ROM does not exist! Build first!", ConsoleColor.Red);
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"-ExportMultLevels \"{Config.OutputPath}\" \"{Config.LevelsPath}{Path.DirectorySeparatorChar}level\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                {
                    Log("Levels Export Success!", ConsoleColor.Green);
                    return true;
                }
                else
                {
                    Log("Levels Export Failure!", ConsoleColor.Red);
                }
            }

            return false;
        }

        static private bool ImportLevels(bool reinsert)
        {
            var romPath = (reinsert ? Config.OutputPath : Config.TempPath);

            Log("Levels", ConsoleColor.Cyan);
            if (reinsert && !File.Exists(romPath))
                Log("Output ROM does not exist! Build first.", ConsoleColor.Red);
            else if (string.IsNullOrWhiteSpace(Config.LevelsPath))
                Log("No path to Levels provided, no levels will be imported.", ConsoleColor.Red);
            else
            {
                // import levels
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                                $"-ImportMultLevels \"{romPath}\" \"{Config.LevelsPath}\"");
                    var p = Process.Start(psi);
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                        Log("Levels Import Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Levels Import Failure!", ConsoleColor.Red);
                        return false;
                    }

                    Console.WriteLine();
                }
            }

            return true;
        }

        static private void Edit()
        {
            if (!File.Exists(Config.OutputPath))
                Error("Output ROM not found - build first!");
            else if (string.IsNullOrWhiteSpace(Config.LunarMagicPath))
                Log("No path to Lunar Magic provided, cannot open built ROM.", ConsoleColor.Red);
            else if (!File.Exists(Config.LunarMagicPath))
                Log("Lunar Magic not found at provided path, cannot open built ROM.", ConsoleColor.Red);
            else
            {
                if (LunarMagicProcess != null && !LunarMagicProcess.HasExited)
                    LunarMagicProcess.Kill(true);

                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"\"{Config.OutputPath}\"");
                LunarMagicProcess = Process.Start(psi);
            }
        }

        static private bool Package()
        {
            Log("Packaging BPS patch...", ConsoleColor.Cyan);

            if (File.Exists(Config.PackagePath))
                File.Delete(Config.PackagePath);

            if (!File.Exists(Config.OutputPath))
                Error("Output ROM not found!");
            else if (string.IsNullOrWhiteSpace(Config.PackagePath))
                Error("Package path not set in config!");
            else if (string.IsNullOrWhiteSpace(Config.CleanPath))
                Error("No clean SMW ROM path set in config!");
            else if (string.IsNullOrWhiteSpace(Config.FlipsPath))
                Error("No path to FLIPS provided in config!");
            else if (!File.Exists(Config.FlipsPath))
                Error("Could not find FLIPS at configured path!");
            else
            {
                var fullCleanPath = Path.GetFullPath(Config.CleanPath);
                var fullOutputPath = Path.GetFullPath(Config.OutputPath);
                var fullPackagePath = Path.GetFullPath(Config.PackagePath);

                if (CreatePatch(fullCleanPath, fullOutputPath, fullPackagePath))
                    Log("Packaging Success!", ConsoleColor.Green);
                else
                {
                    Log("Packaging Failure!", ConsoleColor.Red);
                    return false;
                }
            }

            //Open explorer and select patch file
            if (File.Exists(Config.PackagePath))
                Process.Start("explorer.exe", $"/select, \"{Config.PackagePath}\"");

            Console.WriteLine();

            return true;
        }

        static private void Help()
        {
            Log("Function list:", ConsoleColor.Magenta);
            Log("S - Save", ConsoleColor.Yellow);
            Log("-Exports the global data (overworld, ex global animations, credits, and title screen) to a BPS patch.\n-Export all levels.\nThe ROM must already be built first.\n");

            Log("B - Build", ConsoleColor.Yellow);
            Log("Creates your ROM from scratch, using your provided clean SMW ROM as a base and inserting all the configured patches, graphics, levels, etc.\n");

            Log("R - Run", ConsoleColor.Yellow);
            Log("Loads the previously-built ROM into the configured emulator for testing. The ROM must already be built first.\n");

            Log("T - Test (Save -> Build -> Run)", ConsoleColor.Yellow);
            Log("Executes the above three commands in sequence. Useful to quickly save all your changes and then see them in action.\n");

            Log("E - Edit (in Lunar Magic)", ConsoleColor.Yellow);
            Log("Opens the previously-built ROM in Lunar Magic. The ROM must already be built first.\n");

            Log("P - Package", ConsoleColor.Yellow);
            Log("Creates a BPS patch for your ROM against the configured clean SMW ROM, so that you can share it!\n");
        }

        static private bool ApplyPatch(string cleanROM, string outROM, string patchBPS)
        {
            Log($"Patching {cleanROM}\n\tto {outROM}\n\twith {patchBPS}", ConsoleColor.Yellow);

            Console.ForegroundColor = ConsoleColor.Yellow;
            var psi = new ProcessStartInfo(Config.FlipsPath,
                    $"--apply \"{patchBPS}\" \"{cleanROM}\" \"{outROM}\"");
            var p = Process.Start(psi);
            p.WaitForExit();

            if (p.ExitCode == 0)
                return true;
            else
                return false;
        }

        static private bool CreatePatch(string cleanROM, string hackROM, string outputBPS)
        {
            Log($"Creating Patch {outputBPS}\n\twith {hackROM}\n\tover {cleanROM}", ConsoleColor.Yellow);

            Console.ForegroundColor = ConsoleColor.Yellow;
            var psi = new ProcessStartInfo(Config.FlipsPath,
                    $"--create --bps-delta \"{cleanROM}\" \"{hackROM}\" \"{outputBPS}\"");
            var p = Process.Start(psi);
            p.WaitForExit();

            if (p.ExitCode == 0)
                return true;
            else
                return false;
        }

        static private void Error(string error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {error}\n");
        }

        static private void Log(string msg, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"{msg}");
        }

        static private void Lognl(string msg, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.Write($"{msg}");
        }
    }
}
