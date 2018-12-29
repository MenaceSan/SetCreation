using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

// D:\Linda\Pictures -verbose -usedirdate 

namespace SetCreation
{
    internal class Stats
    {
        // similar to :

        internal string RootDir;
        internal bool UseDirDate = false;       // set the date on a file via the dir name.
        internal bool Verbose = true;         // Print the class/methods i find.

        internal DateTime DirDate;

        public static readonly string[] _dirsEx = { "bin", "obj", "packages" };

        private static bool IsDirDate(int year)
        {
            if (year <= 1900 || year > 2020)
                return false;
            return true;
        }
        private static bool IsDirDate(DateTime dt)
        {
            return IsDirDate(dt.Year);
        }

        private static DateTime GetDirDate(string dirName)
        {
            // set DirDate
            if (!char.IsDigit(dirName[0]))
                return DateTime.MinValue;

            // find a date.
            int[] dts = new int[3] { 1, 1, 1 };
            int part = 0;
            int val = 0;

            for (int i = 0; i < dirName.Length; i++)
            {
                char ch = dirName[i];
                if (char.IsDigit(ch))
                {
                    val = val * 10 + (ch - '0');
                }
                else if (val > 0)
                {
                    dts[part] = val;
                    val = 0;
                    if (++part > 2)
                        break;
                }
            }

            if (val > 0)
            {
                dts[part] = val;
            }

            if (!IsDirDate(dts[0]))
                return DateTime.MinValue;
            if (dts[1] < 1 || dts[1] > 12)
                dts[1] = 1;
            if (dts[2] < 1 || dts[2] > 31)
                return DateTime.MinValue;

            return new DateTime(dts[0], dts[1], dts[2]);  // got a date.
        }

        public static readonly string[] _image_exts = { ".bmp", ".gif", ".png", ".jpg", ".jpeg" };     // what file types do we read?
        public const int kYearLegit = 2015;

        public void SetDirDateImage(string path, DateTime fileDate)
        {
            // this may throw

            try
            {
                string ext = Path.GetExtension(path);
                if (!_image_exts.Contains(ext))    // ignore this
                    return;

                string new_path;

                using (Image theImage = new Bitmap(path))
                {
                    PropertyItem[] propItems = theImage.PropertyItems;
                    var DataTakenProperty1 = propItems.Where(a => a.Id.ToString("x") == "9004").FirstOrDefault();
                    var DataTakenProperty2 = propItems.Where(a => a.Id.ToString("x") == "9003").FirstOrDefault();

                    Encoding _Encoding = Encoding.UTF8;
                    string originalDateString = null;
                    if (DataTakenProperty1 != null)
                        originalDateString = _Encoding.GetString(DataTakenProperty1.Value);
                    if (originalDateString == null && DataTakenProperty2 != null)
                        originalDateString = _Encoding.GetString(DataTakenProperty2.Value);

                    if (originalDateString != null && originalDateString.Length > 4)
                    {
                        originalDateString = originalDateString.Remove(originalDateString.Length - 1);
                        DateTime originalDate;
                        if (DateTime.TryParseExact(originalDateString, "yyyy:MM:dd HH:mm:ss", null, 
                            System.Globalization.DateTimeStyles.None, out originalDate))
                        {
                            //if (originalDate.Year < kYearLegit)
                            //    return;
                            if (originalDate == fileDate)
                                return;
                        }
                    }

                    // originalDate = originalDate.AddHours(-7);
                    bool change = false;
                    if (DataTakenProperty1 != null)
                    {
                        DataTakenProperty1.Value = _Encoding.GetBytes(fileDate.ToString("yyyy:MM:dd HH:mm:ss") + '\0');
                        theImage.SetPropertyItem(DataTakenProperty1);
                        change = true;
                    }
                    if (DataTakenProperty2 != null)
                    {
                        DataTakenProperty2.Value = _Encoding.GetBytes(fileDate.ToString("yyyy:MM:dd HH:mm:ss") + '\0');
                        theImage.SetPropertyItem(DataTakenProperty2);
                        change = true;
                    }

                    if (!change)
                        return;

                    new_path = System.IO.Path.GetDirectoryName(path) + "\\_" + System.IO.Path.GetFileName(path);
                    theImage.Save(new_path);    // dont want to recompress!
                }

                // rename 
                System.IO.File.Delete(path);
                File.Move(new_path, path);
            }
            catch
            {
            }
        }


