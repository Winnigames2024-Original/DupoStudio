using System;
using DupoLang;

namespace BridgeDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Куда DupoLang будет печатать из блоков
            Dupo.Lang.Output = Console.WriteLine;

            string file = "logic.dupolang";

            Console.WriteLine("Блоки в файле: " + Val.Str(Dupo.Lang.Functions(file)));
            Console.WriteLine();

            Console.WriteLine(Dupo.Lang.ExecuteFrom(file, "Привет", "Лилия"));
            Console.WriteLine("Сумма 2 и 3 = " + Dupo.Lang.NumberFrom(file, "Сумма", 2, 3));
            Console.WriteLine(Dupo.Lang.TextFrom(file, "Громко", "  тише едешь  "));
            Console.WriteLine("Повтор: " + Dupo.Lang.TextFrom(file, "Повтор", "ab", 3));
            Console.WriteLine();

            var info = Dupo.Lang.ExecuteFrom(file, "Сведения") as DupoMap;
            if (info != null)
                foreach (var pair in info.Pairs)
                    Console.WriteLine("  " + pair.Key + " = " + Val.Str(pair.Value));

            Console.WriteLine();
            Console.WriteLine("Есть ли блок 'Нету': " + Dupo.Lang.HasFunction(file, "Нету"));
            Console.WriteLine("Параметров у 'Сумма': " + Dupo.Lang.Arity(file, "Сумма"));
        }
    }
}
