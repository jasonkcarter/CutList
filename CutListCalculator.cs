using System;
using System.Collections.Generic;
using System.Linq;
using CutList.Combinatorics;
using CutList.Properties;

namespace CutList
{
    public class CutListCalculator
    {
        public event EventHandler<CutOrder> CutOrderFound;

        public void FindAll()
        {
            var materials = new WoodList();
            materials.Load("materials.csv");

            var parts = new WoodList();
            parts.Load("parts.csv");
            foreach (string dimension in parts.Keys)
            {
                var materialOrders = new Permutations<decimal>(materials[dimension], GenerateOption.WithoutRepetition);

                foreach (IList<decimal> materialOrder in materialOrders)
                {
                    while (NextBoard(new CutOrder(), dimension, materialOrder.GetEnumerator(), parts[dimension]))
                    {
                        // Iterate through each board in the material order
                    }
                }
            }
        }

        public static CutOrder FindOptimal()
        {
            var calculator = new CutListCalculator();
            var dimensionOptimals = new Dictionary<string, CutOrder>();
            var dimensionWasteLengths = new Dictionary<string, decimal>();

            calculator.CutOrderFound += (sender, cutOrder) =>
            {
                string dimension = cutOrder.First().Key.Dimension;
                lock (dimensionOptimals)
                {
                    lock (dimensionWasteLengths)
                    {
                        CutOrder optimalCutOrder;
                        if (!dimensionOptimals.TryGetValue(dimension, out optimalCutOrder))
                        {
                            dimensionOptimals.Add(dimension, cutOrder);
                            return;
                        }
                        decimal optimalWasteLength;
                        if (!dimensionWasteLengths.TryGetValue(dimension, out optimalWasteLength))
                        {
                            optimalWasteLength = optimalCutOrder.ComputeWaste();
                            dimensionWasteLengths.Add(dimension, optimalWasteLength);
                        }


                        // Compute the waste length fo the current cut order
                        decimal wasteLength = cutOrder.ComputeWaste();

                        // If the waste length of the current is better than the best, then use current as the best going forward
                        if (wasteLength < optimalWasteLength)
                        {
                            dimensionWasteLengths[dimension] = wasteLength;
                            dimensionOptimals[dimension] = cutOrder;
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

        private static List<SumList> GetChildCutLengths(decimal boardLength, decimal[] parts)
        {
            var allChildCutOrders = new List<SumList>();

            var knownCutLengths = new List<decimal>();
            for (int i = 0; i < parts.Length; i++)
            {
                decimal cutLength = parts[i];

                // Performance: don't recurse duplicate cut lengths, since the child cut order will be the same.
                if (knownCutLengths.Contains(cutLength))
                {
                    continue;
                }
                knownCutLengths.Add(cutLength);
                if (cutLength > boardLength)
                {
                    continue;
                }

                if (cutLength == boardLength)
                {
                    var childCuts = new SumList {cutLength};
                    allChildCutOrders.Add(childCuts);
                    continue;
                }

                var childParts = new decimal[parts.Length - 1];
                for (int j = 0; j < i; j++)
                {
                    childParts[j] = parts[j];
                }
                for (int j = i + 1; j < parts.Length; j++)
                {
                    childParts[j - 1] = parts[j];
                }
                List<SumList> childCutLengths = GetChildCutLengths(
                    boardLength - cutLength - Settings.Default.BladeWidth,
                    childParts);
                if (childCutLengths.Count == 0)
                {
                    var childCuts = new SumList {cutLength};
                    allChildCutOrders.Add(childCuts);
                    continue;
                }
                foreach (var childCutLengthList in childCutLengths)
                {
                    childCutLengthList.Insert(0, cutLength);
                    // Ignore duplicates
                    if (!allChildCutOrders.Any(
                        x => x.Sum == childCutLengthList.Sum && !x.Except(childCutLengthList).Any()))
                    {
                        allChildCutOrders.Add(childCutLengthList);
                    }
                }
            }


            if (allChildCutOrders.Count == 0)
            {
                return new List<SumList>();
            }

            decimal mostEfficientSum = allChildCutOrders.Max(x => x.Sum);
            allChildCutOrders.RemoveAll(x => x.Sum < mostEfficientSum);

            return allChildCutOrders;
        }

        private bool NextBoard(CutOrder cutOrder, string dimension, IEnumerator<decimal> materialOrderEnumerator,
            List<decimal> dimensionParts)
        {
            if (!materialOrderEnumerator.MoveNext())
            {
                if (CutOrderFound != null)
                {
                    CutOrderFound(this, cutOrder);
                }
                return false;
            }

            decimal boardLength = materialOrderEnumerator.Current;


            List<SumList> childCutSumLists = GetChildCutLengths(boardLength, dimensionParts.ToArray());

            // The material order is no good, since there were no parts that would fit in this board
            if (childCutSumLists.Count == 0)
            {
                return false;
            }

            foreach (SumList childCutSumList in childCutSumLists)
            {
                var childCutLengths = new List<decimal>(childCutSumList);
                cutOrder.Add(new Board {Dimension = dimension, Length = boardLength}, childCutLengths);
                var childDimensionParts = new List<decimal>(dimensionParts);
                foreach (decimal childCutLength in childCutLengths)
                {
                    childDimensionParts.Remove(childCutLength);
                }

                NextBoard(cutOrder, dimension, materialOrderEnumerator, childDimensionParts);
            }
            return true;
        }
    }
}