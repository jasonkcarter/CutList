using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

            var cutOrders = from dimension in parts.Keys.AsParallel()
                let materialOrders = new Permutations<decimal>(materials[dimension], GenerateOption.WithoutRepetition)
                let partOrders = new Permutations<decimal>(parts[dimension], GenerateOption.WithoutRepetition)
                from materialOrder in materialOrders.AsParallel()
                from partOrder in partOrders.AsParallel()
                select new {Dimension = dimension, MaterialOrder = materialOrder, PartOrder = partOrder};

            cutOrders.ForAll(x => BuildCutOrder(x.Dimension, x.MaterialOrder, x.PartOrder));
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

        private async Task BuildCutOrder(string dimension, IList<decimal> materialOrder, IList<decimal> partOrder)
        {
            await Task.Factory.StartNew(() =>
            {
                bool isCutOrderGood = true;
                var cutOrder = new CutOrder();
                int materialOrderIndex = 0;
                int partOrderIndex = 0;
                while (isCutOrderGood && materialOrderIndex < materialOrder.Count)
                {
                    var board = new Board
                    {
                        Dimension = dimension,
                        Length = materialOrder[materialOrderIndex]
                    };
                    var boardCutList = new List<decimal>();
                    decimal boardCutLength = 0M;
                    while (partOrderIndex < partOrder.Count)
                    {
                        decimal partLength = partOrder[partOrderIndex];
                        decimal sawWidth = boardCutList.Count == 0 ? 0M : Settings.Default.BladeWidth;

                        // The next part fits on the current board, so add it to the list and continue
                        if ((boardCutLength + partLength + sawWidth) < board.Length)
                        {
                            boardCutLength += partLength + sawWidth;
                            boardCutList.Add(partLength);
                            partOrderIndex++;
                            continue;
                        }

                        // The next part needs to come from a new board

                        // Add the current board's cut list to the oveerall cut list
                        cutOrder.Add(board, boardCutList);

                        // Move to the next board
                        materialOrderIndex++;
                        decimal boardLength = materialOrder[materialOrderIndex];

                        // If we've run out of boards, or the next board is too short for the part we need, throw out the cut order.
                        if (partOrderIndex >= partOrder.Count || boardLength < partLength)
                        {
                            isCutOrderGood = false;
                            break;
                        }

                        // Set up the next board and its new cut list that includes just the current part
                        board = new Board {Dimension = dimension, Length = boardLength};
                        boardCutLength = boardLength;
                        boardCutList = new List<decimal> {boardCutLength};
                        partOrderIndex++;
                    }
                    materialOrderIndex++;
                }

                // If the cut order was no good, then just move on to the next
                if (!isCutOrderGood)
                {
                    return;
                }

                if (CutOrderFound != null)
                {
                    CutOrderFound(this, cutOrder);
                }
            });
        }
    }
}