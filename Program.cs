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

                var pd = findPatchDir(new DirectoryInfo(inDir + "patches/maps/"), Path.GetFileNameWithoutExtension(f.Name));
                var tf = new FileInfo(inDir + "TEXTURES.MAPS." + Path.GetFileNameWithoutExtension(f.Name));

                var wad = new WADParserObject();
                wad.Open(f.FullName);

                if (pd != null && pd.Exists)
                {
                    wad.InsertLumpAt("PP_START", "", wad.LumpCount);

                    foreach (var p in pd.EnumerateFiles())
                    {
                        wad.InsertLumpAt(Path.GetFileNameWithoutExtension(p.Name), File.ReadAllBytes(p.FullName), wad.LumpCount);
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
            inFolder.Delete(true);
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

        static List<FileInfo> findEmbeddedWads(DirectoryInfo inDir)
        {
            List<FileInfo> ret = new List<FileInfo>();

            var dirs = inDir.EnumerateFiles();

            foreach (var dir in dirs)
            {
                if (dir.Extension.ToLower().Contains(".wad"))
                {
                    ret.Add(dir);
                }
            }

            return ret;
        }
    }
}
