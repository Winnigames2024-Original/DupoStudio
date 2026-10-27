using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DupoLang;

namespace DupoCs
{
    /// <summary>
    /// Переводит разобранный DupoLang в исходник на C#.
    ///
    /// Переменные становятся dynamic — так арифметика и склейка строк
    /// ведут себя ровно как в DupoLang. Обращения к модулям переписываются
    /// на типы из DupoLang.dll; то, для чего пары нет, остаётся как есть
    /// и помечается комментарием.
    /// </summary>
    public class CSharpWriter
    {
        readonly StringBuilder sb = new StringBuilder();
        readonly List<FuncStmt> functions = new List<FuncStmt>();
        readonly HashSet<string> funcNames = new HashSet<string>(StringComparer.Ordinal);
        readonly List<string> modules = new List<string>();
        readonly List<string> notes = new List<string>();

        int indent;

        public string Namespace = "Generated";
        public string ClassName = "Program";
        public string SourceName = "программы";

        /// <summary>Что не удалось перевести один в один.</summary>
        public IList<string> Notes { get { return notes; } }

        static readonly HashSet<string> Builtins = new HashSet<string>(StringComparer.Ordinal)
        {
            "print", "println", "str", "num", "int", "round", "abs", "type", "len",
            "wait", "now", "random", "list", "push", "upper", "lower", "split"
        };

        // Пары «как в DupoLang» → «как в C#».
        static readonly KeyValuePair<string, string>[] TypeMap =
        {
            new KeyValuePair<string, string>("Web.Server.Php", "PhpTools.Instance"),
            new KeyValuePair<string, string>("Web.Server", "DupoWebServer"),
            new KeyValuePair<string, string>("CSharp", "CsTools.Instance"),
            new KeyValuePair<string, string>("Cpp", "CppTools.Instance"),
            new KeyValuePair<string, string>("Services.ServiceLevel", "ServiceLevel"),
            new KeyValuePair<string, string>("Services.Service", "DupoService"),
            new KeyValuePair<string, string>("Services", "ServiceTools"),
            new KeyValuePair<string, string>("System.Services", "ServiceTools"),
            new KeyValuePair<string, string>("System.Files", "FileTools"),
            new KeyValuePair<string, string>("System.Java", "JavaTools.Instance"),
            new KeyValuePair<string, string>("Files", "FileTools"),
            new KeyValuePair<string, string>("Java", "JavaTools.Instance"),
        };

        public string Build(List<Stmt> program)
        {
            foreach (Stmt s in program)
            {
                var fn = s as FuncStmt;
                if (fn != null) { functions.Add(fn); funcNames.Add(fn.Name); }

                var im = s as ImportStmt;
                if (im != null) modules.Add(im.Module);
            }

            Header();

            Line("static class " + ClassName);
            Line("{");
            indent++;

            Line("static void Main(string[] args)");
            Line("{");
            indent++;

            foreach (Stmt s in program)
            {
                if (s is FuncStmt || s is ImportStmt) continue;
                Statement(s);
            }

            indent--;
            Line("}");

            foreach (FuncStmt fn in functions)
            {
                Blank();
                Function(fn);
            }

            indent--;
            Line("}");

            Blank();
            Runtime();

            indent--;
            Line("}");

            return sb.ToString();
        }

        // ---------------- каркас ----------------

        void Header()
        {
            Line("// Сгенерировано DupoLang.exe из " + SourceName + ".");
            Line("// Для модулей нужна ссылка на DupoLang.dll.");
            if (modules.Count > 0)
                Line("// Подключено в исходнике: " + string.Join(", ", modules.ToArray()));
            Blank();
            Line("using System;");
            Line("using System.Collections.Generic;");
            Line("using System.Linq;");
            Line("using DupoLang;");
            Blank();
            Line("namespace " + Namespace);
            Line("{");
            indent++;
        }

        void Function(FuncStmt fn)
        {
            string ps = string.Join(", ", fn.Params.Select(x => "dynamic " + Safe(x)).ToArray());
            Line("static dynamic " + Safe(fn.Name) + "(" + ps + ")");
            Block(fn.Body);
        }

