using System;
using System.Collections.Generic;
using System.Linq;
using CutList.Properties;

namespace CutList
{
    public class CutListCalculator
    {
        public event EventHandler<CutOrderEventArgs> CutOrderFound;

        public void FindAll()
        {
            var materials = new WoodList();
            materials.Load("materials.csv");

            var parts = new WoodList();
            parts.Load("parts.csv");
            foreach (string dimension in parts.Keys)
            {
                var dimensionMaterials = materials[dimension];
                var dimensionParts = parts[dimension];

                if (dimensionMaterials.Distinct().Count() == 1)
                {
                    CutOrder cutOrder = FillLongestToShortest(dimension, dimensionMaterials.First(), dimensionParts);
                    if (CutOrderFound != null)
                    {
                        CutOrderFound(this, new CutOrderEventArgs(cutOrder));
                    }
                }
                else
                {
                    throw new NotSupportedException("Cannot calculate optimal for varying material lengths");
                }
            }
        }

        public static CutOrder FindOptimal()
        {
            var calculator = new CutListCalculator();
            var dimensionOptimals = new Dictionary<string, CutOrder>();
            var dimensionWasteLengths = new Dictionary<string, decimal>();

            calculator.CutOrderFound += (sender, e) =>
            {
                string dimension = e.CutOrder.First().Key.Dimension;
                lock (dimensionOptimals)
                {
                    lock (dimensionWasteLengths)
                    {
                        CutOrder optimalCutOrder;
                        if (!dimensionOptimals.TryGetValue(dimension, out optimalCutOrder))
                        {
                            dimensionOptimals.Add(dimension, e.CutOrder);
                            return;
                        }
                        decimal optimalWasteLength;
                        if (!dimensionWasteLengths.TryGetValue(dimension, out optimalWasteLength))
                        {
                            optimalWasteLength = optimalCutOrder.ComputeWaste();
                            dimensionWasteLengths.Add(dimension, optimalWasteLength);
                        }


                        // Compute the waste length fo the current cut order
                        decimal wasteLength = e.CutOrder.ComputeWaste();

                        // If the waste length of the current is better than the best, then use current as the best going forward
                        if (wasteLength < optimalWasteLength)
                        {
                            dimensionWasteLengths[dimension] = wasteLength;
                            dimensionOptimals[dimension] = e.CutOrder;
                        }
                    }
                }
            };

            calculator.FindAll();

            var optimal = new CutOrder();

            foreach (string dimension in dimensionOptimals.Keys)
            {
                foreach (Board board in dimensionOptimals[dimension].Keys)
                {
                    optimal.Add(board, dimensionOptimals[dimension][board]);
                }
            }
            return optimal;
        }

        private CutOrder FillLongestToShortest(string dimension, decimal boardLength, List<decimal> parts)
        {
            if (parts.Any(x => x > boardLength))
            {
                throw new ArgumentException("parts cannot contain any lengths longer than boardLength", "parts");
            }
            var cutOrder = new CutOrder();
            decimal currentBoardLength = 0M;
            var currentBoardCuts = new List<decimal>();
            bool isFirst = true;

            var remainingParts = new List<decimal>(parts);

            while (remainingParts.Count > 0)
            {
                if (isFirst)
                {
                    decimal firstCut = remainingParts[0];
                    currentBoardCuts.Add(firstCut);
                    remainingParts.RemoveAt(0);
                    currentBoardLength = boardLength - firstCut;
                    isFirst = false;
                    continue;
                }

                // Advanced strategy: for the last two cuts on the board when we have three or more cuts to go, 
                // find the max two-cut sum that will fit on the current board from the list of available boards
                PartPair lastCutPair = GetLongestPartPair(remainingParts, currentBoardLength);
                if (lastCutPair != null)
                {
                    remainingParts.Remove(lastCutPair.LargerPart);
                    remainingParts.Remove(lastCutPair.SmallerPart);
                    currentBoardCuts.Add(lastCutPair.LargerPart);
                    currentBoardCuts.Add(lastCutPair.SmallerPart);
                    cutOrder.Add(new Board {Dimension = dimension, Length = boardLength}, currentBoardCuts);

                    currentBoardLength = boardLength;
                    currentBoardCuts = new List<decimal>();
                    isFirst = true;
                    continue;
                }


                // Normal strategy: select the longest cut from the remaining parts that will fit on the current board
                decimal cut =
                    remainingParts.FirstOrDefault(x => currentBoardLength >= (x + Settings.Default.BladeWidth));
                if (cut != 0M)
                {
                    currentBoardCuts.Add(cut);
                    remainingParts.Remove(cut);
                    cut += Settings.Default.BladeWidth;
                    currentBoardLength -= cut;
                    continue;
                }

                cutOrder.Add(new Board {Dimension = dimension, Length = boardLength}, currentBoardCuts);
                currentBoardCuts = new List<decimal> ();
                currentBoardLength = boardLength;
                isFirst = true;
            }
            if (currentBoardCuts.Count > 0)
            {
                cutOrder.Add(new Board { Dimension = dimension, Length = boardLength }, currentBoardCuts);
            }

            return cutOrder;
        }

        /// <summary>
        ///     For parts lists with at least 3 more parts to go, finds the largest pair of remaining parts that will fit on a
        ///     given length of board, including the width of the saw blade.
        /// </summary>
        /// <param name="parts">The list of remaining parts to be cut, sorted from longest to shortest.</param>
        /// <param name="boardLength">The length of board to fit the cuts into.</param>
        /// <returns>
        ///     The largest part pair that will fit on the board; null if the all the parts are too big, or if there are fewer
        ///     than three boards left.
        /// </returns>
        private static PartPair GetLongestPartPair(IList<decimal> parts, decimal boardLength)
        {
            if (parts.Count < 3 || boardLength >= ((3*Settings.Default.BladeWidth) + parts[0] + parts[1] + parts[2]))
            {
                return null;
            }
            // Give me the largest pair of remaining parts that sum to less than the current board length + 2 * BladeWidth
            decimal cutPairThreshold = boardLength - (2*Settings.Default.BladeWidth);
            var distinctRemainingParts =
                parts.GroupBy(x => x).Select(x => new {Part = x.Key, Count = x.Count()}).ToArray();

            // If there's only one size of part left...
            if (distinctRemainingParts.Length == 1)
            {
                // See if two of them will fit on this board...
                decimal part = distinctRemainingParts.First().Part;
                decimal sum = part*2;

                // If not, return null
                if (sum > cutPairThreshold)
                {
                    return null;
                }

                // If so, return a pair with the part length
                return new PartPair(part, part);
            }

            for (int i = 0; i < distinctRemainingParts.Length; i++)
            {
                decimal firstPart = distinctRemainingParts[i].Part;
                if (firstPart > cutPairThreshold)
                {
                    continue;
                }
                int secondPartStartIndex = distinctRemainingParts[i].Count > 1 ? i : i + 1;
                for (int j = secondPartStartIndex; j < distinctRemainingParts.Length; j++)
                {
                    decimal secondPart = distinctRemainingParts[j].Part;
                    decimal sum = firstPart + secondPart;
                    if (sum > cutPairThreshold)
                    {
                        continue;
                    }

                    // Assumption: parts list is sorted largest to smallest.
                    // We can just return the first match, since the sum of the largest two component parts will be the longest.
                    return new PartPair(firstPart, secondPart);
                }
            }
            return null;
        }
    }
}