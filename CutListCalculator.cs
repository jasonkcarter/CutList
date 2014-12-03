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
            decimal currentBoardLength = boardLength;
            var currentBoardCuts = new List<decimal>();
            bool isFirst = true;

            var remainingParts = new List<decimal>(parts);

            while (remainingParts.Count > 0)
            {
                decimal bladeWidth = 0M;
                if (isFirst)
                {
                    isFirst = false;
                }
                else
                {
                    bladeWidth += Settings.Default.BladeWidth;
                }

                decimal partLength = remainingParts.FirstOrDefault(x => x + bladeWidth <= currentBoardLength);
                if (partLength == 0M)
                {
                    cutOrder.Add(new Board {Dimension = dimension, Length = boardLength}, currentBoardCuts);
                    currentBoardCuts = new List<decimal>();
                    currentBoardLength = boardLength;
                    isFirst = true;
                    partLength = remainingParts.First(x => x <= currentBoardLength);
                }
                currentBoardCuts.Add(partLength);
                currentBoardLength -= bladeWidth + partLength;
                remainingParts.Remove(partLength);
            }
            cutOrder.Add(new Board {Dimension = dimension, Length = boardLength}, currentBoardCuts);
            return cutOrder;
        }
    }
}