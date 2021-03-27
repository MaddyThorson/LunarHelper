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

        static void Main(string[] args)
        {
            XmlSerializer xs = new XmlSerializer(typeof(Config));

            // load config
            Config = Config.Load();
            if (Config == null)
            {
                Error("Could not open config.txt");
                return;
            }

            // set the working directory
            if (!String.IsNullOrWhiteSpace(Config.WorkingDirectory))
                Directory.SetCurrentDirectory(Config.WorkingDirectory);

            // some error checks
            if (String.IsNullOrWhiteSpace(Config.InputPath))
            {
                Error("No Input ROM path provided!");
                return;
            }
            else if (!File.Exists(Config.InputPath))
            {
                Error($"Input ROM file '{Config.InputPath}' does not exist!");
                return;
            }
            else if (String.IsNullOrWhiteSpace(Config.OutputPath))
            {
                Error("No Output ROM path provided!");
                return;
            }
            else if (String.IsNullOrWhiteSpace(Config.TempPath))
            {
                Error("No Temp ROM path provided!");
                return;
            }

            DoIt();

            Console.ForegroundColor = ConsoleColor.White;
            Console.ReadLine();
        }

        static private bool DoIt()
        {
            // create temp ROM to operate on, in case something goes wrong
            if (File.Exists(Config.TempPath))
                File.Delete(Config.TempPath);
            File.Copy(Config.InputPath, Config.TempPath);

            // apply asar patches
            Log("1 - Patches", ConsoleColor.Cyan);
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

                    ProcessStartInfo psi = new ProcessStartInfo(Config.AsarPath, $"{patch} {Config.TempPath}");
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

            // run AddMusicK
            Log("2 - AddMusicK", ConsoleColor.Cyan);
            if (String.IsNullOrWhiteSpace(Config.AddMusicKPath))
                Log("No path to AddMusicK provided, no music will be inserted.", ConsoleColor.Red);
            else if (!File.Exists(Config.AddMusicKPath))
                Log("AddMusicK not found at provided path, no music will be inserted.", ConsoleColor.Red);
            else
            {
                var dir = Path.GetFullPath(Path.GetDirectoryName(Config.AddMusicKPath));
                var exe = Path.GetFileName(Config.AddMusicKPath);
                var rom = Path.GetRelativePath(dir, Path.GetFullPath(Config.TempPath));
                Console.ForegroundColor = ConsoleColor.Gray;

                ProcessStartInfo psi = new ProcessStartInfo(Config.AddMusicKPath, rom);
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

            // import gfx
            Log("3 - Graphics", ConsoleColor.Cyan);
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

            // import levels
            Log("4 - Levels", ConsoleColor.Cyan);
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

                // import test level
                if (!String.IsNullOrWhiteSpace(Config.TestLevel) && !String.IsNullOrWhiteSpace(Config.TestLevelDest))
                {
                    Log("4b - Test Level", ConsoleColor.Cyan);
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

                        // test level TODO
                        Log($"Importing level {Config.TestLevel} to {Config.TestLevelDest} for testing...  ", ConsoleColor.Yellow);

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        ProcessStartInfo psi = new ProcessStartInfo(Config.LunarMagicPath,
                            $"-ImportLevel {Config.TempPath} \"{path}\" {Config.TestLevelDest}");
                        var p = Process.Start(psi);
                        p.WaitForExit();

                        if (p.ExitCode == 0)
                            Log("Test Level Import Success!", ConsoleColor.Green);
                        else
                        {
                            Log("Test Level Import Failure!", ConsoleColor.Red);
                            return false;
                        }

                        Console.WriteLine();
                    }
                }                
            }

            // import map16
            Log("5 - Map16", ConsoleColor.Cyan);
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

            // output final ROM
            if (File.Exists(Config.OutputPath))
                File.Delete(Config.OutputPath);
            File.Move(Config.TempPath, Config.OutputPath);

            Log($"ROM patched successfully to '{Config.OutputPath}'!", ConsoleColor.Cyan);
            Log("Have a nice day :)", ConsoleColor.Cyan);

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
