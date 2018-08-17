using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIAudioSlicer
{
    public class AudioUtility
    {
        string path = @"C:\audio\";
        public async Task go(string[] args)
        {
            int abCount = 100;
            int abcCount = 50;
            if (args.Length > 0)
            {
                path = args[0];
                if (path[path.Length - 1] != '\\' && path[path.Length - 1] != '/')
                {
                    path = path + "\\";
                }
                if (!Directory.Exists(path))
                {
                    Console.WriteLine("Specified directory does not exist");
                    return;
                }
            }
            if (args.Length > 1)
            {
                abCount = int.Parse(args[1]);
            }
            if (args.Length > 2)
            {
                abcCount = int.Parse(args[2]);
            }
            if (!Directory.Exists(path))
            {
                Console.WriteLine("Usage: ./aislicer.exe \"C:\\audio\" 400 200");
                Console.WriteLine("(Working directory) (2 clip merge count) (3 clip merge count)");
                Console.WriteLine("You must specify a root directory or create \"C:\\audio\\\"");
                return;
            }
            if (!Directory.Exists(path+"inputs"))
            {
                Console.WriteLine("Specified directory does not contain \"inputs\" directory.");
                return;
            }

            var Files = getFilesForDir(path + "inputs", ".mp3");
            Files = Files.Concat(getFilesForDir(path + "inputs", ".wav")).ToList();
            if (Files.Count == 0)
            {
                Console.WriteLine("There are no .mp3 or .wav files in the \"inputs\" directory");
                return;
            }

            if (!Directory.Exists(path + "converted"))
            {
                DirectoryInfo di = Directory.CreateDirectory(path + "converted");
            }
            if (!Directory.Exists(path + "mono_audio"))
            {
                DirectoryInfo di = Directory.CreateDirectory(path + "mono_audio");
            }
            if (!Directory.Exists(path + "ten_second_clips"))
            {
                DirectoryInfo di = Directory.CreateDirectory(path + "ten_second_clips");
            }
            if (!Directory.Exists(path + "AB"))
            {
                DirectoryInfo di = Directory.CreateDirectory(path + "AB");
            }
            if (!Directory.Exists(path + "ABC"))
            {
                DirectoryInfo di = Directory.CreateDirectory(path + "ABC");
            }

            string[] a = await Task.WhenAll(Files.Select(file =>
            {
                  string arguments = String.Format("\"{0}inputs\\{1}\" -r 16000 \"{0}converted\\{2}.wav\"", 
                      path, 
                      file.Name, 
                      Path.GetFileNameWithoutExtension(file.Name), 
                      file.Extension
                      );
                  return RunProcessAsync(arguments, false);
            }));

            var Files1 = getFilesForDir(path + "converted", ".wav");
            //ensure single channel
            string[] b = await Task.WhenAll(Files1.Select(file =>
            {
                string arguments = String.Format("\"{0}converted\\{1}\" \"{0}mono_audio\\{2}{3}\" remix 1", 
                    path, 
                    file.Name, 
                    Path.GetFileNameWithoutExtension(file.Name), 
                    file.Extension
                    );
                return RunProcessAsync(arguments, false);
            }));

            var Files2 = getFilesForDir(path + "mono_audio", ".wav");

            string[] lengths = await Task.WhenAll(Files2.Select(file =>
            {
                string arguments = String.Format("-D \"{0}mono_audio\\{1}\"", path, file.Name);
                return RunProcessAsync(arguments, true);
            }));

            List<Tuple<FileInfo, int>> readySplit = new List<Tuple<FileInfo, int>>();
            for (int i = 0; i < Files2.Count; i++)
            {
                int length = ((int)float.Parse(lengths[i].TrimEnd()));
                readySplit.Add(new Tuple<FileInfo, int>(Files2[i], length));
            }

            await Task.WhenAll(readySplit.Select(tuple =>
            {
                List<Task<string>> taskers = new List<Task<string>>();
                var file = tuple.Item1;
                if (!Directory.Exists(path + "ten_second_clips\\" + Path.GetFileNameWithoutExtension(file.Name)))
                {
                    DirectoryInfo di = Directory.CreateDirectory(path + "ten_second_clips\\" + Path.GetFileNameWithoutExtension(file.Name));
                }
                for (int i = 0; i < tuple.Item2 / 10; i++)
                {
                    string arguments = String.Format("\"{0}mono_audio\\{1}\" \"{0}ten_second_clips\\{2}\\{2}{4}-{5}{3}\" trim {4} 10",
                        path,
                        file.Name,
                        Path.GetFileNameWithoutExtension(file.Name),
                        file.Extension,
                        (10 * i).ToString(),
                        (10 * (i + 1)).ToString()
                        );
                    taskers.Add(RunProcessAsync(arguments, false));
                }
                return Task.WhenAll(taskers);
            }));

            var Files3 = getFilesForDir(path + "ten_second_clips", ".wav");

            Random rand = new Random();
            Parallel.For(0, abCount, index =>
            {
                string dirname = index.ToString();
                var file1 = Files3[rand.Next(Files3.Count() - 1)];
                var file2 = Files3[rand.Next(Files3.Count() - 1)];

                if (!Directory.Exists(path + "AB\\" + index))
                {
                    DirectoryInfo di = Directory.CreateDirectory(path + "AB\\" + index);
                    string arguments = String.Format("-m \"{1}\" \"{2}\" \"{0}AB\\{3}\\a_b.wav", 
                        path, 
                        file1.FullName, 
                        file2.FullName, 
                        dirname
                        );
                    ProcessFile(arguments, false);
                    File.Copy(file1.FullName, Path.Combine(di.FullName, "a" + file1.Extension), true);
                    File.Copy(file2.FullName, Path.Combine(di.FullName, "b" + file2.Extension), true);
                }
            });

            var Files4 = getFilesForDir(path + "AB", ".wav").Where(f => f.Name == "a_b.wav").ToList();

            Parallel.For(0, abcCount, index =>
            {
                string dirname = index.ToString();
                var file1 = Files4[rand.Next(Files4.Count() - 1)];
                var file2 = Files3[rand.Next(Files3.Count() - 1)];
                if (!Directory.Exists(path + "ABC\\" + index))
                {
                    DirectoryInfo di = Directory.CreateDirectory(path + "ABC\\" + index);
                    string arguments = String.Format("-m \"{1}\" \"{2}\" \"{0}ABC\\{3}\\a_b_c.wav", 
                        path, 
                        file1.FullName, 
                        file2.FullName, 
                        dirname
                        );
                    ProcessFile(arguments, false);
                    File.Copy(file1.DirectoryName + "\\a.wav", Path.Combine(di.FullName, "a" + file1.Extension), true);
                    File.Copy(file1.DirectoryName + "\\b.wav", Path.Combine(di.FullName, "b" + file1.Extension), true);
                    File.Copy(file2.FullName, Path.Combine(di.FullName, "c" + file2.Extension), true);
                }
            });
            Console.WriteLine("Done");
            System.Console.Read();
        }
        public List<FileInfo> getFilesForDir(string path, string extension)
        {
            var Files = new List<FileInfo>();
            var folderPath = new DirectoryInfo(path);
            try
            {
                FileInfo[] tempfiles = folderPath.GetFiles("*" + extension, System.IO.SearchOption.AllDirectories);
                foreach (FileInfo fi in tempfiles)
                {
                    try
                    {
                        string test = fi.FullName;
                        Files.Add(fi);
                    }
                    catch { }
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            return Files;
        }

        public string ProcessFile(string arguments, bool soxi)
        {
            try
            {
                Process pProcess = new Process();
                if (soxi)
                {
                    pProcess.StartInfo.FileName = @"C:\Program Files (x86)\sox-14-4-2\soxi.exe";
                }
                else
                {
                    pProcess.StartInfo.FileName = @"C:\Program Files (x86)\sox-14-4-2\sox.exe";
                }
                pProcess.StartInfo.Arguments = arguments;
                pProcess.StartInfo.UseShellExecute = false;
                pProcess.StartInfo.RedirectStandardOutput = true;
                pProcess.Start();
                string strOutput = pProcess.StandardOutput.ReadToEnd();
                pProcess.WaitForExit();
                return strOutput;
            }
            catch (Exception ee)
            {
                return "";
            }
        }
        public static Task<string> RunProcessAsync(string arguments, bool soxi)
        {
            var tcs = new TaskCompletionSource<string>();

            Process process = new Process();
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.EnableRaisingEvents = true;
            process.StartInfo.RedirectStandardOutput = true;
            if (soxi)
            {
                process.StartInfo.FileName = @"C:\Program Files (x86)\sox-14-4-2\soxi.exe";
            }
            else
            {
                process.StartInfo.FileName = @"C:\Program Files (x86)\sox-14-4-2\sox.exe";
            }

            process.Exited += (sender, args) =>
            {
                tcs.SetResult(process.StandardOutput.ReadToEnd());
                process.Dispose();
            };

            process.Start();

            return tcs.Task;
        }
    }
}
