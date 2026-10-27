using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DupoLang
{
    /// <summary>Точка входа: запуск кода на DupoLang.</summary>
    public static class Dupo
    {
        public const string Version = "0.0.2";

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
