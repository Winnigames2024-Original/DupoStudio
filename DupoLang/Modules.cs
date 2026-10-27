using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

namespace DupoLang
{
    /// <summary>Стандартная библиотека DupoLang.</summary>
    public static class Modules
    {
        public static readonly string[] Available =
        {
            "Dupo.Web", "Dupo.App", "Dupo.Java", "Dupo.Tools", "Dupo.Resources",
            "Dupo.Services", "Dupo.CSharp", "Dupo.Cpp",
            "Dupo.System.Files", "Dupo.System", "Dupo.IO", "Dupo.Sys"
        };

        public static NativeNamespace Load(string module, Interp interp)
        {
            switch (module)
            {
                case "Dupo.Web": return Web(interp);
                case "Dupo.IO": return IO();
                case "Dupo.Sys": return Sys();
                case "Dupo.App": return App(interp);
                case "Dupo.Java": return Java(interp);
                case "Dupo.Tools": return Tools(interp);
                case "Dupo.Resources": return ResourcesNs(interp);
                case "Dupo.Services": return ServicesNs(interp);
                case "Dupo.CSharp": return CSharpNs(interp);
                case "Dupo.Cpp": return CppNs(interp);
                case "Dupo.System.Files": return Files(interp);
                case "Dupo.System": return SystemNs(interp);
            }
            return null;
        }

        static object A(object[] a, int i) { return i < a.Length ? a[i] : null; }

        // ---------------- Dupo.Web ----------------
        static NativeNamespace Web(Interp interp)
        {
            DupoWebServer.Log = interp.Print;
            PhpTools.Log = interp.Print;

            var ns = new NativeNamespace("Web");
            ns.Members["Server"] = new NativeType("Server", typeof(DupoWebServer));
            ns.Members["Php"] = PhpTools.Instance;

            ns.Members["Open"] = new Builtin("Open", delegate (object[] a) {
                string url = Val.Str(A(a, 0));
                try { Process.Start(url); interp.Print("Открываю " + url); }
                catch (Exception ex) { interp.Print("Не открылось: " + ex.Message); }
                return null;
            });

            ns.Members["Get"] = new Builtin("Get", delegate (object[] a) {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Encoding = System.Text.Encoding.UTF8;
                    return wc.DownloadString(Val.Str(A(a, 0)));
                }
            });

            ns.Members["Download"] = new Builtin("Download", delegate (object[] a) {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                string file = DupoRuntime.Resolve(Val.Str(A(a, 1)));
                using (var wc = new WebClient()) wc.DownloadFile(Val.Str(A(a, 0)), file);
                interp.Print("Скачано: " + file);
                return file;
            });

            return ns;
        }

        // ---------------- Dupo.App ----------------
        static NativeNamespace App(Interp interp)
        {
            DappPackage.Log = interp.Print;

            var ns = new NativeNamespace("App");
            ns.Members["Package"] = new NativeType("Package", typeof(DappPackage));

            ns.Members["Export"] = new Builtin("Export", delegate (object[] a) {
                string file = DupoRuntime.Resolve(a.Length > 0 ? Val.Str(a[0]) : "app.dapp");
                var m = new DappManifest();
                m.Name = a.Length > 1 ? Val.Str(a[1]) : Path.GetFileName(DupoRuntime.ProjectDir);
                if (a.Length > 2) m.Version = Val.Str(a[2]);
                if (a.Length > 3) m.Author = Val.Str(a[3]);
                return DappPackage.Create(DupoRuntime.ProjectDir, file, m);
            });

            ns.Members["Import"] = new Builtin("Import", delegate (object[] a) {
                string file = DupoRuntime.Resolve(Val.Str(A(a, 0)));
                string dir = a.Length > 1
                    ? DupoRuntime.Resolve(Val.Str(a[1]))
                    : DupoRuntime.Resolve(Path.GetFileNameWithoutExtension(file));
                return DappPackage.Extract(file, dir);
            });

            ns.Members["Unpack"] = new Builtin("Unpack", delegate (object[] a) {
                return DappPackage.ExtractToTemp(DupoRuntime.Resolve(Val.Str(A(a, 0))));
            });

            ns.Members["Info"] = new Builtin("Info", delegate (object[] a) {
                return DappPackage.Read(DupoRuntime.Resolve(Val.Str(A(a, 0)))).Summary();
            });

            ns.Members["Files"] = new Builtin("Files", delegate (object[] a) {
                var info = DappPackage.Read(DupoRuntime.Resolve(Val.Str(A(a, 0))));
                return new List<object>(info.Entries.Select(x => (object)x.Path));
            });

            return ns;
        }