        /// <summary>Небольшой набор помощников — чтобы встроенные функции вели себя так же.</summary>
        void Runtime()
        {
            Line("/// <summary>Встроенные функции DupoLang.</summary>");
            Line("static class Rt");
            Line("{");
            indent++;
            Line("static readonly Random rnd = new Random();");
            Blank();
            Line("public static string Str(object v) { return Val.Str(v); }");
            Line("public static double Num(object v) { return Val.Num(v); }");
            Line("public static string Type(object v) { return Val.TypeName(v); }");
            Blank();
            Line("public static object Print(params object[] a)");
            Line("{");
            Line("    Console.WriteLine(string.Join(\" \", a.Select(Val.Str).ToArray()));");
            Line("    return null;");
            Line("}");
            Blank();
            Line("public static double Len(object v)");
            Line("{");
            Line("    var s = v as string; if (s != null) return s.Length;");
            Line("    var l = v as List<object>; if (l != null) return l.Count;");
            Line("    return 0;");
            Line("}");
            Blank();
            Line("public static object Index(object target, object key)");
            Line("{");
            Line("    int i = (int)Val.Num(key);");
            Line("    var l = target as List<object>; if (l != null) return l[i];");
            Line("    var s = target as string; if (s != null) return s[i].ToString();");
            Line("    throw new InvalidOperationException(\"Индексация не поддерживается\");");
            Line("}");
            Blank();
            Line("public static object Push(object target, params object[] items)");
            Line("{");
            Line("    var l = target as List<object>;");
            Line("    if (l != null) l.AddRange(items);");
            Line("    return l;");
            Line("}");
            Blank();
            Line("public static List<object> List(params object[] a) { return new List<object>(a); }");
            Line("public static object Wait(object ms) { System.Threading.Thread.Sleep((int)Val.Num(ms)); return null; }");
            Line("public static string Now() { return DateTime.Now.ToString(\"dd.MM.yyyy HH:mm:ss\"); }");
            Line("public static string Upper(object v) { return Val.Str(v).ToUpperInvariant(); }");
            Line("public static string Lower(object v) { return Val.Str(v).ToLowerInvariant(); }");
            Blank();
            Line("public static List<object> Split(object v, object sep)");
            Line("{");
            Line("    var parts = Val.Str(v).Split(new[] { Val.Str(sep) }, StringSplitOptions.None);");
            Line("    return new List<object>(parts.Cast<object>());");
            Line("}");
            Blank();
            Line("public static double Random(params object[] a)");
            Line("{");
            Line("    if (a.Length == 0) return rnd.NextDouble();");
            Line("    if (a.Length == 1) return rnd.Next((int)Val.Num(a[0]));");
            Line("    return rnd.Next((int)Val.Num(a[0]), (int)Val.Num(a[1]));");
            Line("}");
            Blank();
            Line("public static object Open(object url)");
            Line("{");
            Line("    try { System.Diagnostics.Process.Start(Val.Str(url)); } catch { }");
            Line("    return null;");
            Line("}");
            indent--;
            Line("}");
        }

        // ---------------- операторы ----------------

        void Block(Stmt body)
        {
            var block = body as BlockStmt;
            if (block == null)
            {
                Line("{");
                indent++;
                Statement(body);
                indent--;
                Line("}");
                return;
            }

            Line("{");
            indent++;
            foreach (Stmt s in block.Body) Statement(s);
            indent--;
            Line("}");
        }

