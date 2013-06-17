using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace SetAssemblyInfoTool
{
    class Program
    {
        /// <summary>
        /// первый аргумент - базовая директория, второй - версия которую надо проставить, строка вида х.х.х.ххх, 
        /// третий (необязательно) - функция определяющая поведение
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            Version v = new Version();
            string basePath = "";

            if (args.Length >= 2)
            {
                basePath = args[0] as string;
                string version = args[1] as string;
            
                if (Version.TryParse(version, out v))
                {
                    if (Directory.Exists(basePath))
                    {
                        if (basePath.StartsWith(".") || basePath.StartsWith("\\"))
                            basePath = Path.Combine(System.Reflection.Assembly.GetExecutingAssembly().Location, basePath);
                    }
                }
            }

            if (args.Length == 2)
            {
                UsualOperations(v, basePath);
                return;
            }
            else if (args.Length == 3)
            {
                switch (args[2].ToLower())
                {
                    case "autoupdaterservice": GenerateUpdateServiceData(v, basePath); break;
                    case "autoupdaterconfig": GenerateUpdateConfig(v, basePath); break;
                    default: UsualOperations(v, basePath); break;
                }
            }
            else
            {
                WriteHelp();
            }
        }

        private static void GenerateUpdateServiceData(Version v, string basePath)
        {
            throw new NotImplementedException();
        }

        private static void GenerateUpdateConfig(Version v, string basePath)
        {
            try
            {
                string[] paths = Directory.GetFiles(basePath, "Autoupdater.config", SearchOption.AllDirectories);
                foreach (string path in paths)
                {
                    Console.WriteLine("Autoupdater.config: " + path);
                    List<string> lines = new List<string>();
                    using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
                    {
                        using (StreamReader reader = new StreamReader(fs))
                        {
                            string content = reader.ReadLine();
                            while (content != null)
                            {
                                lines.Add(content);
                                content = reader.ReadLine();
                            }
                        }
                    }

                    using (FileStream fs = new FileStream(path, FileMode.Create))
                    {
                        using (StreamWriter sw = new StreamWriter(fs))
                        {
                            foreach (string s in lines)
                            {
                                string toWrite = s;
                                Regex rxAssemblyVersion = new Regex("lastver=\"[0-9.]+\"");
                                if (rxAssemblyVersion.IsMatch(s))
                                    toWrite = rxAssemblyVersion.Replace(s, String.Format("lastver=\"{0}\"", v.ToString()));
                                sw.WriteLine(toWrite);
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static void UsualOperations(Version v, string basePath)
        {
            try
            {
                string[] paths = Directory.GetFiles(basePath, "AssemblyInfo.cs", SearchOption.AllDirectories);
                string[] wxsPaths = Directory.GetFiles(basePath, "*.wxs", SearchOption.AllDirectories);

                CorrectAssemblyFiles(v.ToString(), paths);
                CorrectWxsFiles(v.ToString(), wxsPaths);
            }
            catch (Exception ex)
            {
                string p = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string dirName = p.Substring(0, p.LastIndexOf("\\"));
                using (FileStream fs = new FileStream(Path.Combine(dirName, "log.txt"), FileMode.Create))
                {
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        sw.WriteLine(ex.Message);
                    }
                }
            }
        }

        private static void CorrectAssemblyFiles(string version, string[] paths)
        {
            foreach (string path in paths)
            {
                Console.WriteLine("AssemblyInfo: " + path);
                List<string> lines = new List<string>();
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
                {
                    using (StreamReader reader = new StreamReader(fs))
                    {
                        string content = reader.ReadLine();
                        while (content != null)
                        {
                            lines.Add(content);
                            content = reader.ReadLine();
                        }
                    }
                }

                using (FileStream fs = new FileStream(path, FileMode.Create))
                {
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        foreach (string s in lines)
                        {
                            string toWrite = s;
                            Regex rxAssemblyVersion = new Regex("\\[assembly: AssemblyVersion\\(\"[0-9.]+\"\\)\\]");
                            if (rxAssemblyVersion.IsMatch(s))
                                toWrite = rxAssemblyVersion.Replace(s, String.Format("[assembly: AssemblyVersion(\"{0}\")]", version));

                            Regex q = new Regex("\\[assembly: AssemblyFileVersion\\(\"[0-9.]+\"\\)\\]");
                            if (q.IsMatch(s))
                                toWrite = q.Replace(s, String.Format("[assembly: AssemblyFileVersion(\"{0}\")]", version));

                            sw.WriteLine(toWrite);
                        }
                    }
                }
            }
        }

        private static void CorrectWxsFiles(string version, string[] wxsPaths)
        {
            foreach (string path in wxsPaths)
            {
                Console.WriteLine("wxs: " + path);
                List<string> lines = new List<string>();
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
                {
                    using (StreamReader reader = new StreamReader(fs))
                    {
                        string content = reader.ReadLine();
                        while (content != null)
                        {
                            lines.Add(content);
                            content = reader.ReadLine();
                        }
                    }
                }

                using (FileStream fs = new FileStream(path, FileMode.Create))
                {
                    using (StreamWriter sw = new StreamWriter(fs))
                    {
                        foreach (string s in lines)
                        {
                            string toWrite = s;
                            Regex rxAssemblyVersion = new Regex("<\\?define ProductVersion=\"[0-9.]+\" \\?>");
                            if (rxAssemblyVersion.IsMatch(s.TrimStart()))
                                toWrite = String.Format("  <?define ProductVersion=\"{0}\" ?>", version);

                            sw.WriteLine(toWrite);
                        }
                    }
                }
            }
        }

        private static void WriteHelp()
        {
            Console.WriteLine("Invalid command line parameters.");
            Console.WriteLine("There are expected ones:");
            Console.WriteLine("\t1. path to base directory to look for AssemblyInfo.cs files");
            Console.WriteLine("\t2. Assembly version fasioned like: 0.0.0.000");
            Console.WriteLine("\t3. Behaviour specifier:");
            Console.WriteLine("\t\t * autoupdaterservice - generates autoupdaterservice.xml");
            Console.WriteLine("\t\t * autoupdaterconfig - replaces versions in all found autoupdaterconfig.conf");
        }
    }
}
