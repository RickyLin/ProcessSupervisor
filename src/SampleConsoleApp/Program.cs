using System;

namespace SampleConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                Console.WriteLine("args:");
                foreach (string arg in args)
                {
                    Console.WriteLine(arg);
                }

                Console.WriteLine();
            }

            while(true)
            {
                Console.WriteLine(DateTime.Now.ToString());
                System.Threading.Thread.Sleep(1000);
            }
        }
    }
}
