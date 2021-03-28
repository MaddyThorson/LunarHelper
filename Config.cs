using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SMWPatcher
{
    [Serializable]
    public class Config
    {
        public String InputPath;
        public String OutputPath;
        public String TempPath;
        public String WorkingDirectory;

        public String AsarPath;
        public String GPSPath;
        public String AddMusicKPath;
        public String LunarMagicPath;
        public String LevelsPath;
        public String TestLevel;
        public String TestLevelDest;
        public String Map16Path;

        public List<String> Patches = new List<string>();

        #region load

        static private readonly String FilePath = "config.txt";
        static public bool Exists => File.Exists(FilePath);

        static public Config Load()
        {
            try
            {
                return Load(File.ReadAllText(FilePath));
            }
            catch
            {
                return null;
            }
        }

        static private Config Load(String data)
        {
            Config config = new Config();

            Dictionary<string, string> vars = new Dictionary<string, string>();
            Dictionary<string, List<string>> lists = new Dictionary<string, List<string>>();

            #region parse

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
            }

            #endregion

            vars.TryGetValue("dir", out config.WorkingDirectory);
            vars.TryGetValue("input", out config.InputPath);
            vars.TryGetValue("output", out config.OutputPath);
            vars.TryGetValue("temp", out config.TempPath);
            vars.TryGetValue("asar_path", out config.AsarPath);
            vars.TryGetValue("gps_path", out config.GPSPath);
            vars.TryGetValue("addmusick_path", out config.AddMusicKPath);
            vars.TryGetValue("lm_path", out config.LunarMagicPath);
            vars.TryGetValue("levels", out config.LevelsPath);
            vars.TryGetValue("test_level", out config.TestLevel);
            vars.TryGetValue("test_level_dest", out config.TestLevelDest);
            vars.TryGetValue("map16", out config.Map16Path);
            lists.TryGetValue("patches", out config.Patches);

            return config;
        }

        #endregion
    }
}
