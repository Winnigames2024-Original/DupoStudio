using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DupoLang
{
    /// <summary>
    /// Словарь DupoLang: значения по именам.
    /// <code>
    /// using Dupo.Data
    /// Data.Map user = new Data.Map()
    /// user["имя"] = "Лилия"
    /// print(user["имя"])
    /// // или короче, без модуля: var user = map("имя", "Лилия")
    /// </code>
    /// </summary>
    public class DupoMap
    {
        readonly Dictionary<string, object> items =
            new Dictionary<string, object>(StringComparer.Ordinal);

        readonly List<string> order = new List<string>();

        public DupoMap() { }

        /// <summary>Сколько пар внутри.</summary>
        public double Count { get { return items.Count; } }

        /// <summary>Имена в том порядке, в каком их добавляли.</summary>
        public List<object> Keys { get { return order.Select(x => (object)x).ToList(); } }

        /// <summary>Значения в том же порядке.</summary>
        public List<object> Values { get { return order.Select(x => items[x]).ToList(); } }

        /// <summary>Значение по имени или null.</summary>
        public object Get(string key)
        {
            object v;
            return items.TryGetValue(key ?? "", out v) ? v : null;
        }

        /// <summary>Значение по имени, а если его нет — запасное.</summary>
        public object Get(string key, object fallback)
        {
            object v;
            return items.TryGetValue(key ?? "", out v) ? v : fallback;
        }

        /// <summary>Положить значение.</summary>
        public DupoMap Set(string key, object value)
        {
            string k = key ?? "";
            if (!items.ContainsKey(k)) order.Add(k);
            items[k] = value;
            return this;
        }

        /// <summary>Есть ли такое имя.</summary>
        public bool Has(string key) { return items.ContainsKey(key ?? ""); }

        /// <summary>Убрать пару.</summary>
        public bool Remove(string key)
        {
            string k = key ?? "";
            order.Remove(k);
            return items.Remove(k);
        }

        /// <summary>Очистить.</summary>
        public DupoMap Clear()
        {
            items.Clear();
            order.Clear();
            return this;
        }

        /// <summary>Копия словаря.</summary>
        public DupoMap Copy()
        {
            var m = new DupoMap();
            foreach (string k in order) m.Set(k, items[k]);
            return m;
        }

        /// <summary>Внутренний словарь — для кода на C#.</summary>
        public IEnumerable<KeyValuePair<string, object>> Pairs
        {
            get { return order.Select(k => new KeyValuePair<string, object>(k, items[k])); }
        }

        public override string ToString()
        {
            var parts = order.Select(k => k + ": " + Val.Str(items[k]));
            return "{" + string.Join(", ", parts.ToArray()) + "}";
        }
    }
}