        // ---------------- Dupo.Java ----------------
        static NativeNamespace Java(Interp interp)
        {
            JavaTools.Log = interp.Print;

            // Target — живые свойства (IsInstalled, Version), Backing — пути (Home, JavacPath).
            var ns = new NativeNamespace("Java");
            ns.Target = JavaTools.Instance;
            ns.Backing = typeof(JavaTools);
            return ns;
        }

        // ---------------- Dupo.Tools ----------------
        static NativeNamespace Tools(Interp interp)
        {
            BuildTools.Log = interp.Print;

            var ns = new NativeNamespace("Tools");
            ns.Target = BuildTools.Instance;
            ns.Backing = typeof(BuildTools);
            return ns;
        }

        // ---------------- Dupo.Resources ----------------
        static NativeNamespace ResourcesNs(Interp interp)
        {
            var ns = new NativeNamespace("Resources");

            var found = ResourceSet.Load();
            foreach (var pair in found) ns.Members[pair.Key] = pair.Value;

            // Пара служебных имён на случай, если ресурс назван так же — не беда,
            // свои члены в словаре идут первыми.
            if (!ns.Members.ContainsKey("Folder")) ns.Members["Folder"] = ResourceSet.Folder;
            if (!ns.Members.ContainsKey("Count")) ns.Members["Count"] = (double)found.Count;

            interp.Print("Ресурсов подключено: " + found.Count +
                         (found.Count == 0 ? "  (папка " + ResourceSet.FolderName + " пуста или её нет)" : ""));

            return ns;
        }

        // ---------------- Dupo.Services ----------------
        static NativeNamespace ServicesNs(Interp interp)
        {
            ServiceTools.Log = interp.Print;

            var ns = new NativeNamespace("Services");
            ns.Members["Service"] = new NativeType("Service", typeof(DupoService));
            ns.Members["ServiceLevel"] = new NativeType("ServiceLevel", typeof(ServiceLevel));
            ns.Backing = typeof(ServiceTools);
            return ns;
        }

        // ---------------- Dupo.CSharp ----------------
        static NativeNamespace CSharpNs(Interp interp)
        {
            CsTools.Log = interp.Print;

            var ns = new NativeNamespace("CSharp");
            ns.Target = CsTools.Instance;
            ns.Backing = typeof(CsTools);
            return ns;
        }

        // ---------------- Dupo.Cpp ----------------
        static NativeNamespace CppNs(Interp interp)
        {
            CppTools.Log = interp.Print;

            var ns = new NativeNamespace("Cpp");
            ns.Target = CppTools.Instance;
            ns.Backing = typeof(CppTools);
            return ns;
        }

        // ---------------- Dupo.System.Files ----------------
        static NativeNamespace Files(Interp interp)
        {
            FileTools.Log = interp.Print;

            var ns = new NativeNamespace("Files");
            ns.Backing = typeof(FileTools);
            return ns;
        }

        // ---------------- Dupo.System ----------------
        static NativeNamespace SystemNs(Interp interp)
        {
            var ns = new NativeNamespace("System");
            ns.Members["Files"] = Files(interp);
            ns.Members["Java"] = Java(interp);
            ns.Members["Services"] = ServicesNs(interp);
            ns.Members["Sys"] = Sys();
            return ns;
        }

