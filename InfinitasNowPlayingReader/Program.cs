using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IIDXMemory
{
    class Program
    {
        // REQUIRED CONSTS
        const int PROCESS_QUERY_INFORMATION = 0x0400;
        const int PROCESS_WM_READ = 0x0010;

        // REQUIRED METHODS
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess,
            Int64 lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        static Int64 songListStartAddress = Int64.Parse("141D633F0", System.Globalization.NumberStyles.HexNumber); // 5.1.1 memory start
        static Int64 nowPlayingStartAddress = Int64.Parse("141CE1498", System.Globalization.NumberStyles.HexNumber); // this is the address of the song playing preview file, for example 01006_pre.2dx

        static void Main(string[] args)
        {
            Process process = null;



            Console.WriteLine("Waiting for Infinitas launch...");

            while (process == null)
            {
                var processes = Process.GetProcessesByName("bm2dx");
                if (processes.Any())
                {
                    process = processes[0];
                    process.Exited += Process_Exited;
                }

                Thread.Sleep(2000);
            }

            Console.Clear();
            Console.WriteLine("Infinitas launched, waiting for song selection screen...");

            IntPtr processHandle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_WM_READ, false, process.Id);

            var songList = new Dictionary<string, IIDXSong>();

            var lastSongId = string.Empty;

            while(true)
            {
                var currentSongId = GetLastSongPreviewBgmId(processHandle);

                if (!string.IsNullOrEmpty(currentSongId) && !currentSongId.Contains("\0"))
                {
                    if (!songList.Any())
                    {
                        var initialMemory = songListStartAddress; // 5.1.1 lol
                        var currentMemory = initialMemory;
                        Console.WriteLine("Loading songlist from memory");
                        while (true)
                        {

                            int bytesRead = 0;

                            byte[] buffer = new byte[1008];

                            ReadProcessMemory((int)processHandle, currentMemory, buffer, buffer.Length, ref bytesRead);

                            var title1 = Encoding.GetEncoding("Shift-JIS").GetString(buffer.Take(64).ToArray());

                            if (string.IsNullOrWhiteSpace(title1))
                            {
                                break;
                            }

                            if (!title1.Contains("\0"))
                            {
                                break;
                            }

                            var title2 = Encoding.GetEncoding("Shift-JIS").GetString(buffer.Skip(64).Take(64).ToArray());
                            var genre = Encoding.GetEncoding("Shift-JIS").GetString(buffer.Skip(64).Skip(64).Take(64).ToArray());
                            var artist = Encoding.GetEncoding("Shift-JIS").GetString(buffer.Skip(64).Skip(64).Skip(64).Take(64).ToArray());

                            var idarray = buffer.Skip(256 + 368).Take(4).ToArray();

                            var songId = BitConverter.ToInt32(idarray, 0).ToString("D5");

                            var song = new IIDXSong
                            {
                                Title = title1,
                                EnglishTitle = title2,
                                Genre = genre,
                                Artist = artist
                            };

                            if (!songList.ContainsKey(songId))
                            {
                                songList.Add(songId, song);
                            }

                            currentMemory += 0x3F0;

                            Console.WriteLine(song.EnglishTitle);
                        }
                    }

                    if (lastSongId != currentSongId)
                    {
                        if (songList.ContainsKey(currentSongId))
                        {
                            var song = songList[currentSongId];

                            Console.Clear();

                            Console.WriteLine(currentSongId);
                            Console.WriteLine("Title:         " + song.Title);
                            Console.WriteLine("English title: " + song.EnglishTitle);
                            Console.WriteLine("Genre:         " + song.Genre);
                            Console.WriteLine("Artist:        " + song.Artist);

                            try
                            {
                                File.WriteAllText("title.txt", song.Title, Encoding.UTF8);
                                File.WriteAllText("englishtitle.txt", song.EnglishTitle, Encoding.UTF8);
                                File.WriteAllText("genre.txt", song.Genre, Encoding.UTF8);
                                File.WriteAllText("artist.txt", song.Artist, Encoding.UTF8);
                            }
                            catch
                            {
                                Console.WriteLine("Could not write song txt files. Check they aren't open.");
                            }

                            lastSongId = currentSongId;
                        }
                    }

                }

                Thread.Sleep(2000);
            }

        }

        private static void Process_Exited(object sender, EventArgs e)
        {
            Environment.Exit(0);
        }

        private static string GetLastSongPreviewBgmId(IntPtr processHandle)
        {
            int bytesRead = 0;

            byte[] buffer = new byte[5];

            ReadProcessMemory((int)processHandle, nowPlayingStartAddress, buffer, buffer.Length, ref bytesRead); 

            var lastBgmSongId = Encoding.UTF8.GetString(buffer);

            return lastBgmSongId;
        }
    }

    public class IIDXSong
    {
        public string Title { get; set; }
        public string EnglishTitle { get; set; }
        public string Genre { get; set; }
        public string Artist { get; set; }
    }
}