        void Statement(Stmt s)
        {
            var vs = s as VarStmt;
            if (vs != null)
            {
                string init = vs.Init == null ? Default(vs.TypeName) : Expr(vs.Init);
                Line("dynamic " + Safe(vs.Name) + " = " + init + ";");
                return;
            }

            var es = s as ExprStmt;
            if (es != null) { Line(Expr(es.Value) + ";"); return; }

            var bs = s as BlockStmt;
            if (bs != null) { Block(bs); return; }

            var ifs = s as IfStmt;
            if (ifs != null)
            {
                Line("if (Val.Truth(" + Expr(ifs.Cond) + "))");
                Block(ifs.Then);
                if (ifs.Else != null)
                {
                    Line("else");
                    Block(ifs.Else);
                }
                return;
            }

            var ws = s as WhileStmt;
            if (ws != null)
            {
                Line("while (Val.Truth(" + Expr(ws.Cond) + "))");
                Block(ws.Body);
                return;
            }

            var fs = s as ForStmt;
            if (fs != null)
            {
                Line("{");
                indent++;
                if (fs.Init != null) Statement(fs.Init);
                Line("while (" + (fs.Cond == null ? "true" : "Val.Truth(" + Expr(fs.Cond) + ")") + ")");
                Line("{");
                indent++;
                if (fs.Body is BlockStmt)
                    foreach (Stmt inner in ((BlockStmt)fs.Body).Body) Statement(inner);
                else
                    Statement(fs.Body);
                if (fs.Step != null) Statement(fs.Step);
                indent--;
                Line("}");
                indent--;
                Line("}");
                return;
            }

            var rs = s as ReturnStmt;
            if (rs != null)
            {
                Line(rs.Value == null ? "return null;" : "return " + Expr(rs.Value) + ";");
                return;
            }

            if (s is BreakStmt) { Line("break;"); return; }
            if (s is ContinueStmt) { Line("continue;"); return; }

            if (s is FuncStmt || s is ImportStmt) return;

            Line("// не удалось перевести конструкцию");
            notes.Add("строка " + s.Line + ": конструкция не переведена");
        }

        static string Default(string typeName)
        {
            string t = (typeName ?? "").ToLowerInvariant();
            if (t == "int" || t == "number" || t == "double" || t == "float") return "0d";
            if (t == "string" || t == "text") return "\"\"";
            if (t == "bool" || t == "boolean") return "false";
            return "null";
        }

        // ---------------- выражения ----------------

        string Expr(Expr e)
        {
            var n = e as NumberExpr;
            if (n != null) return n.Value.ToString("R", CultureInfo.InvariantCulture) + "d";

            var st = e as StringExpr;
            if (st != null) return Quote(st.Value);

            var b = e as BoolExpr;
            if (b != null) return b.Value ? "true" : "false";

            if (e is NullExpr) return "null";

            var id = e as IdentExpr;
            if (id != null) return Safe(id.Name);

            var me = e as MemberExpr;
            if (me != null) return Member(me);

            var ie = e as IndexExpr;
            if (ie != null) return "Rt.Index(" + Expr(ie.Target) + ", " + Expr(ie.Index) + ")";

            var ar = e as ArrayExpr;
            if (ar != null)
                return "new List<object> { " + string.Join(", ", ar.Items.Select(Expr).ToArray()) + " }";

            var ce = e as CallExpr;
            if (ce != null) return Call(ce);

            var ne = e as NewExpr;
            if (ne != null)
                return "new " + Member(ne.TypeExpr) + "(" + Args(ne.Args) + ")";

            var ue = e as UnaryExpr;
            if (ue != null)
            {
                if (ue.Op == "!") return "(!Val.Truth(" + Expr(ue.Right) + "))";
                return "(" + ue.Op + Expr(ue.Right) + ")";
            }

            var be = e as BinaryExpr;
            if (be != null)
            {
                if (be.Op == "&&" || be.Op == "||")
                    return "(Val.Truth(" + Expr(be.Left) + ") " + be.Op + " Val.Truth(" + Expr(be.Right) + "))";
                return "(" + Expr(be.Left) + " " + be.Op + " " + Expr(be.Right) + ")";
            }

            var ae = e as AssignExpr;
            if (ae != null)
                return Expr(ae.Target) + " " + ae.Op + " " + Expr(ae.Value);

            notes.Add("строка " + e.Line + ": выражение не переведено");
            return "null /* не переведено */";
        }

        string Args(List<Expr> args)
        {
            return string.Join(", ", args.Select(Expr).ToArray());
        }

