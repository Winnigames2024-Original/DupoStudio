using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DupoLang
{
    public class Env
    {
        readonly Dictionary<string, object> vars = new Dictionary<string, object>(StringComparer.Ordinal);
        public readonly Env Parent;

        public Env(Env parent) { Parent = parent; }

        public void Define(string name, object value) { vars[name] = value; }

        public bool TryGet(string name, out object value)
        {
            Env e = this;
            while (e != null)
            {
                if (e.vars.TryGetValue(name, out value)) return true;
                e = e.Parent;
            }
            value = null;
            return false;
        }

        public bool TrySet(string name, object value)
        {
            Env e = this;
            while (e != null)
            {
                if (e.vars.ContainsKey(name)) { e.vars[name] = value; return true; }
                e = e.Parent;
            }
            return false;
        }
    }

    public class DupoFunction
    {
        public string Name;
        public List<string> Params;
        public BlockStmt Body;
        public Env Closure;
        public override string ToString() { return "<функция " + Name + ">"; }
    }

    class ReturnSignal : Exception { public object Value; public ReturnSignal(object v) { Value = v; } }
    class BreakSignal : Exception { }
    class ContinueSignal : Exception { }

    public class Interp
    {
        public readonly Env Globals;
        public Action<string> Print;
        public readonly List<IDisposable> Resources = new List<IDisposable>();
        readonly Random rnd = new Random();

        public Interp()
        {
            Globals = new Env(null);
            Print = delegate (string s) { Console.WriteLine(s); };
            RegisterBuiltins();
        }

        public void Run(List<Stmt> program)
        {
            foreach (var s in program) if (s is FuncStmt) Exec(s, Globals);
            foreach (var s in program) if (!(s is FuncStmt)) Exec(s, Globals);
        }

        /// <summary>
        /// Объявить блоки и подключить модули, но не выполнять код верхнего
        /// уровня. Нужно, когда из C# вызывают один def из файла.
        /// </summary>
        public void Declare(List<Stmt> program)
        {
            foreach (var s in program)
                if (s is ImportStmt || s is FuncStmt) Exec(s, Globals);
        }

        /// <summary>Закрывает всё, что запустил скрипт (серверы и т.п.).</summary>
        public void Shutdown()
        {
            foreach (var r in Resources.ToArray())
            {
                try { r.Dispose(); } catch { }
            }
            Resources.Clear();
        }

        /// <summary>Остался ли работающий сервер после завершения скрипта.</summary>
        public bool HasLiveResources
        {
            get
            {
                foreach (var r in Resources)
                {
                    var server = r as DupoWebServer;
                    if (server == null) return true;
                    if (server.Running) return true;
                }
                return false;
            }
        }

        void Def(string name, Func<object[], object> fn) { Globals.Define(name, new Builtin(name, fn)); }

        static object Arg(object[] a, int i) { return i < a.Length ? a[i] : null; }

        void RegisterBuiltins()
        {
            Def("print", delegate (object[] a) {
                Print(string.Join(" ", a.Select(Val.Str).ToArray()));
                return null;
            });
            Def("println", delegate (object[] a) {
                Print(string.Join(" ", a.Select(Val.Str).ToArray()));
                return null;
            });
            Def("str", delegate (object[] a) { return Val.Str(Arg(a, 0)); });
            Def("num", delegate (object[] a) { return Val.Num(Arg(a, 0)); });
            Def("int", delegate (object[] a) { return Math.Truncate(Val.Num(Arg(a, 0))); });
            Def("round", delegate (object[] a) { return Math.Round(Val.Num(Arg(a, 0))); });
            Def("abs", delegate (object[] a) { return Math.Abs(Val.Num(Arg(a, 0))); });
            Def("type", delegate (object[] a) { return Val.TypeName(Arg(a, 0)); });
            Def("len", delegate (object[] a) {
                object v = Arg(a, 0);
                if (v is string) return (double)((string)v).Length;
                if (v is List<object>) return (double)((List<object>)v).Count;
                if (v is DupoMap) return ((DupoMap)v).Count;
                return 0d;
            });
            Def("wait", delegate (object[] a) {
                Thread.Sleep((int)Math.Max(0, Val.Num(Arg(a, 0))));
                return null;
            });
            Def("now", delegate (object[] a) { return DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"); });
            Def("random", delegate (object[] a) {
                if (a.Length == 0) return rnd.NextDouble();
                if (a.Length == 1) return (double)rnd.Next((int)Val.Num(a[0]));
                return (double)rnd.Next((int)Val.Num(a[0]), (int)Val.Num(a[1]));
            });
            Def("list", delegate (object[] a) { return new List<object>(a); });
            Def("map", delegate (object[] a) {
                // map("имя", значение, "имя2", значение2, ...)
                var m = new DupoMap();
                for (int k = 0; k + 1 < a.Length; k += 2) m.Set(Val.Str(a[k]), a[k + 1]);
                return m;
            });
            Def("keys", delegate (object[] a) {
                var m = Arg(a, 0) as DupoMap;
                return m == null ? new List<object>() : m.Keys;
            });
            Def("values", delegate (object[] a) {
                var m = Arg(a, 0) as DupoMap;
                return m == null ? new List<object>() : m.Values;
            });
            Def("has", delegate (object[] a) {
                var m = Arg(a, 0) as DupoMap;
                return m != null && m.Has(Val.Str(Arg(a, 1)));
            });
            Def("push", delegate (object[] a) {
                var l = Arg(a, 0) as List<object>;
                if (l != null) for (int k = 1; k < a.Length; k++) l.Add(a[k]);
                return l;
            });
            Def("upper", delegate (object[] a) { return Val.Str(Arg(a, 0)).ToUpperInvariant(); });
            Def("lower", delegate (object[] a) { return Val.Str(Arg(a, 0)).ToLowerInvariant(); });
            Def("split", delegate (object[] a) {
                var parts = Val.Str(Arg(a, 0)).Split(new[] { Val.Str(Arg(a, 1)) }, StringSplitOptions.None);
                return new List<object>(parts.Cast<object>());
            });
        }

        // ---------- операторы ----------

        void Exec(Stmt s, Env env)
        {
            var im = s as ImportStmt;
            if (im != null)
            {
                var ns = Modules.Load(im.Module, this);
                if (ns == null)
                    throw new DupoError("Нет модуля '" + im.Module + "'. Доступны: " +
                                        string.Join(", ", Modules.Available), im.Line);
                Globals.Define(ns.Name, ns);
                return;
            }

            var vs = s as VarStmt;
            if (vs != null)
            {
                env.Define(vs.Name, vs.Init == null ? Default(vs.TypeName) : Eval(vs.Init, env));
                return;
            }

            var es = s as ExprStmt;
            if (es != null) { Eval(es.Value, env); return; }

            var bs = s as BlockStmt;
            if (bs != null) { ExecBlock(bs, new Env(env)); return; }

            var ifs = s as IfStmt;
            if (ifs != null)
            {
                if (Val.Truth(Eval(ifs.Cond, env))) Exec(ifs.Then, new Env(env));
                else if (ifs.Else != null) Exec(ifs.Else, new Env(env));
                return;
            }

            var ws = s as WhileStmt;
            if (ws != null)
            {
                int guard = 0;
                while (Val.Truth(Eval(ws.Cond, env)))
                {
                    try { Exec(ws.Body, new Env(env)); }
                    catch (BreakSignal) { break; }
                    catch (ContinueSignal) { }
                    if (++guard > 50000000) throw new DupoError("Похоже, бесконечный цикл", ws.Line);
                }
                return;
            }

            var fs = s as ForStmt;
            if (fs != null)
            {
                var loopEnv = new Env(env);
                if (fs.Init != null) Exec(fs.Init, loopEnv);
                while (fs.Cond == null || Val.Truth(Eval(fs.Cond, loopEnv)))
                {
                    try { Exec(fs.Body, new Env(loopEnv)); }
                    catch (BreakSignal) { break; }
                    catch (ContinueSignal) { }
                    if (fs.Step != null) Exec(fs.Step, loopEnv);
                }
                return;
            }

            var fn = s as FuncStmt;
            if (fn != null)
            {
                env.Define(fn.Name, new DupoFunction
                {
                    Name = fn.Name,
                    Params = fn.Params,
                    Body = fn.Body,
                    Closure = env
                });
                return;
            }

            var rs = s as ReturnStmt;
            if (rs != null) throw new ReturnSignal(rs.Value == null ? null : Eval(rs.Value, env));
            if (s is BreakStmt) throw new BreakSignal();
            if (s is ContinueStmt) throw new ContinueSignal();

            throw new DupoError("Неизвестная конструкция", s.Line);
        }

        void ExecBlock(BlockStmt block, Env env)
        {
            foreach (var s in block.Body) Exec(s, env);
        }

        static object Default(string typeName)
        {
            if (typeName == null) return null;
            string t = typeName.ToLowerInvariant();
            if (t == "int" || t == "number" || t == "double" || t == "float") return 0d;
            if (t == "string" || t == "text") return "";
            if (t == "bool" || t == "boolean") return false;
            return null;
        }

        // ---------- выражения ----------

        public object Eval(Expr e, Env env)
        {
            var n = e as NumberExpr; if (n != null) return n.Value;
            var st = e as StringExpr; if (st != null) return st.Value;
            var b = e as BoolExpr; if (b != null) return b.Value;
            if (e is NullExpr) return null;

            var id = e as IdentExpr;
            if (id != null)
            {
                object v;
                if (env.TryGet(id.Name, out v)) return v;
                throw new DupoError("Неизвестное имя '" + id.Name + "'", id.Line);
            }

            var me = e as MemberExpr;
            if (me != null) return Interop.GetMember(Eval(me.Target, env), me.Name, me.Line);

            var ie = e as IndexExpr;
            if (ie != null)
            {
                object t = Eval(ie.Target, env);
                object idx = Eval(ie.Index, env);
                var list = t as List<object>;
                if (list != null)
                {
                    int k = (int)Val.Num(idx);
                    if (k < 0 || k >= list.Count) throw new DupoError("Индекс " + k + " вне массива", ie.Line);
                    return list[k];
                }
                var map = t as DupoMap;
                if (map != null) return map.Get(Val.Str(idx));

                if (t is string)
                {
                    string s = (string)t;
                    int k = (int)Val.Num(idx);
                    if (k < 0 || k >= s.Length) throw new DupoError("Индекс " + k + " вне строки", ie.Line);
                    return s[k].ToString();
                }
                throw new DupoError("Индексация не поддерживается для " + Val.TypeName(t), ie.Line);
            }

            var ar = e as ArrayExpr;
            if (ar != null) return new List<object>(ar.Items.Select(x => Eval(x, env)));

            var ce = e as CallExpr;
            if (ce != null)
            {
                object callee = Eval(ce.Callee, env);
                object[] args = ce.Args.Select(x => Eval(x, env)).ToArray();
                return CallValue(callee, args, ce.Line);
            }

            var ne = e as NewExpr;
            if (ne != null)
            {
                object t = Eval(ne.TypeExpr, env);
                var nt = t as NativeType;
                if (nt == null) throw new DupoError("После 'new' должен быть тип", ne.Line);
                object[] args = ne.Args.Select(x => Eval(x, env)).ToArray();
                object instance = Interop.Construct(nt.Type, args, ne.Line);
                var disp = instance as IDisposable;
                if (disp != null) Resources.Add(disp);
                return instance;
            }

            var ue = e as UnaryExpr;
            if (ue != null)
            {
                object v = Eval(ue.Right, env);
                if (ue.Op == "!") return !Val.Truth(v);
                if (ue.Op == "-") return -Val.Num(v);
                return Val.Num(v);
            }

            var be = e as BinaryExpr;
            if (be != null) return Binary(be, env);

            var ae = e as AssignExpr;
            if (ae != null) return Assign(ae, env);

            throw new DupoError("Непонятное выражение", e.Line);
        }

        object Binary(BinaryExpr be, Env env)
        {
            if (be.Op == "&&") return Val.Truth(Eval(be.Left, env)) && Val.Truth(Eval(be.Right, env));
            if (be.Op == "||") return Val.Truth(Eval(be.Left, env)) || Val.Truth(Eval(be.Right, env));

            object l = Eval(be.Left, env);
            object r = Eval(be.Right, env);

            switch (be.Op)
            {
                case "+":
                    if (l is string || r is string) return Val.Str(l) + Val.Str(r);
                    return Val.Num(l) + Val.Num(r);
                case "-": return Val.Num(l) - Val.Num(r);
                case "*": return Val.Num(l) * Val.Num(r);
                case "/":
                    if (Val.Num(r) == 0) throw new DupoError("Деление на ноль", be.Line);
                    return Val.Num(l) / Val.Num(r);
                case "%":
                    if (Val.Num(r) == 0) throw new DupoError("Деление на ноль", be.Line);
                    return Val.Num(l) % Val.Num(r);
                case "==": return Val.Eq(l, r);
                case "!=": return !Val.Eq(l, r);
                case "<": return Val.Num(l) < Val.Num(r);
                case ">": return Val.Num(l) > Val.Num(r);
                case "<=": return Val.Num(l) <= Val.Num(r);
                case ">=": return Val.Num(l) >= Val.Num(r);
            }
            throw new DupoError("Неизвестная операция '" + be.Op + "'", be.Line);
        }

        object Assign(AssignExpr ae, Env env)
        {
            object value = Eval(ae.Value, env);

            if (ae.Op != "=")
            {
                object old = Eval(ae.Target, env);
                string op = ae.Op.Substring(0, 1);
                if (op == "+" && (old is string || value is string)) value = Val.Str(old) + Val.Str(value);
                else if (op == "+") value = Val.Num(old) + Val.Num(value);
                else if (op == "-") value = Val.Num(old) - Val.Num(value);
                else if (op == "*") value = Val.Num(old) * Val.Num(value);
                else if (op == "/") value = Val.Num(old) / Val.Num(value);
            }

            var id = ae.Target as IdentExpr;
            if (id != null)
            {
                if (!env.TrySet(id.Name, value)) env.Define(id.Name, value);
                return value;
            }

            var me = ae.Target as MemberExpr;
            if (me != null)
            {
                Interop.SetMember(Eval(me.Target, env), me.Name, value, me.Line);
                return value;
            }

            var ie = ae.Target as IndexExpr;
            if (ie != null)
            {
                object target = Eval(ie.Target, env);

                var map = target as DupoMap;
                if (map != null)
                {
                    map.Set(Val.Str(Eval(ie.Index, env)), value);
                    return value;
                }

                var list = target as List<object>;
                if (list == null) throw new DupoError("Слева от '=' не массив и не словарь", ie.Line);
                int k = (int)Val.Num(Eval(ie.Index, env));
                while (list.Count <= k) list.Add(null);
                list[k] = value;
                return value;
            }

            throw new DupoError("Нельзя присвоить это значение", ae.Line);
        }

        public object CallValue(object callee, object[] args, int line)
        {
            var bi = callee as Builtin;
            if (bi != null) return bi.Fn(args);

            var nm = callee as NativeMethod;
            if (nm != null) return Interop.Invoke(nm, args, line);

            var fn = callee as DupoFunction;
            if (fn != null)
            {
                var local = new Env(fn.Closure);
                for (int k = 0; k < fn.Params.Count; k++)
                    local.Define(fn.Params[k], k < args.Length ? args[k] : null);
                try { ExecBlock(fn.Body, local); }
                catch (ReturnSignal r) { return r.Value; }
                return null;
            }

            var nt = callee as NativeType;
            if (nt != null) return Interop.Construct(nt.Type, args, line);

            throw new DupoError(Val.TypeName(callee) + " нельзя вызвать как функцию", line);
        }
    }
}