        // ---------------- Dupo.IO ----------------
        static NativeNamespace IO()
        {
            var ns = new NativeNamespace("IO");

            ns.Members["ReadFile"] = new Builtin("ReadFile", delegate (object[] a) {
                return File.ReadAllText(DupoRuntime.Resolve(Val.Str(A(a, 0))), System.Text.Encoding.UTF8);
            });
            ns.Members["WriteFile"] = new Builtin("WriteFile", delegate (object[] a) {
                string p = DupoRuntime.Resolve(Val.Str(A(a, 0)));
                string dir = Path.GetDirectoryName(p);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(p, Val.Str(A(a, 1)), new System.Text.UTF8Encoding(false));
                return p;
            });
            ns.Members["AppendFile"] = new Builtin("AppendFile", delegate (object[] a) {
                File.AppendAllText(DupoRuntime.Resolve(Val.Str(A(a, 0))), Val.Str(A(a, 1)),
                                   new System.Text.UTF8Encoding(false));
                return null;
            });
            ns.Members["Exists"] = new Builtin("Exists", delegate (object[] a) {
                string p = DupoRuntime.Resolve(Val.Str(A(a, 0)));
                return File.Exists(p) || Directory.Exists(p);
            });
            ns.Members["MakeDir"] = new Builtin("MakeDir", delegate (object[] a) {
                string p = DupoRuntime.Resolve(Val.Str(A(a, 0)));
                Directory.CreateDirectory(p);
                return p;
            });
            ns.Members["Delete"] = new Builtin("Delete", delegate (object[] a) {
                string p = DupoRuntime.Resolve(Val.Str(A(a, 0)));
                if (File.Exists(p)) File.Delete(p);
                else if (Directory.Exists(p)) Directory.Delete(p, true);
                return null;
            });
            ns.Members["Files"] = new Builtin("Files", delegate (object[] a) {
                string p = DupoRuntime.Resolve(Val.Str(A(a, 0)));
                return new List<object>(Directory.GetFiles(p).Cast<object>());
            });
            ns.Members["Dirs"] = new Builtin("Dirs", delegate (object[] a) {
                string p = DupoRuntime.Resolve(Val.Str(A(a, 0)));
                return new List<object>(Directory.GetDirectories(p).Cast<object>());
            });
            ns.Members["ProjectDir"] = new Builtin("ProjectDir", delegate (object[] a) {
                return DupoRuntime.ProjectDir;
            });

            return ns;
        }

        // ---------------- Dupo.Sys ----------------
        static NativeNamespace Sys()
        {
            var ns = new NativeNamespace("Sys");

            ns.Members["Run"] = new Builtin("Run", delegate (object[] a) {
                var psi = new ProcessStartInfo("cmd.exe", "/c " + Val.Str(A(a, 0)));
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.StandardOutputEncoding = System.Text.Encoding.GetEncoding(866);
                using (var pr = Process.Start(psi))
                {
                    string outp = pr.StandardOutput.ReadToEnd();
                    pr.WaitForExit();
                    return outp;
                }
            });
            ns.Members["Open"] = new Builtin("Open", delegate (object[] a) {
                Process.Start(DupoRuntime.Resolve(Val.Str(A(a, 0))));
                return null;
            });
            ns.Members["Env"] = new Builtin("Env", delegate (object[] a) {
                return Environment.GetEnvironmentVariable(Val.Str(A(a, 0))) ?? "";
            });
            ns.Members["Sleep"] = new Builtin("Sleep", delegate (object[] a) {
                Thread.Sleep((int)Math.Max(0, Val.Num(A(a, 0))));
                return null;
            });
            ns.Members["Time"] = new Builtin("Time", delegate (object[] a) {
                return DateTime.Now.ToString("HH:mm:ss");
            });
            ns.Members["Date"] = new Builtin("Date", delegate (object[] a) {
                return DateTime.Now.ToString("dd.MM.yyyy");
            });

            return ns;
        }
    }
}
