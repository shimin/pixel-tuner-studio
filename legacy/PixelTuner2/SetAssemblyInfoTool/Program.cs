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
        /// первый аргумент - базовая директория, второй - версия которую надо проставить, строка вида х.х.х.ххх
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            if (args.Length == 2)
            {
                string basePath = args[0] as string;
                string version = args[1] as string;
                try
                {
                    Version v;
                    if (Version.TryParse(version, out v))
                    {
                        if (Directory.Exists(basePath))
                        {
                            if (basePath.StartsWith(".") || basePath.StartsWith("\\"))
                                basePath = Path.Combine(System.Reflection.Assembly.GetExecutingAssembly().Location, basePath);
                            string[] paths = Directory.GetFiles(basePath, "AssemblyInfo.cs", SearchOption.AllDirectories);
                            string[] wxsPaths = Directory.GetFiles(basePath, "*.wxs", SearchOption.AllDirectories);

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
                            return;
                        }
                    }
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
            WriteHelp();
        }

        private static void WriteHelp()
        {
            Console.WriteLine("Invalid command line parameters.");
            Console.WriteLine("There are expected ones:");
            Console.WriteLine("\t1. path to base directory to look for AssemblyInfo.cs files");
            Console.WriteLine("\t2. Assembly version fasioned like: 0.0.0.000");
        }
    }
}
