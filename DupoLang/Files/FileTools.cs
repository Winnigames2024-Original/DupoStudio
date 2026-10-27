using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace DupoLang
{
    /// <summary>
    /// Dupo.System.Files — создание файлов и управление ими.
    /// Относительные пути считаются от папки проекта.
    /// </summary>
    public static class FileTools
    {
        public static Action<string> Log = delegate (string s) { Console.WriteLine(s); };

        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        static string P(string path) { return DupoRuntime.Resolve(path); }

        static void EnsureParent(string full)
        {
            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        // ---------- создание и запись ----------

        /// <summary>Создаёт файл (с папками по пути). Если он уже есть — не трогает.</summary>
        public static string Create(string path) { return Create(path, ""); }

        public static string Create(string path, string text)
        {
            string full = P(path);
            EnsureParent(full);
            if (!File.Exists(full)) File.WriteAllText(full, text ?? "", Utf8);
            return full;
        }

        /// <summary>Записывает текст, затирая прежнее содержимое.</summary>
        public static string Write(string path, string text)
        {
            string full = P(path);
            EnsureParent(full);
            File.WriteAllText(full, text ?? "", Utf8);
            return full;
        }

        /// <summary>Дописывает текст в конец.</summary>
        public static string Append(string path, string text)
        {
            string full = P(path);
            EnsureParent(full);
            File.AppendAllText(full, text ?? "", Utf8);
            return full;
        }

        /// <summary>Дописывает строку и перевод строки.</summary>
        public static string AppendLine(string path, string text)
        {
            return Append(path, (text ?? "") + Environment.NewLine);
        }

        // ---------- чтение ----------

        public static string Read(string path)
        {
            string full = P(path);
            if (!File.Exists(full)) throw new DupoError("Файл не найден: " + full, 0);
            return File.ReadAllText(full, Encoding.UTF8);
        }

        public static List<object> ReadLines(string path)
        {
            return new List<object>(File.ReadAllLines(P(path), Encoding.UTF8).Cast<object>());
        }

        /// <summary>Заменяет текст внутри файла. Возвращает число замен.</summary>
        public static double ReplaceIn(string path, string from, string to)
        {
            string full = P(path);
            string text = File.ReadAllText(full, Encoding.UTF8);
            if (string.IsNullOrEmpty(from)) return 0;

            int count = 0, idx = 0;
            while ((idx = text.IndexOf(from, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += from.Length;
            }

            if (count > 0) File.WriteAllText(full, text.Replace(from, to ?? ""), Utf8);
            return count;
        }

        public static bool Contains(string path, string text)
        {
            return File.ReadAllText(P(path), Encoding.UTF8).IndexOf(text ?? "", StringComparison.Ordinal) >= 0;
        }

        // ---------- файлы ----------

        public static bool Exists(string path) { return File.Exists(P(path)); }

        public static void Delete(string path)
        {
            string full = P(path);
            if (File.Exists(full)) File.Delete(full);
        }

        public static string Copy(string from, string to)
        {
            string target = P(to);
            EnsureParent(target);
            File.Copy(P(from), target, true);
            return target;
        }

        public static string Move(string from, string to)
        {
            string source = P(from);
            string target = P(to);
            EnsureParent(target);
            if (File.Exists(target)) File.Delete(target);
            File.Move(source, target);
            return target;
        }

        /// <summary>Переименовывает файл, оставляя его в той же папке.</summary>
        public static string Rename(string path, string newName)
        {
            string source = P(path);
            string target = Path.Combine(Path.GetDirectoryName(source), newName);
            if (File.Exists(target)) File.Delete(target);
            File.Move(source, target);
            return target;
        }

        public static double Size(string path) { return new FileInfo(P(path)).Length; }

        public static string Modified(string path)
        {
            return File.GetLastWriteTime(P(path)).ToString("dd.MM.yyyy HH:mm:ss");
        }

        // ---------- пути ----------

        public static string Name(string path) { return Path.GetFileName(path ?? ""); }
        public static string NameOnly(string path) { return Path.GetFileNameWithoutExtension(path ?? ""); }
        public static string Extension(string path) { return Path.GetExtension(path ?? ""); }
        public static string Folder(string path) { return Path.GetDirectoryName(P(path)); }
        public static string Full(string path) { return P(path); }

        public static string Join(string a, string b) { return Path.Combine(a ?? "", b ?? ""); }
        public static string Join(string a, string b, string c) { return Path.Combine(a ?? "", b ?? "", c ?? ""); }

        public static string Temp()
        {
            return Path.Combine(Path.GetTempPath(), "dupo-" + Guid.NewGuid().ToString("N") + ".tmp");
        }

        // ---------- папки ----------

        public static string MakeDir(string path)
        {
            string full = P(path);
            Directory.CreateDirectory(full);
            return full;
        }

        public static bool DirExists(string path) { return Directory.Exists(P(path)); }

        public static void DeleteDir(string path)
        {
            string full = P(path);
            if (Directory.Exists(full)) Directory.Delete(full, true);
        }

        public static string CopyDir(string from, string to)
        {
            string source = P(from);
            string target = P(to);
            Directory.CreateDirectory(target);

            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);

            foreach (string dir in Directory.GetDirectories(source))
                CopyDir(dir, Path.Combine(target, Path.GetFileName(dir)));

            return target;
        }

        // ---------- списки ----------

        public static List<object> List(string path) { return List(path, "*"); }

        public static List<object> List(string path, string mask)
        {
            string full = P(path);
            if (!Directory.Exists(full)) return new List<object>();
            return new List<object>(Directory.GetFiles(full, string.IsNullOrEmpty(mask) ? "*" : mask)
                                             .OrderBy(x => x).Cast<object>());
        }

        public static List<object> Dirs(string path)
        {
            string full = P(path);
            if (!Directory.Exists(full)) return new List<object>();
            return new List<object>(Directory.GetDirectories(full).OrderBy(x => x).Cast<object>());
        }

        /// <summary>Все файлы по маске, включая вложенные папки.</summary>
        public static List<object> Find(string path, string mask)
        {
            string full = P(path);
            if (!Directory.Exists(full)) return new List<object>();
            return new List<object>(
                Directory.GetFiles(full, string.IsNullOrEmpty(mask) ? "*" : mask, SearchOption.AllDirectories)
                         .OrderBy(x => x).Cast<object>());
        }

        public static List<object> Tree(string path) { return Find(path, "*"); }

        public static double Count(string path) { return List(path, "*").Count; }

        // ---------- архивы ----------

        public static string Zip(string folder, string zipFile)
        {
            string target = P(zipFile);
            EnsureParent(target);
            if (File.Exists(target)) File.Delete(target);
            ZipFile.CreateFromDirectory(P(folder), target);
            Log("Архив собран: " + target);
            return target;
        }

        public static string Unzip(string zipFile, string folder)
        {
            string target = P(folder);
            Directory.CreateDirectory(target);
            ZipFile.ExtractToDirectory(P(zipFile), target);
            Log("Распаковано в " + target);
            return target;
        }

        // ---------- система ----------

        /// <summary>Открывает файл или папку в проводнике или программе по умолчанию.</summary>
        public static void Open(string path)
        {
            try { Process.Start(P(path)); }
            catch (Exception ex) { Log("Не открылось: " + ex.Message); }
        }

        public static string ProjectDir() { return DupoRuntime.ProjectDir; }
    }
}