        private void SetDirDate(FileInfo file)
        {
            // use DirDate
            if (file.Name.StartsWith("."))
                return;
            if (file.CreationTime.Year < kYearLegit)  // just leave it ?
                return;
            if (!IsDirDate(this.DirDate))
                return;

            DateTime fileDate = GetDirDate(file.Name);      // file has its own year.
            if (!IsDirDate(fileDate))
                fileDate = this.DirDate;

            if (file.CreationTime == fileDate)
                return;

            // re-label file.
            file.CreationTime = fileDate;
            SetDirDateImage(file.FullName, fileDate);
            File.SetCreationTime(file.FullName, fileDate);
            // File.SetLastWriteTime(file.FullName, fileDate);
        }

        public void ReadDir(string dirPath)
        {
            // Recursive dir reader.

            var d = new DirectoryInfo(dirPath);     //  Assuming Test is your Folder

            bool showDir = false;   // only show dir if it has files.

            // deal with files first.
            int filesInDir = 0;
            var Files = d.GetFiles("*.*");      // Getting all files
            foreach (FileInfo file in Files)
            {
                if (file.Attributes.HasFlag(FileAttributes.Hidden) && file.Attributes.HasFlag(FileAttributes.System)) // e.g. Thumb.db
                    continue;

                if (this.Verbose && !showDir)
                {
                    Console.WriteLine($"Dir: {dirPath.Substring(RootDir.Length)}");
                    showDir = true;
                }

                filesInDir++;
                if (this.UseDirDate)
                {
                    SetDirDate(file);
                }
                else
                {
                }
            }

            // Recurse into dirs.
            var Dirs = d.GetDirectories();
            foreach (var dir in Dirs)
            {
                if (_dirsEx.Contains(dir.Name))     // excluded dir
                    continue;
                if (dir.Name.StartsWith("."))       // hidden
                    continue;
                if (this.UseDirDate)
                {
                    // what is the dir name? 
                    DateTime dt = GetDirDate(dir.Name);
                    if (!IsDirDate(dt))
                        continue;
                    DirDate = dt;
                }
                ReadDir(dir.FullName);
            }
        }
    }

    public class Program
    {
        // The main entry point.

        static void Main(string[] args)
        {
            // Main entry point.

            Console.WriteLine("SetCreation v1");

            bool waitOnDone = false;
            var stats = new Stats();
            foreach (string arg in args)
            {
                if (string.IsNullOrWhiteSpace(arg))
                    continue;

                string argL = arg.ToLower();

                if (argL == "-help" || argL == "-?")
                {
                    Console.WriteLine("SetCreation walks a directory of .cs sources and compiles some statistics.");
                    Console.WriteLine("Use: SetCreation -flag directory");
                    Console.WriteLine("-wait");
                    Console.WriteLine("-usedirdate");
                    Console.WriteLine("-verbose");
                    return;
                }
                if (argL == "-wait")
                {
                    waitOnDone = true;
                    continue;
                }
                if (argL == "-usedirdate")
                {
                    stats.UseDirDate = true;
                    continue;
                }
                
                if (argL == "-verbose")
                {
                    stats.Verbose = true;
                    continue;
                }
                if (argL.StartsWith("-"))
                {
                    Console.WriteLine("Bad Arg");
                    return;
                }
                stats.RootDir = arg;
            }
            if (string.IsNullOrWhiteSpace(stats.RootDir))
                stats.RootDir = Environment.CurrentDirectory;       // just use current dir.

            Console.WriteLine($"Read Dir '{stats.RootDir}'.");

            stats.ReadDir(stats.RootDir);
 
            if (waitOnDone)
            {
                Console.WriteLine("Press Enter to Continue");
                Console.ReadKey();
            }
        }
    }
}
