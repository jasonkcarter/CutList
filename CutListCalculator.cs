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
                    // Start to recurse through all the boards in the bill of materials
                    NextBoard(new CutOrder(), dimension, materialOrder, 0, parts[dimension]);
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
                decimal partLength = parts[i];

                // Performance: don't recurse duplicate cut lengths, since the child cut order will be the same.
                if (knownCutLengths.Contains(partLength))
                {
                    continue;
                }
                knownCutLengths.Add(partLength);
                if (partLength > boardLength)
                {
                    continue;
                }

                if (partLength == boardLength)
                {
                    var childCuts = new SumList {partLength};
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
                    boardLength - partLength - Settings.Default.BladeWidth,
                    childParts);
                if (childCutLengths.Count == 0)
                {
                    var childCuts = new SumList {partLength};
                    allChildCutOrders.Add(childCuts);
                    continue;
                }
                foreach (var childCutLengthList in childCutLengths)
                {
                    childCutLengthList.Insert(0, partLength);
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

        private void NextBoard(CutOrder cutOrder, string dimension, IList<decimal> materialOrder, int currentIndex,
            List<decimal> dimensionParts)
        {
            // Ran out of parts to cut. The cut order is good.
            if (dimensionParts.Count == 0)
            {
                if (CutOrderFound != null)
                {
                    CutOrderFound(this, cutOrder);
                }
                return;
            }

            // Ran out of boards. Bad build order.
            if (currentIndex > (materialOrder.Count - 1))
            {
                return;
            }

            decimal boardLength = materialOrder[currentIndex];

            // Performance: assume if we still need all the same parts as were in the last cut, and we have the same size board, we can copy the results of the previous board.
            if (cutOrder.Count > 0)
            {
                var previous = cutOrder.Last();
                if (previous.Key.Dimension == dimension && previous.Key.Length == boardLength)
                {
                    var previousCutCounts = previous.Value
                        .GroupBy(x => x)
                        .Select(grp => new {Length = grp.Key, Count = grp.Count()});
                    bool canRepeatPrevious = previousCutCounts
                        .All(previousCut => dimensionParts.Count(x => x == previousCut.Length) >= previousCut.Count);
                    if (canRepeatPrevious)
                    {
                        var board = new Board {Dimension = dimension, Length = boardLength};
                        var cuts = new List<decimal>(previous.Value);
                        cutOrder.Add(board, cuts);
                        var childDimensionParts = new List<decimal>(dimensionParts);
                        foreach (decimal cutLength in cuts)
                        {
                            childDimensionParts.Remove(cutLength);
                        }
                        NextBoard(cutOrder, dimension, materialOrder, currentIndex + 1, childDimensionParts);
                        return;
                    }
                }
            }

            List<SumList> childCutSumLists = GetChildCutLengths(boardLength, dimensionParts.ToArray());

            // The material order is no good, since there were no parts that would fit in this board
            if (childCutSumLists.Count == 0)
            {
                return;
            }

            // Create a new cut order instance for each child branch to avoid recursive calls to 
            // sibling branches modifying the cut order of the child branch.
            var childCutOrders = new CutOrder[childCutSumLists.Count];
            for (int i = 0; i < childCutOrders.Length; i++)
            {
                var childCutOrder = (CutOrder) cutOrder.Clone();
                childCutOrders[i] = childCutOrder;
            }
            for (int i = 0; i < childCutSumLists.Count; i++)
            {
                SumList childCutSumList = childCutSumLists[i];
                CutOrder childCutOrder = childCutOrders[i];

                var childCutLengths = new List<decimal>(childCutSumList);
                childCutOrder.Add(new Board { Dimension = dimension, Length = boardLength }, childCutLengths);
                var childDimensionParts = new List<decimal>(dimensionParts);
                foreach (decimal childCutLength in childCutLengths)
                {
                    childDimensionParts.Remove(childCutLength);
                }
                NextBoard(childCutOrder, dimension, materialOrder, currentIndex + 1, childDimensionParts);
            }
        }
    }
}