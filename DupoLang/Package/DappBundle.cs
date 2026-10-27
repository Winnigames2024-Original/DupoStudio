using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace DupoLang
{
    /// <summary>
    /// Сборка проекта в один .exe.
    ///
    /// К копии хост-рантайма в хвост дописывается архив .dapp и метка:
    ///   [ ... обычный PE-файл ... ][ байты .dapp ][ int64 длина ][ uint32 CRC32 ][ "DAPPBND1" ]
    ///
    /// Windows игнорирует лишние байты после PE-образа, поэтому exe остаётся рабочим,
    /// а рантайм при старте читает сам себя и достаёт приложение.
    /// </summary>
    public static class DappBundle
    {
        public const string Magic = "DAPPBND1";
        const int TrailerSize = 8 + 4 + 8; // длина + CRC + сигнатура

        public static Action<string> Log = delegate (string s) { Console.WriteLine(s); };

        /// <summary>Манифест приложения, вшитого в текущий exe (после RunSelf).</summary>
        public static DappInfo LastInfo;

        public static string SelfPath()
        {
            try
            {
                string path = Process.GetCurrentProcess().MainModule.FileName;
                if (!string.IsNullOrEmpty(path)) return path;
            }
            catch { }

            Assembly asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            return asm.Location;
        }

        public static bool IsBundled { get { return HasPayload(SelfPath()); } }

        public static bool HasPayload(string exePath)
        {
            try
            {
                if (!File.Exists(exePath)) return false;
                using (var fs = File.OpenRead(exePath))
                {
                    if (fs.Length < TrailerSize + 16) return false;
                    fs.Seek(-8, SeekOrigin.End);
                    var sig = new byte[8];
                    fs.Read(sig, 0, 8);
                    return Encoding.ASCII.GetString(sig) == Magic;
                }
            }
            catch { return false; }
        }

        /// <summary>Достаёт вшитый архив из exe. null — если его там нет.</summary>
        public static byte[] ReadPayload(string exePath)
        {
            if (!HasPayload(exePath)) return null;

            using (var fs = File.OpenRead(exePath))
            using (var br = new BinaryReader(fs))
            {
                fs.Seek(-TrailerSize, SeekOrigin.End);
                long length = br.ReadInt64();
                uint crc = br.ReadUInt32();

                if (length <= 0 || length > fs.Length - TrailerSize)
                    throw new DupoError("Вшитое приложение повреждено (неверная длина)", 0);

                fs.Seek(fs.Length - TrailerSize - length, SeekOrigin.Begin);
                byte[] payload = br.ReadBytes((int)length);

                if (Crc32.Compute(payload) != crc)
                    throw new DupoError("Вшитое приложение повреждено (не сошлась контрольная сумма)", 0);

                return payload;
            }
        }

        /// <summary>Делает одиночный exe: хост + архив в хвосте.</summary>
        public static string Build(string hostExe, string outExe, byte[] dapp)
        {
            if (!File.Exists(hostExe))
                throw new DupoError("Не найден рантайм DupoHost.exe: " + hostExe, 0);
            if (dapp == null || dapp.Length == 0)
                throw new DupoError("Пустой архив приложения", 0);

            outExe = Path.GetFullPath(outExe);
            string dir = Path.GetDirectoryName(outExe);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.Copy(hostExe, outExe, true);

            using (var fs = new FileStream(outExe, FileMode.Append, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(dapp);
                bw.Write((long)dapp.Length);
                bw.Write(Crc32.Compute(dapp));
                bw.Write(Encoding.ASCII.GetBytes(Magic));
            }

            long size = new FileInfo(outExe).Length;
            Log("Собран один файл: " + outExe + "  (" + (size / 1024) + " КБ)");
            return outExe;
        }

        /// <summary>
        /// Достаёт приложение из текущего exe, разворачивает во временную папку
        /// и возвращает путь к главному скрипту. null — если exe без приложения.
        /// </summary>
        public static string RunSelf()
        {
            string exe = SelfPath();
            byte[] payload = ReadPayload(exe);
            if (payload == null) return null;

            uint crc = Crc32.Compute(payload);
            string cache = Path.Combine(Path.GetTempPath(), "dupo-bundle-" + crc.ToString("X8") + ".dapp");

            if (!File.Exists(cache) || new FileInfo(cache).Length != payload.Length)
                File.WriteAllBytes(cache, payload);

            LastInfo = DappPackage.Read(cache);
            return DappPackage.ExtractToTemp(cache);
        }
    }
}
