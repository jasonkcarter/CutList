using System.IO;

namespace CutList
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            CutOrder cutOrder = CutListCalculator.FindOptimal();
            const string outFile = "out.csv";
            File.WriteAllText(outFile, cutOrder.ToString());
        }
    }
}