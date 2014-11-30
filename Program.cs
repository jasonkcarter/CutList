using System;
using System.IO;

namespace CutList
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            CutOrder cutOrder = CutListCalculator.FindOptimal();
            if (cutOrder == null)
            {
                Console.WriteLine("No optimal cut order found.");
            }
            else
            {
                const string outFile = "out.csv";
                File.WriteAllText(outFile, cutOrder.ToString());
                Console.WriteLine("Optimal cut order written to {0}", outFile);
            }
            Console.ReadKey();
        }
    }
}