using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DupoLang
{
    /// <summary>
    /// Мост между C# и DupoLang. Живёт внутри <see cref="Dupo"/>, поэтому
    /// из кода на C# вызывается ровно так, как ожидается:
    /// <code>
    /// using DupoLang;
    /// object x = Dupo.Lang.ExecuteFrom("scripts/logic.dupolang", "Привет");
    /// </code>
    /// На стороне DupoLang то же самое доступно через <c>using Dupo.Lang</c>.
    /// </summary>
    public static class LangBridge
    {
        public static Action<string> Log = delegate (string s) { Console.WriteLine(s); };

        /// <summary>Разобранный файл вместе со временем правки — чтобы не читать заново.</summary>
        class Loaded
        {
            public List<Stmt> Program;
            public DateTime Stamp;
            public string Dir;
        }

        static readonly Dictionary<string, Loaded> cache =
            new Dictionary<string, Loaded>(StringComparer.OrdinalIgnoreCase);

        // ------------------------------------------------------------------
        // главное
        // ------------------------------------------------------------------

        /// <summary>
        /// Выполнить блок <c>def</c> (или <c>func</c>) из файла .dupolang
        /// и вернуть его результат.
        /// </summary>
        public static object ExecuteFrom(string file, string def)
        {
            return ExecuteFrom(file, def, new object[0]);
        }

        /// <summary>Выполнить блок с аргументами.</summary>
        public static object ExecuteFrom(string file, string def, params object[] args)
        {
            if (string.IsNullOrEmpty(def))
                throw new ArgumentException("не указано имя блока def");

            string full = Full(file);
            var loaded = Load(full);

            var interp = Prepare(loaded);

            object fn;
            if (!interp.Globals.TryGet(def, out fn) || !(fn is DupoFunction))
            {
                var have = Functions(file);
                throw new DupoError(
                    "В файле " + Path.GetFileName(full) + " нет блока def '" + def + "'" +
                    (have.Count == 0 ? "" : ". Есть: " + string.Join(", ", have.Select(Val.Str).ToArray())),
                    0);
            }

            try
            {
                return interp.CallValue(fn, Wrap(args), 0);
            }
            finally
            {
                // Серверы и прочее, поднятое блоком, оставляем жить:
                // гасим только когда ничего живого не осталось.
                if (!interp.HasLiveResources) interp.Shutdown();
            }
        }

        /// <summary>Имена всех блоков def и func в файле.</summary>
        public static List<object> Functions(string file)
        {
            var loaded = Load(Full(file));
            return loaded.Program.OfType<FuncStmt>().Select(x => (object)x.Name).ToList();
        }

        /// <summary>Есть ли такой блок.</summary>
        public static bool HasFunction(string file, string def)
        {
            return Functions(file).Any(x => string.Equals(Val.Str(x), def, StringComparison.Ordinal));
        }

        /// <summary>Сколько у блока параметров.</summary>
        public static double Arity(string file, string def)
        {
            var loaded = Load(Full(file));
            var fn = loaded.Program.OfType<FuncStmt>()
                           .FirstOrDefault(x => string.Equals(x.Name, def, StringComparison.Ordinal));
            return fn == null ? -1 : fn.Params.Count;
        }

        /// <summary>Выполнить файл целиком, как это делает F5.</summary>
        public static object RunFile(string file)
        {
            string full = Full(file);
            var interp = Dupo.RunFile(full, Log);
            if (!interp.HasLiveResources) interp.Shutdown();
            return null;
        }

        /// <summary>Выполнить строку кода.</summary>
        public static object RunCode(string code)
        {
            var interp = Dupo.RunCode(code ?? "", Log);
            if (!interp.HasLiveResources) interp.Shutdown();
            return null;
        }

        /// <summary>Проверить файл на синтаксис. null — всё хорошо.</summary>
        public static string Check(string file)
        {
            try { return Dupo.Check(File.ReadAllText(Full(file), Encoding.UTF8)); }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>Версия языка.</summary>
        public static string Version { get { return Dupo.Version; } }

        /// <summary>Забыть разобранные файлы.</summary>
        public static void Reset() { lock (cache) cache.Clear(); }

        // ------------------------------------------------------------------
        // внутреннее
        // ------------------------------------------------------------------

        static string Full(string file)
        {
            string path = DupoRuntime.Resolve(file ?? "");
            if (!File.Exists(path))
                throw new FileNotFoundException("файл DupoLang не найден: " + path, path);
            return path;
        }

        static Loaded Load(string full)
        {
            DateTime stamp = File.GetLastWriteTimeUtc(full);

            lock (cache)
            {
                Loaded had;
                if (cache.TryGetValue(full, out had) && had.Stamp == stamp) return had;

                string code = File.ReadAllText(full, Encoding.UTF8);
                var program = new Parser(new Lexer(code).Tokenize()).ParseProgram();

                var fresh = new Loaded
                {
                    Program = program,
                    Stamp = stamp,
                    Dir = Path.GetDirectoryName(full)
                };
                cache[full] = fresh;
                return fresh;
            }
        }

        /// <summary>
        /// Готовит интерпретатор: подключает модули и объявляет блоки,
        /// но не выполняет код верхнего уровня — иначе файл «запустился бы»
        /// целиком, а нам нужен один блок.
        /// </summary>
        static Interp Prepare(Loaded loaded)
        {
            string was = DupoRuntime.ProjectDir;
            if (!string.IsNullOrEmpty(loaded.Dir)) DupoRuntime.ProjectDir = loaded.Dir;

            var interp = new Interp();
            interp.Print = Log;

            try
            {
                interp.Declare(loaded.Program);
            }
            catch
            {
                DupoRuntime.ProjectDir = was;
                throw;
            }

            return interp;
        }

        static object[] Wrap(object[] args)
        {
            if (args == null) return new object[0];
            var res = new object[args.Length];
            for (int i = 0; i < args.Length; i++) res[i] = Interop.Wrap(args[i]);
            return res;
        }
    }
}