        string Call(CallExpr ce)
        {
            var id = ce.Callee as IdentExpr;
            if (id != null)
            {
                if (Builtins.Contains(id.Name)) return Builtin(id.Name, ce.Args);
                if (funcNames.Contains(id.Name)) return Safe(id.Name) + "(" + Args(ce.Args) + ")";
            }

            return Expr(ce.Callee) + "(" + Args(ce.Args) + ")";
        }

        string Builtin(string name, List<Expr> args)
        {
            string a = Args(args);
            switch (name)
            {
                case "print":
                case "println": return "Rt.Print(" + a + ")";
                case "str": return "Rt.Str(" + a + ")";
                case "num": return "Rt.Num(" + a + ")";
                case "int": return "Math.Truncate(Rt.Num(" + a + "))";
                case "round": return "Math.Round(Rt.Num(" + a + "))";
                case "abs": return "Math.Abs(Rt.Num(" + a + "))";
                case "type": return "Rt.Type(" + a + ")";
                case "len": return "Rt.Len(" + a + ")";
                case "wait": return "Rt.Wait(" + a + ")";
                case "now": return "Rt.Now()";
                case "random": return "Rt.Random(" + a + ")";
                case "list": return "Rt.List(" + a + ")";
                case "push": return "Rt.Push(" + a + ")";
                case "upper": return "Rt.Upper(" + a + ")";
                case "lower": return "Rt.Lower(" + a + ")";
                case "split": return "Rt.Split(" + a + ")";
            }
            return "Rt.Print(" + a + ")";
        }

        /// <summary>Склеивает цепочку обращений и подставляет пару из таблицы.</summary>
        string Member(Expr e)
        {
            string path = Path(e);
            if (path == null) return Expr(e) + ".?";

            foreach (var pair in TypeMap)
            {
                if (path == pair.Key) return pair.Value;
                if (path.StartsWith(pair.Key + ".", StringComparison.Ordinal))
                    return pair.Value + path.Substring(pair.Key.Length);
            }

            if (path.StartsWith("Web.", StringComparison.Ordinal))
            {
                string rest = path.Substring(4);
                if (rest == "Open") return "Rt.Open";
                notes.Add("Web." + rest + " — пары в C# нет, оставлено как есть");
            }

            return path;
        }

        /// <summary>Превращает a.b.c в строку. null — если в цепочке есть вызовы.</summary>
        static string Path(Expr e)
        {
            var id = e as IdentExpr;
            if (id != null) return id.Name;

            var me = e as MemberExpr;
            if (me == null) return null;

            string left = Path(me.Target);
            return left == null ? null : left + "." + me.Name;
        }

        // ---------------- мелочи ----------------

        static readonly HashSet<string> Reserved = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract","as","base","bool","break","byte","case","catch","char","checked","class","const",
            "continue","decimal","default","delegate","do","double","else","enum","event","explicit",
            "extern","false","finally","fixed","float","for","foreach","goto","if","implicit","in","int",
            "interface","internal","is","lock","long","namespace","new","null","object","operator","out",
            "override","params","private","protected","public","readonly","ref","return","sbyte","sealed",
            "short","sizeof","stackalloc","static","string","struct","switch","this","throw","true","try",
            "typeof","uint","ulong","unchecked","unsafe","ushort","using","virtual","void","volatile","while"
        };

        static string Safe(string name)
        {
            return Reserved.Contains(name) ? "@" + name : name;
        }

        static string Quote(string value)
        {
            var q = new StringBuilder("\"");
            foreach (char c in value ?? "")
            {
                if (c == '"') q.Append("\\\"");
                else if (c == '\\') q.Append("\\\\");
                else if (c == '\n') q.Append("\\n");
                else if (c == '\r') q.Append("\\r");
                else if (c == '\t') q.Append("\\t");
                else q.Append(c);
            }
            return q.Append('"').ToString();
        }

        void Line(string text)
        {
            sb.Append(new string(' ', indent * 4)).Append(text).Append("\r\n");
        }

        void Blank() { sb.Append("\r\n"); }
    }
}
