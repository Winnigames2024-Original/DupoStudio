using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DupoLang
{
    /// <summary>Точка входа: запуск кода на DupoLang.</summary>
    public static class Dupo
    {
        public const string Version = "0.0.5";

        public static Interp RunFile(string path, Action<string> print)
        {
            string full = Path.GetFullPath(path);
            if (!File.Exists(full)) throw new DupoError("Файл не найден: " + full, 0);
            DupoRuntime.ProjectDir = Path.GetDirectoryName(full);
            return RunCode(File.ReadAllText(full, Encoding.UTF8), print);
        }

        public static Interp RunCode(string code, Action<string> print)
        {
            List<Token> tokens = new Lexer(code).Tokenize();
            List<Stmt> program = new Parser(tokens).ParseProgram();

            var interp = new Interp();
            if (print != null) interp.Print = print;
            interp.Run(program);
            return interp;
        }

        /// <summary>
        /// Мост для кода на C#: <c>Dupo.Lang.ExecuteFrom("файл.dupolang", "имяDef")</c>.
        /// Лежит внутри Dupo, поэтому с <c>using DupoLang;</c> вызывается
        /// буквально так, как написано, без путаницы с пространствами имён.
        /// </summary>
        public static class Lang
        {
            /// <summary>Выполнить блок def из файла .dupolang.</summary>
            public static object ExecuteFrom(string file, string def)
            {
                return LangBridge.ExecuteFrom(file, def);
            }

            /// <summary>Выполнить блок def с аргументами.</summary>
            public static object ExecuteFrom(string file, string def, params object[] args)
            {
                return LangBridge.ExecuteFrom(file, def, args);
            }

            /// <summary>Результат блока строкой.</summary>
            public static string TextFrom(string file, string def, params object[] args)
            {
                return Val.Str(LangBridge.ExecuteFrom(file, def, args));
            }

            /// <summary>Результат блока числом.</summary>
            public static double NumberFrom(string file, string def, params object[] args)
            {
                return Val.Num(LangBridge.ExecuteFrom(file, def, args));
            }

            /// <summary>Результат блока как «да/нет».</summary>
            public static bool TruthFrom(string file, string def, params object[] args)
            {
                return Val.Truth(LangBridge.ExecuteFrom(file, def, args));
            }

            /// <summary>Имена блоков в файле.</summary>
            public static List<object> Functions(string file) { return LangBridge.Functions(file); }

            /// <summary>Есть ли такой блок.</summary>
            public static bool HasFunction(string file, string def) { return LangBridge.HasFunction(file, def); }

            /// <summary>Сколько у блока параметров; -1 — блока нет.</summary>
            public static double Arity(string file, string def) { return LangBridge.Arity(file, def); }

            /// <summary>Выполнить файл целиком.</summary>
            public static void Run(string file) { LangBridge.RunFile(file); }

            /// <summary>Выполнить строку кода.</summary>
            public static void Code(string code) { LangBridge.RunCode(code); }

            /// <summary>Проверить файл; null — ошибок нет.</summary>
            public static string Check(string file) { return LangBridge.Check(file); }

            /// <summary>Куда печатает DupoLang при вызове из C#.</summary>
            public static Action<string> Output
            {
                get { return LangBridge.Log; }
                set { LangBridge.Log = value ?? delegate (string s) { }; }
            }

            /// <summary>Забыть разобранные файлы.</summary>
            public static void Reset() { LangBridge.Reset(); }

            /// <summary>Версия языка.</summary>
            public static string Version { get { return Dupo.Version; } }
        }

        /// <summary>Проверка синтаксиса без выполнения.</summary>
        public static string Check(string code)
        {
            try
            {
                new Parser(new Lexer(code).Tokenize()).ParseProgram();
                return null;
            }
            catch (DupoError e) { return e.ToString(); }
            catch (Exception e) { return e.Message; }
        }
    }
}
