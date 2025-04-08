using System;
using System.Reflection;
using System.IO;
using WADParser;
using System.Text;
using System.IO.Compression;

namespace Resource_Packer
{
    internal class Program
    {
        private static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }
        }

        static List<string> GarbageFileTypes = new List<string>() {"backup", "dbs", "autosave", "bak"};

        static void Main(string[] args)
        {
            String startDir = "./../../mm8bdm-pk3/";
            String inDir = "./Pack/pk3/";
            String outDir = "./Pack/";
            String tempDir = inDir + "maps/";

            DirectoryInfo startFolder = new DirectoryInfo(@startDir);
            DirectoryInfo inFolder = new DirectoryInfo(@inDir);
            DirectoryInfo outFolder = new DirectoryInfo(@outDir);
            DirectoryInfo mapFolder = new DirectoryInfo(@tempDir);

            Console.WriteLine("Prepping Environment...");

            if(outFolder.Exists)
                outFolder.Delete(true);

            outFolder.Create();

            CopyFilesRecursively(startDir, inDir);

            Console.WriteLine("Files copied. Beginning repacks...");

            // Extract necessary resources from all the wads
            List<FileInfo> embeds = findEmbeddedWads(inFolder);

            foreach (var f in mapFolder.EnumerateFiles())
            {
                bool found = false;
                foreach (string ext in GarbageFileTypes)
                {
                    if (f.Extension.Contains(ext))
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                    continue; // This is a junk file that we want to avoid processing here

                var pd = findPatchDir(new DirectoryInfo(inDir + "patches/maps/"), removeFileExtension(f.Name));
                var tf = new FileInfo(inDir + "TEXTURES.MAPS." + removeFileExtension(f.Name));

                var wad = new WADParserObject();
                wad.Open(f.FullName);

                if (pd != null && pd.Exists)
                {
                    wad.InsertLumpAt("PP_START", "", wad.LumpCount);

                    foreach (var p in pd.EnumerateFiles())
                    {
                        wad.InsertLumpAt(removeFileExtension(p.Name), File.ReadAllBytes(p.FullName), wad.LumpCount);
                    }

                    wad.InsertLumpAt("PP_END", "", wad.LumpCount);
                }

                if (tf.Exists)
                {
                    StreamReader sr = new StreamReader(tf.FullName);
                    wad.InsertLumpAt(tf.Name, sr.ReadToEnd(), wad.LumpCount);
                    sr.Close();
                }

                wad.Write(inFolder.FullName + f.Name);
                tf.Delete();
                Console.WriteLine("- Repacked {0}", f.Name);
            }

            Console.WriteLine("Map wads repacked. Cleaning up...");
            new DirectoryInfo(inDir + "patches/maps/").Delete(true);
            new DirectoryInfo(inDir + "maps/").Delete(true);

            Console.WriteLine("Zipping up pk3...");

            string zipPath = @outDir + "ResourcePack.pk3";
            ZipFile.CreateFromDirectory(inDir, zipPath, CompressionLevel.SmallestSize, false);
            Console.WriteLine("Done.");
        }

        static DirectoryInfo findPatchDir(DirectoryInfo start, string key)
        {
            if (start.Name.StartsWith(key))
            {
                return start;
            }
            else
            {
                foreach (var d in start.EnumerateDirectories())
                {
                    var f = findPatchDir(d, key);

                    if (f != null)
                    {
                        return f;
                    }
                }
                return null;
            }
        }

        static string removeFileExtension(string ins)
        {
            return ins.Substring(0, ins.LastIndexOf("."));
        }

        static void CopyEntriesToFile(HashSet<LumpEntry> lst, WADParserObject wad, String lump, String outFile, String commentName)
        {
            List<LumpEntry> entries = wad.FindLumps(lump);
            if (entries.Count > 0)
            {
                using (StreamWriter sw = new StreamWriter(outFile, true))
                {
                    sw.WriteLine("///// " + commentName + " /////");

                    foreach (var e in entries)
                    {
                        sw.WriteLine(Encoding.Default.GetString(e.Data));
                        lst.Add(e);
                    }

                    sw.WriteLine("\n////////////////////");
                }
            }
        }

        static void CopyEntriesToDir(HashSet<LumpEntry> lst, WADParserObject wad, String prefix, String outFile)
        {
            bool copying = false;

            DirectoryInfo dir = new DirectoryInfo(outFile);

            String nameStart = prefix + "_START";
            // Console.WriteLine("Prefix: {0}, nameStart: {1}", prefix, nameStart);
            nameStart = nameStart.ToUpper();

            String nameEnd = prefix + "_END";
            nameEnd = nameEnd.ToUpper();

            bool fileFound = false;

            for (int i = 0; i < wad.LumpCount; i++)
            {
                var e = wad.GetLumpAt(i);
                var cleanName = cleanString(e.Name);
                var checkE = cleanName.ToUpper();
                // Console.WriteLine("Lump: {0} // Check: {1}", checkE, nameStart);

                if (!copying)
                {
                    if (stringIsSame(checkE, nameStart))
                    {
                        lst.Add(e);
                        // Console.WriteLine("found " + e.Name + ". Starting copy process.");
                        copying = true;
                        if (!fileFound)
                        {
                            fileFound = true;
                            dir.Create();
                        }
                    }
                }
                else
                {
                    // Console.WriteLine(checkE + " vs " + nameEnd);
                    if (stringIsSame(checkE, nameEnd))
                    {
                        lst.Add(e);
                        // Console.WriteLine("found " + e.Name + ". Ending copy process.");
                        copying = false;
                    }
                    else
                    {
                        // Console.WriteLine("copying " + e.Name);
                        ByteArrayToFile(dir.FullName + cleanName + guessFileExtension(e.Data, ".png"), e.Data);
                        lst.Add(e);
                    }
                }
            }
        }

        static string guessFileExtension(byte[] data, string attempt)
        {
            if (attempt.ToLower().Equals(".png"))
            {
                if (data[0] == 137 && data[1] == 'P' && data[2] == 'N' && data[3] == 'G') // PNG header
                {
                    return ".png";
                }
                return "";
            }
            return attempt;
        }

        static string cleanString(string str1)
        {
            return str1.Replace("\0", "");
        }

        static bool stringIsSame(string str1, string str2)
        {
            // Console.WriteLine(str1 + " " + str2);
            // Console.WriteLine(str1.Length + " " + str2.Length);
            if (str1.Length == str2.Length)
            {
                for (int i = 0; i < str1.Length; i++)
                {
                    if (str1[i] != str2[i])
                        return false;
                }
                // Console.WriteLine("Returning true");
                return true;
            }
            return false;
        }

        static bool ByteArrayToFile(string fileName, byte[] byteArray)
        {
            try
            {
                using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(byteArray, 0, byteArray.Length);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught. Filename: {0} // Bytes: {1}", fileName, stringToByteString(fileName));
                return false;
            }
        }

        static string stringToByteString(string str1)
        {
            String ret = "[";
            for (int i = 0; i < str1.Count(); i++)
            {
                if (i > 0)
                    ret += ", ";
                ret += (int)str1[i];
            }
            return ret + "]";
        }

        static List<FileInfo> findEmbeddedWads(DirectoryInfo inDir)
        {
            List<FileInfo> ret = new List<FileInfo>();

            var dirs = inDir.EnumerateFiles();

            foreach (var dir in dirs)
            {
                if (dir.FullName.ToLower().Contains(".wad"))
                {
                    ret.Add(dir);
                }
            }

            return ret;
        }

        static String getMapFirstName(WADParserObject wad, String fileName)
        {
            var e = wad.FindLumps("TEXTMAP");
            if (e.Count > 0 && e[0].Index > 0)
            {
                return cleanString(wad.GetLumpAt(e[0].Index - 1).Name);
            }
            return fileName;
        }
    }
}
