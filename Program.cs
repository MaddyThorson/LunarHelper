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

        static void Main(string[] args)
        {
            bool running = true;
            while (running)
            {
                Log("Welcome to Lunar Helper ^_^", ConsoleColor.Cyan);
                Log("B - Build, T - Build and Test, O - Test Only, P - Package, ESC - Exit");
                Console.WriteLine();

                var key = Console.ReadKey(true);

                switch (key.Key)
                {
                    case ConsoleKey.B:
                        if (Init())
                            Build();
                        break;

                    case ConsoleKey.T:
                        if (Init() && Build())
                            Test();
                        break;

                    case ConsoleKey.O:
                        if (Init())
                            Test();
                        break;

                    case ConsoleKey.P:
                        if (Init() && Build())
                            Package();
                        break;

                    case ConsoleKey.Escape:
                        running = false;
                        Log("Have a nice day!", ConsoleColor.Cyan);
                        Console.ForegroundColor = ConsoleColor.White;
                        break;

                    default:
                        Log("Key not recognized!!", ConsoleColor.Red);
                        Console.WriteLine();
                        break;
                }
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
            if (!String.IsNullOrWhiteSpace(Config.WorkingDirectory))
                Directory.SetCurrentDirectory(Config.WorkingDirectory);

            // some error checks
            if (String.IsNullOrWhiteSpace(Config.InputPath))
            {
                Error("No Input ROM path provided!");
                return false;
            }
            else if (!File.Exists(Config.InputPath))
            {
                Error($"Input ROM file '{Config.InputPath}' does not exist!");
                return false;
            }
            else if (String.IsNullOrWhiteSpace(Config.OutputPath))
            {
                Error("No Output ROM path provided!");
                return false;
            }
            else if (String.IsNullOrWhiteSpace(Config.TempPath))
            {
                Error("No Temp ROM path provided!");
                return false;
            }

            return true;
        }

        static private bool Build()
        {
            // create temp ROM to operate on, in case something goes wrong
            if (File.Exists(Config.TempPath))
                File.Delete(Config.TempPath);
            File.Copy(Config.InputPath, Config.TempPath);

            // import gfx
            Log("Graphics", ConsoleColor.Cyan);
            if (String.IsNullOrWhiteSpace(Config.LunarMagicPath))
                Log("No path to Lunar Magic provided, no graphics will be imported.", ConsoleColor.Red);
            else if (!File.Exists(Config.LunarMagicPath))
                Log("Lunar Magic not found at provided path, no graphics will be imported.", ConsoleColor.Red);
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"-ImportAllGraphics {Config.TempPath}");
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
            if (String.IsNullOrWhiteSpace(Config.Map16Path))
                Log("No path to Levels provided, no map16 will be imported.", ConsoleColor.Red);
            else if (String.IsNullOrWhiteSpace(Config.LunarMagicPath))
                Log("No path to Lunar Magic provided, no map16 will be imported.", ConsoleColor.Red);
            else if (!File.Exists(Config.LunarMagicPath))
                Log("Lunar Magic not found at provided path, no map16 will be imported.", ConsoleColor.Red);
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"-ImportAllMap16 {Config.TempPath} {Config.Map16Path}");
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

            // import overworld
            Log("Overworld", ConsoleColor.Cyan);
            if (String.IsNullOrWhiteSpace(Config.OverworldPath))
                Log("No path to Overworld ROM provided, no overworld will be imported.", ConsoleColor.Red);
            else if (String.IsNullOrWhiteSpace(Config.LunarMagicPath))
                Log("No path to Lunar Magic provided, no overworld will be imported.", ConsoleColor.Red);
            else if (!File.Exists(Config.LunarMagicPath))
                Log("Lunar Magic not found at provided path, no overworld will be imported.", ConsoleColor.Red);
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"-TransferOverworld {Config.TempPath} {Config.OverworldPath}");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Overworld Import Success!", ConsoleColor.Green);
                else
                {
                    Log("Overworld Import Failure!", ConsoleColor.Red);
                    return false;
                }

                Console.WriteLine();
            }

            // import levels
            Log("Levels", ConsoleColor.Cyan);
            if (String.IsNullOrWhiteSpace(Config.LevelsPath))
                Log("No path to Levels provided, no levels will be imported.", ConsoleColor.Red);
            else if (String.IsNullOrWhiteSpace(Config.LunarMagicPath))
                Log("No path to Lunar Magic provided, no levels will be imported.", ConsoleColor.Red);
            else if (!File.Exists(Config.LunarMagicPath))
                Log("Lunar Magic not found at provided path, no levels will be imported.", ConsoleColor.Red);
            else
            {
                // import levels
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                                $"-ImportMultLevels {Config.TempPath} {Config.LevelsPath}");
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

            // apply asar patches
            Log("Patches", ConsoleColor.Cyan);
            if (String.IsNullOrWhiteSpace(Config.AsarPath))
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
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;

                    var p = Process.Start(psi);
                    p.WaitForExit();

                    if (p.ExitCode == 0)
                        Log("Success!", ConsoleColor.Green);
                    else
                    {
                        Log("Failure!", ConsoleColor.Red);
                        Error(p.StandardError.ReadToEnd());
                        return false;
                    }
                }

                Log("Patching Success!", ConsoleColor.Green);
                Console.WriteLine();
            }

            // run GPS
            Log("GPS", ConsoleColor.Cyan);
            if (String.IsNullOrWhiteSpace(Config.GPSPath))
                Log("No path to GPS provided, no music will be inserted.", ConsoleColor.Red);
            else if (!File.Exists(Config.GPSPath))
                Log("GPS not found at provided path, no music will be inserted.", ConsoleColor.Red);
            else
            {
                var dir = Path.GetFullPath(Path.GetDirectoryName(Config.GPSPath));
                var rom = Path.GetRelativePath(dir, Path.GetFullPath(Config.TempPath));

                ProcessStartInfo psi = new ProcessStartInfo(Config.GPSPath, $"-l \"{dir}/list.txt\" {rom}");
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.WorkingDirectory = dir;

                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("GPS Success!", ConsoleColor.Green);
                else
                {
                    Log("GPS Failure!", ConsoleColor.Red);
                    //Error(p.StandardError.ReadToEnd());
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
                Console.ForegroundColor = ConsoleColor.Gray;

                // pixi is a weird little tool and we need to specify the list path
                ProcessStartInfo psi = new ProcessStartInfo(Config.PixiPath, $"-l \"{list}\" \"{Config.TempPath}\"")
                {
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var p = Process.Start(psi);
                while (!p.HasExited)
                    p.StandardInput.Write('a');

                if (p.ExitCode == 0)
                    Log("Pixi Success!", ConsoleColor.Green);
                else
                {
                    Log("Pixi Failure!", ConsoleColor.Red);
                    Error(p.StandardOutput.ReadToEnd());
                    return false;
                }

                Console.WriteLine();
            }

            // run AddMusicK
            Log("AddMusicK", ConsoleColor.Cyan);
            if (String.IsNullOrWhiteSpace(Config.AddMusicKPath))
                Log("No path to AddMusicK provided, no music will be inserted.", ConsoleColor.Red);
            else if (!File.Exists(Config.AddMusicKPath))
                Log("AddMusicK not found at provided path, no music will be inserted.", ConsoleColor.Red);
            else
            {
                var dir = Path.GetFullPath(Path.GetDirectoryName(Config.AddMusicKPath));
                var rom = Path.GetRelativePath(dir, Path.GetFullPath(Config.TempPath));
                Console.ForegroundColor = ConsoleColor.Gray;

                ProcessStartInfo psi = new ProcessStartInfo(Config.AddMusicKPath, $"\"{rom}\"");
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.WorkingDirectory = dir;

                var p = Process.Start(psi);
                while (!p.HasExited)
                    p.StandardInput.Write('a');

                if (p.ExitCode == 0)
                    Log("AddMusicK Success!", ConsoleColor.Green);
                else
                {
                    Log("AddMusicK Failure!", ConsoleColor.Red);
                    Error(p.StandardError.ReadToEnd());
                    return false;
                }

                Console.WriteLine();
            }

            // output final ROM
            if (File.Exists(Config.OutputPath))
                File.Delete(Config.OutputPath);
            File.Move(Config.TempPath, Config.OutputPath);

            Log($"ROM patched successfully to '{Config.OutputPath}'!", ConsoleColor.Cyan);
            Console.WriteLine();

            return true;
        }

        static private bool Test()
        {
            Console.WriteLine();
            Log("Initiating Test routine!", ConsoleColor.Magenta);

            // test level
            if (!String.IsNullOrWhiteSpace(Config.TestLevel) && !String.IsNullOrWhiteSpace(Config.TestLevelDest))
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
                        $"-ImportLevel {Config.OutputPath} \"{path}\" {Config.TestLevelDest}");
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

                // retroarch
                if (!String.IsNullOrWhiteSpace(Config.RetroArchPath))
                {
                    Log("Launching RetroArch...", ConsoleColor.Yellow);
                    var fullRom = Path.GetFullPath(Config.OutputPath);

                    if (RetroArchProcess != null && !RetroArchProcess.HasExited)
                        RetroArchProcess.Kill(true);

                    ProcessStartInfo psi = new ProcessStartInfo(Config.RetroArchPath,
                        $"-L \"{Config.RetroArchCore}\" \"{fullRom}\"");
                    RetroArchProcess = Process.Start(psi);
                }
            }

            Log("Test routine complete!", ConsoleColor.Magenta);
            Console.WriteLine();

            return true;
        }

        static private bool Package()
        {
            Log("Packaging BPS patch...", ConsoleColor.Cyan);

            if (File.Exists(Config.PackagePath))
                File.Delete(Config.PackagePath);

            if (!File.Exists(Config.OutputPath))
                Error("Output ROM not found!");
            else if (String.IsNullOrWhiteSpace(Config.PackagePath))
                Error("Package path not set in config!");
            else if (String.IsNullOrWhiteSpace(Config.CleanPath))
                Error("No clean SMW ROM path set in config!");
            else if (String.IsNullOrWhiteSpace(Config.FlipsPath))
                Error("No path to FLIPS provided in config!");
            else if (!File.Exists(Config.FlipsPath))
                Error("Could not find FLIPS at configured path!");
            else
            {
                var fullCleanPath = Path.GetFullPath(Config.CleanPath);
                var fullOutputPath = Path.GetFullPath(Config.OutputPath);
                var fullPackagePath = Path.GetFullPath(Config.PackagePath);

                ProcessStartInfo psi = new ProcessStartInfo(Config.FlipsPath,
                        $"--create --bps-delta \"{fullCleanPath}\" \"{fullOutputPath}\" \"{fullPackagePath}\"");
                var p = Process.Start(psi);
                p.WaitForExit();

                if (p.ExitCode == 0)
                    Log("Patch Creation Success!", ConsoleColor.Green);
                else
                {
                    Log("Patch Creation Failure!", ConsoleColor.Red);
                    return false;
                }
            }

            Console.WriteLine();

            return true;
        }

        static private void Error(String error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {error}");
        }

        static private void Log(String msg, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"{msg}");
        }

        static private void Lognl(String msg, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            Console.Write($"{msg}");
        }
    }
}
