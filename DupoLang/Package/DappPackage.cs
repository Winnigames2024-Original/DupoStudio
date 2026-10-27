using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace DupoLang
{
    /// <summary>
    /// Формат .dapp — контейнер приложения DupoLang.
    ///
    ///   "DAPP"              4 байта сигнатуры
    ///   uint16              версия формата
    ///   string              манифест (UTF-8, длина 7-битным префиксом)
    ///   int32               количество записей
    ///   записи:
    ///       string          путь относительно корня, через '/'
    ///       int32           исходный размер
    ///       uint32          CRC32 исходных байтов
    ///       byte            способ: 0 — как есть, 1 — deflate
    ///       int32           размер в архиве
    ///       byte[]          данные
    ///   "DEND"              4 байта завершителя
    /// </summary>
    public static class DappPackage
    {
        public const string Extension = ".dapp";
        public const uint Magic = 0x50504144;   // DAPP
        public const uint Trailer = 0x444E4544; // DEND
        public const ushort FormatVersion = 1;

        public static Action<string> Log = delegate (string s) { Console.WriteLine(s); };

        static readonly string[] SkipDirs =
            { "build", "bin", "obj", ".git", ".vs", "node_modules", "runtime" };

        // ---------------- упаковка ----------------

        public static string Create(string projectDir, string outFile, DappManifest manifest)
        {
            projectDir = Path.GetFullPath(projectDir);
            if (!Directory.Exists(projectDir))
                throw new DupoError("Папка проекта не найдена: " + projectDir, 0);

            if (manifest == null) manifest = new DappManifest();
            if (string.IsNullOrEmpty(manifest.Name)) manifest.Name = Path.GetFileName(projectDir);
            if (string.IsNullOrEmpty(manifest.Created))
                manifest.Created = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

            outFile = Path.GetFullPath(outFile);
            if (!outFile.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) outFile += Extension;

            string outDir = Path.GetDirectoryName(outFile);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            List<string> files = Collect(projectDir, outFile);
            if (files.Count == 0) throw new DupoError("В проекте нет файлов для упаковки", 0);

            if (!files.Any(f => Rel(projectDir, f).Equals(manifest.Main, StringComparison.OrdinalIgnoreCase)))
            {
                string guess = files.FirstOrDefault(f =>
                    f.EndsWith(".dupolang", StringComparison.OrdinalIgnoreCase));
                if (guess == null) throw new DupoError("Не нашёл главный файл .dupolang", 0);
                manifest.Main = Rel(projectDir, guess);
            }

            long total = 0;

            using (var fs = File.Create(outFile))
            using (var bw = new BinaryWriter(fs, new UTF8Encoding(false)))
            {
                bw.Write(Magic);
                bw.Write(FormatVersion);
                bw.Write(manifest.ToText());
                bw.Write(files.Count);

                foreach (string file in files)
                {
                    byte[] raw = File.ReadAllBytes(file);
                    byte[] packed = Compress(raw);

                    byte method = (byte)(packed.Length < raw.Length ? 1 : 0);
                    byte[] stored = method == 1 ? packed : raw;

                    bw.Write(Rel(projectDir, file));
                    bw.Write(raw.Length);
                    bw.Write(Crc32.Compute(raw));
                    bw.Write(method);
                    bw.Write(stored.Length);
                    bw.Write(stored);

                    total += raw.Length;
                }

                bw.Write(Trailer);
            }

            long packedSize = new FileInfo(outFile).Length;
            Log("Упаковано " + files.Count + " файлов: " + (total / 1024) + " КБ → " +
                (packedSize / 1024) + " КБ");
            Log("Архив: " + outFile);

            return outFile;
        }

        static List<string> Collect(string dir, string exclude)
        {
            var result = new List<string>();
            Walk(dir, dir, exclude, result);
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        static void Walk(string root, string dir, string exclude, List<string> result)
        {
            foreach (string file in Directory.GetFiles(dir))
            {
                if (string.Equals(Path.GetFullPath(file), exclude, StringComparison.OrdinalIgnoreCase)) continue;
                if (file.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) continue;
                result.Add(file);
            }

            foreach (string sub in Directory.GetDirectories(dir))
            {
                string name = Path.GetFileName(sub).ToLowerInvariant();
                if (Array.IndexOf(SkipDirs, name) >= 0) continue;
                Walk(root, sub, exclude, result);
            }
        }

        static string Rel(string root, string file)
        {
            string rel = Path.GetFullPath(file).Substring(Path.GetFullPath(root).Length);
            return rel.TrimStart('\\', '/').Replace('\\', '/');
        }

        // ---------------- чтение ----------------

        public static DappInfo Read(string file)
        {
            return Open(file, null);
        }

        /// <summary>Распаковывает архив в папку и возвращает путь к главному файлу.</summary>
        public static string Extract(string file, string targetDir)
        {
            targetDir = Path.GetFullPath(targetDir);
            Directory.CreateDirectory(targetDir);

            DappInfo info = Open(file, targetDir);

            Log("Распаковано " + info.Entries.Count + " файлов в " + targetDir);
            return Path.Combine(targetDir, info.Manifest.Main.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>Распаковывает во временную папку — для запуска без установки.</summary>
        public static string ExtractToTemp(string file)
        {
            DappInfo info = Read(file);
            string stamp = Math.Abs((Path.GetFullPath(file) + info.Manifest.Version).GetHashCode())
                               .ToString("X8");
            string dir = Path.Combine(Path.GetTempPath(), "dupo-" + SafeName(info.Manifest.Name) + "-" + stamp);
            return Extract(file, dir);
        }

        static DappInfo Open(string file, string targetDir)
        {
            file = Path.GetFullPath(file);
            if (!File.Exists(file)) throw new DupoError("Архив не найден: " + file, 0);

            var info = new DappInfo();
            info.File = file;
            info.FileSize = new FileInfo(file).Length;

            using (var fs = File.OpenRead(file))
            using (var br = new BinaryReader(fs, new UTF8Encoding(false)))
            {
                uint magic = br.ReadUInt32();
                if (magic != Magic)
                    throw new DupoError("Это не файл .dapp (неверная сигнатура)", 0);

                ushort version = br.ReadUInt16();
                if (version > FormatVersion)
                    throw new DupoError("Архив сделан более новой версией формата (" + version + ")", 0);

                info.Manifest = DappManifest.Parse(br.ReadString());

                int count = br.ReadInt32();
                if (count < 0 || count > 200000) throw new DupoError("Архив повреждён", 0);

                for (int i = 0; i < count; i++)
                {
                    var entry = new DappEntry();
                    entry.Path = br.ReadString();
                    entry.Size = br.ReadInt32();
                    entry.Crc = br.ReadUInt32();
                    entry.Method = br.ReadByte();
                    entry.Stored = br.ReadInt32();

                    byte[] stored = br.ReadBytes(entry.Stored);
                    if (stored.Length != entry.Stored)
                        throw new DupoError("Архив обрывается на '" + entry.Path + "'", 0);

                    info.Entries.Add(entry);
                    info.TotalSize += entry.Size;

                    if (targetDir == null) continue;

                    byte[] raw = entry.Method == 1 ? Decompress(stored, entry.Size) : stored;
                    if (Crc32.Compute(raw) != entry.Crc)
                        throw new DupoError("Повреждён файл '" + entry.Path + "' (не сошлась контрольная сумма)", 0);

                    WriteEntry(targetDir, entry.Path, raw);
                }

                if (targetDir != null || count >= 0)
                {
                    try
                    {
                        if (br.ReadUInt32() != Trailer) Log("Внимание: архив без завершителя, возможно обрезан.");
                    }
                    catch { Log("Внимание: архив без завершителя, возможно обрезан."); }
                }
            }

            return info;
        }

        static void WriteEntry(string targetDir, string relPath, byte[] data)
        {
            string safe = relPath.Replace('/', Path.DirectorySeparatorChar)
                                 .TrimStart(Path.DirectorySeparatorChar);

            string full = Path.GetFullPath(Path.Combine(targetDir, safe));
            if (!full.StartsWith(Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase))
                throw new DupoError("Подозрительный путь внутри архива: " + relPath, 0);

            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllBytes(full, data);
        }

        public static string SafeName(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name ?? "App")
                sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            string result = sb.ToString().Trim();
            return result.Length == 0 ? "App" : result;
        }

        // ---------------- сжатие ----------------

        static byte[] Compress(byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, true))
                    ds.Write(data, 0, data.Length);
                return ms.ToArray();
            }
        }

        static byte[] Decompress(byte[] data, int size)
        {
            using (var ms = new MemoryStream(data))
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
            using (var outMs = new MemoryStream(Math.Max(size, 16)))
            {
                ds.CopyTo(outMs);
                return outMs.ToArray();
            }
        }
    }
}
