namespace CutList
{
    public class PartPair
    {
        public PartPair(decimal part1, decimal part2)
        {
            LargerPart = part1 > part2 ? part1 : part2;
            SmallerPart = part1 < part2 ? part1 : part2;
            Key = string.Format("{0},{1}", LargerPart, SmallerPart);
            Sum = part1 + part2;
            Spread = LargerPart - SmallerPart;
        }

        public string Key { get; private set; }
        public decimal LargerPart { get; private set; }
        public decimal SmallerPart { get; private set; }
        public decimal Spread { get; private set; }
        public decimal Sum { get; private set; }
    }
}