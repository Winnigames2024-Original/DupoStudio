using System;

namespace CsDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("Привет из CsDemo!");

            for (int i = 1; i <= 3; i++)
                Console.WriteLine("  строка " + i);
        }
    }
}
