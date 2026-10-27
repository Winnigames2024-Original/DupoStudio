using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace DupoLang
{
    /// <summary>Пространство имён модуля, например Web.</summary>
    public class NativeNamespace
    {
        public string Name;

        /// <summary>Объект, чьи члены доступны через этот модуль (для живых свойств).</summary>
        public object Target;

        /// <summary>Тип, чьи статические члены доступны через этот модуль.</summary>
        public Type Backing;

        public readonly Dictionary<string, object> Members =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public NativeNamespace(string name) { Name = name; }
        public bool TryGet(string n, out object v) { return Members.TryGetValue(n, out v); }
        public override string ToString() { return "<модуль " + Name + ">"; }
    }

    /// <summary>Тип, доступный из DupoLang (например Web.Server).</summary>
    public class NativeType
    {
        public string Name;
        public Type Type;
        public NativeType(string name, Type t) { Name = name; Type = t; }
        public override string ToString() { return "<тип " + Name + ">"; }
    }

    public class NativeMethod
    {
        public object Target;
        public Type Owner;
        public string Name;
        public NativeMethod(object target, Type owner, string name) { Target = target; Owner = owner; Name = name; }
        public override string ToString() { return "<метод " + Owner.Name + "." + Name + ">"; }
    }

    public class Builtin
    {
        public string Name;
        public Func<object[], object> Fn;
        public Builtin(string name, Func<object[], object> fn) { Name = name; Fn = fn; }
        public override string ToString() { return "<функция " + Name + ">"; }
    }

    /// <summary>Работа со значениями DupoLang.</summary>
    public static class Val
    {
        public static bool Truth(object v)
        {
            if (v == null) return false;
            if (v is bool) return (bool)v;
            if (v is double) return (double)v != 0;
            if (v is string) return ((string)v).Length > 0;
            if (v is List<object>) return ((List<object>)v).Count > 0;
            if (v is DupoMap) return ((DupoMap)v).Count > 0;
            return true;
        }

        public static double Num(object v)
        {
            if (v == null) return 0;
            if (v is double) return (double)v;
            if (v is bool) return (bool)v ? 1 : 0;
            if (v is string)
            {
                double d;
                if (double.TryParse(((string)v).Replace(',', '.'), NumberStyles.Any,
                                    CultureInfo.InvariantCulture, out d)) return d;
                return 0;
            }
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); }
            catch { return 0; }
        }

        public static string Str(object v)
        {
            if (v == null) return "null";
            if (v is bool) return (bool)v ? "true" : "false";
            if (v is string) return (string)v;
            if (v is double)
            {
                double d = (double)v;
                if (double.IsNaN(d) || double.IsInfinity(d)) return d.ToString(CultureInfo.InvariantCulture);
                if (Math.Abs(d - Math.Round(d)) < 1e-10 && Math.Abs(d) < 1e15)
                    return ((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture);
                return d.ToString("0.##########", CultureInfo.InvariantCulture);
            }
            if (v is List<object>)
            {
                var items = ((List<object>)v).Select(x => x is string ? "\"" + x + "\"" : Str(x)).ToArray();
                return "[" + string.Join(", ", items) + "]";
            }
            return v.ToString();
        }

        public static string TypeName(object v)
        {
            if (v == null) return "null";
            if (v is double) return "number";
            if (v is string) return "string";
            if (v is bool) return "bool";
            if (v is List<object>) return "array";
            if (v is DupoMap) return "map";
            if (v is DupoFunction || v is Builtin || v is NativeMethod) return "function";
            if (v is NativeType) return "type";
            if (v is NativeNamespace) return "module";
            return v.GetType().Name;
        }

        public static bool Eq(object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a is string && b is string) return (string)a == (string)b;
            if (a is bool || b is bool) return Truth(a) == Truth(b);
            if (a is double || b is double)
            {
                if (a is string || b is string) return Str(a) == Str(b);
                return Math.Abs(Num(a) - Num(b)) < 1e-12;
            }
            return ReferenceEquals(a, b) || a.Equals(b);
        }
    }

    /// <summary>Мост между DupoLang и объектами .NET (рефлексия).</summary>
    public static class Interop
    {
        const BindingFlags InstFlags = BindingFlags.Public | BindingFlags.Instance |
                                       BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy;
        const BindingFlags StatFlags = BindingFlags.Public | BindingFlags.Static |
                                       BindingFlags.IgnoreCase | BindingFlags.FlattenHierarchy;

        public static object Wrap(object v)
        {
            if (v == null) return null;
            if (v is int) return (double)(int)v;
            if (v is long) return (double)(long)v;
            if (v is short) return (double)(short)v;
            if (v is byte) return (double)(byte)v;
            if (v is float) return (double)(float)v;
            if (v is decimal) return (double)(decimal)v;
            if (v is string[]) return new List<object>(((string[])v).Cast<object>());
            return v;
        }

        public static object Cast(object v, Type t)
        {
            if (t == typeof(object)) return v;
            if (v == null) return null;
            if (t.IsInstanceOfType(v)) return v;
            if (t == typeof(string)) return Val.Str(v);
            if (t == typeof(int)) return (int)Math.Round(Val.Num(v));
            if (t == typeof(long)) return (long)Math.Round(Val.Num(v));
            if (t == typeof(short)) return (short)Math.Round(Val.Num(v));
            if (t == typeof(byte)) return (byte)Math.Round(Val.Num(v));
            if (t == typeof(double)) return Val.Num(v);
            if (t == typeof(float)) return (float)Val.Num(v);
            if (t == typeof(decimal)) return (decimal)Val.Num(v);
            if (t == typeof(bool)) return Val.Truth(v);
            if (t.IsEnum) return Enum.Parse(t, Val.Str(v), true);
            if (t == typeof(string[]) && v is List<object>)
                return ((List<object>)v).Select(Val.Str).ToArray();
            throw new InvalidCastException("нельзя привести " + Val.TypeName(v) + " к " + t.Name);
        }

        public static object GetMember(object target, string name, int line)
        {
            if (target == null)
                throw new DupoError("Обращение к '" + name + "' у пустого значения (null)", line);

            var ns = target as NativeNamespace;
            if (ns != null)
            {
                object v;
                if (ns.TryGet(name, out v)) return v;

                if (ns.Target != null)
                {
                    Type ot = ns.Target.GetType();

                    var op = ot.GetProperty(name, InstFlags);
                    if (op != null && op.CanRead) return Wrap(op.GetValue(ns.Target, null));

                    var of = ot.GetField(name, InstFlags);
                    if (of != null) return Wrap(of.GetValue(ns.Target));

                    if (HasMethod(ot, name, false)) return new NativeMethod(ns.Target, ot, name);
                }

                if (ns.Backing != null)
                {
                    try { return GetStatic(ns.Backing, name, line); }
                    catch (DupoError) { }
                }

                throw new DupoError("В модуле '" + ns.Name + "' нет '" + name + "'", line);
            }

            var nt = target as NativeType;
            if (nt != null) return GetStatic(nt.Type, name, line);

            Type t = target.GetType();

            var p = t.GetProperty(name, InstFlags);
            if (p != null && p.CanRead) return Wrap(p.GetValue(target, null));

            var f = t.GetField(name, InstFlags);
            if (f != null) return Wrap(f.GetValue(target));

            if (HasMethod(t, name, false)) return new NativeMethod(target, t, name);

            throw new DupoError("У " + Val.TypeName(target) + " нет члена '" + name + "'", line);
        }

        static object GetStatic(Type t, string name, int line)
        {
            var p = t.GetProperty(name, StatFlags);
            if (p != null && p.CanRead) return Wrap(p.GetValue(null, null));

            var f = t.GetField(name, StatFlags);
            if (f != null) return Wrap(f.GetValue(null));

            var nested = t.GetNestedType(name, BindingFlags.Public | BindingFlags.IgnoreCase);
            if (nested != null) return new NativeType(name, nested);

            if (HasMethod(t, name, true)) return new NativeMethod(null, t, name);

            throw new DupoError("У типа " + t.Name + " нет статического члена '" + name + "'", line);
        }

        static bool HasMethod(Type t, string name, bool stat)
        {
            var flags = stat ? StatFlags : InstFlags;
            return t.GetMethods(flags).Any(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public static void SetMember(object target, string name, object value, int line)
        {
            if (target == null) throw new DupoError("Присваивание члену '" + name + "' у null", line);

            var ns = target as NativeNamespace;
            if (ns != null) { ns.Members[name] = value; return; }

            bool stat = target is NativeType;
            Type t = stat ? ((NativeType)target).Type : target.GetType();
            object inst = stat ? null : target;
            var flags = stat ? StatFlags : InstFlags;

            var p = t.GetProperty(name, flags);
            if (p != null && p.CanWrite) { p.SetValue(inst, Cast(value, p.PropertyType), null); return; }

            var f = t.GetField(name, flags);
            if (f != null) { f.SetValue(inst, Cast(value, f.FieldType)); return; }

            throw new DupoError("Нельзя записать в '" + name + "'", line);
        }

        public static object Invoke(NativeMethod m, object[] args, int line)
        {
            bool stat = m.Target == null;
            var flags = stat ? StatFlags : InstFlags;
            var cands = m.Owner.GetMethods(flags)
                .Where(x => string.Equals(x.Name, m.Name, StringComparison.OrdinalIgnoreCase)
                            && Accepts(x, args.Length))
                .OrderBy(x => Variadic(x) ? 1 : 0)
                .ToList();

            if (cands.Count == 0)
                throw new DupoError("Нет метода '" + m.Name + "' с " + args.Length + " аргументом(ами)", line);

            Exception last = null;
            foreach (var mi in cands)
            {
                object[] conv;
                if (!Fits(mi, args, out conv, ref last)) continue;
                try { return Wrap(mi.Invoke(m.Target, conv)); }
                catch (TargetInvocationException tie)
                {
                    throw new DupoError(m.Name + "(): " + tie.InnerException.Message, line);
                }
            }
            throw new DupoError("Аргументы не подходят для '" + m.Name + "'" +
                                (last == null ? "" : " (" + last.Message + ")"), line);
        }

        public static object Construct(Type t, object[] args, int line)
        {
            var cands = t.GetConstructors()
                .Where(c => Accepts(c, args.Length))
                .OrderBy(c => Variadic(c) ? 1 : 0)
                .ToList();

            if (cands.Count == 0)
                throw new DupoError("У типа " + t.Name + " нет конструктора с " + args.Length + " аргументом(ами)", line);

            Exception last = null;
            foreach (var ci in cands)
            {
                object[] conv;
                if (!Fits(ci, args, out conv, ref last)) continue;
                try { return ci.Invoke(conv); }
                catch (TargetInvocationException tie)
                {
                    throw new DupoError("new " + t.Name + "(): " + tie.InnerException.Message, line);
                }
            }
            throw new DupoError("Аргументы не подходят для new " + t.Name +
                                (last == null ? "" : " (" + last.Message + ")"), line);
        }

        /// <summary>Последний параметр объявлен как params.</summary>
        static bool Variadic(MethodBase m)
        {
            var ps = m.GetParameters();
            return ps.Length > 0 && ps[ps.Length - 1].IsDefined(typeof(ParamArrayAttribute), false);
        }

        /// <summary>Может ли метод принять столько аргументов.</summary>
        static bool Accepts(MethodBase m, int count)
        {
            int n = m.GetParameters().Length;
            return Variadic(m) ? count >= n - 1 : count == n;
        }

        /// <summary>
        /// Приводит аргументы к параметрам, при необходимости собирая
        /// хвост в массив params — иначе вызовы вида
        /// Lang.ExecuteFrom(файл, блок, арг1, арг2) не находили метода.
        /// </summary>
        static bool Fits(MethodBase m, object[] args, out object[] conv, ref Exception last)
        {
            conv = null;
            var ps = m.GetParameters();

            if (!Variadic(m))
            {
                if (ps.Length != args.Length) return false;
                return TryCastAll(args, ps, out conv, ref last);
            }

            int fixedCount = ps.Length - 1;
            if (args.Length < fixedCount) return false;

            // Массив могли передать и целиком — тогда ничего собирать не нужно.
            if (args.Length == ps.Length)
            {
                Exception ignore = null;
                object[] exact;
                if (TryCastAll(args, ps, out exact, ref ignore)) { conv = exact; return true; }
            }

            var head = new object[fixedCount];
            Array.Copy(args, head, fixedCount);

            var headPs = new ParameterInfo[fixedCount];
            Array.Copy(ps, headPs, fixedCount);

            object[] convHead;
            if (!TryCastAll(head, headPs, out convHead, ref last)) return false;

            Type elem = ps[fixedCount].ParameterType.GetElementType();
            var tail = Array.CreateInstance(elem, args.Length - fixedCount);

            for (int k = fixedCount; k < args.Length; k++)
            {
                try { tail.SetValue(Cast(args[k], elem), k - fixedCount); }
                catch (Exception ex) { last = ex; return false; }
            }

            conv = new object[ps.Length];
            Array.Copy(convHead, conv, fixedCount);
            conv[fixedCount] = tail;
            return true;
        }

        static bool TryCastAll(object[] args, ParameterInfo[] ps, out object[] result, ref Exception last)
        {
            result = new object[args.Length];
            for (int k = 0; k < args.Length; k++)
            {
                try { result[k] = Cast(args[k], ps[k].ParameterType); }
                catch (Exception ex) { last = ex; return false; }
            }
            return true;
        }
    }
}
