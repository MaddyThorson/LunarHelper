using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SMWPatcher
{
    public class Config
    {
        public string WorkingDirectory;

        public string OutputPath;
        public string TempPath;
        public string CleanPath;
        public string PackagePath;

        public string AsarPath;
        public string UberASMPath;
        public string GPSPath;
        public string PixiPath;
        public string AddMusicKPath;
        public string LunarMagicPath;
        public string FlipsPath;

        public string InitialPatch;
        public string LevelsPath;
        public string Map16Path;
        public string SharedPalettePath;
        public string GlobalDataPath;

        public List<string> Patches = new List<string>();

        public string TestLevel;
        public string TestLevelDest;
        public string RetroArchPath;
        public string RetroArchCore;

        #region load

        static public Config Load()
        {
            try
            {
                List<string> data = new List<string>();
                foreach (var file in Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "config*.txt", SearchOption.TopDirectoryOnly))
                    data.Add(File.ReadAllText(file));
                return Load(data);
            }
            catch
            {
                return null;
            }
        }

        static private Config Load(List<string> data)
        {
            Config config = new Config();

            HashSet<string> flags = new HashSet<string>();
            Dictionary<string, string> vars = new Dictionary<string, string>();
            Dictionary<string, List<string>> lists = new Dictionary<string, List<string>>();
            foreach (var d in data)
                Parse(d, flags, vars, lists);

            vars.TryGetValue("dir", out config.WorkingDirectory);
            vars.TryGetValue("output", out config.OutputPath);
            vars.TryGetValue("temp", out config.TempPath);
            vars.TryGetValue("clean", out config.CleanPath);
            vars.TryGetValue("package", out config.PackagePath);
            vars.TryGetValue("asar_path", out config.AsarPath);
            vars.TryGetValue("uberasm_path", out config.UberASMPath);
            vars.TryGetValue("gps_path", out config.GPSPath);
            vars.TryGetValue("pixi_path", out config.PixiPath);
            vars.TryGetValue("addmusick_path", out config.AddMusicKPath);
            vars.TryGetValue("lm_path", out config.LunarMagicPath);
            vars.TryGetValue("flips_path", out config.FlipsPath);
            vars.TryGetValue("levels", out config.LevelsPath);
            vars.TryGetValue("map16", out config.Map16Path);
            vars.TryGetValue("shared_palette", out config.SharedPalettePath);
            vars.TryGetValue("global_data", out config.GlobalDataPath);
            vars.TryGetValue("initial_patch", out config.InitialPatch);
            lists.TryGetValue("patches", out config.Patches);

            vars.TryGetValue("test_level", out config.TestLevel);
            vars.TryGetValue("test_level_dest", out config.TestLevelDest);
            vars.TryGetValue("retroarch_path", out config.RetroArchPath);
            vars.TryGetValue("retroarch_core", out config.RetroArchCore);

            return config;
        }

        static private void Parse(string data, HashSet<string> flags, Dictionary<string, string> vars, Dictionary<string, List<string>> lists)
        {
            var lines = data.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string str = lines[i];
                string peek = null;
                if (i < lines.Length - 1)
                    peek = lines[i + 1];

                if (str.StartsWith("--"))
                {
                    // comment
                }
                else if (str.Contains('='))
                {
                    // var
                    var sp = str.Split('=');
                    if (sp.Length != 2)
                        throw new Exception("Malformed assignment");
                    vars.Add(sp[0].Trim(), sp[1].Trim());
                }
                else if (peek != null && peek.Trim() == "[")
                {
                    // list
                    var list = new List<string>();
                    lists.Add(str.Trim(), list);
                    i += 2;

                    while (true)
                    {
                        if (i >= lines.Length)
                            throw new Exception("Malformed list");

                        str = lines[i];
                        if (str.Trim() == "]")
                            break;
                        else
                            list.Add(str.Trim());

                        i++;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(str))
                {
                    // flag
                    flags.Add(str.Trim());
                }
            }
        }

        #endregion
    }
}
