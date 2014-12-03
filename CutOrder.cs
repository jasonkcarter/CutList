using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CutList
{
    /// <summary>
    ///     Represents a specific order used to cut the required parts list in.
    /// </summary>
    public class CutOrder : Dictionary<Board, List<decimal>>, ICloneable
    {
        public object Clone()
        {
            var cutOrder = new CutOrder();
            foreach (Board board in Keys)
            {
                var boardCopy = new Board {Dimension = board.Dimension, Length = board.Length};
                var cutListCopy = new List<decimal>(this[board]);
                cutOrder.Add(boardCopy, cutListCopy);
            }

            return cutOrder;
        }

        /// <summary>
        ///     Calculates the total length of wasted wood in this cut order.
        /// </summary>
        /// <returns>The length of wasted wood, in linear inches</returns>
        public decimal ComputeWaste()
        {
            decimal wasteLength =
                (from board in Keys
                    let boardCuts = this[board]
                    let cutsLength = boardCuts.Sum()
                    select board.Length - cutsLength
                    ).Sum();
            return wasteLength;
        }

        public override string ToString()
        {
            var csv = new StringBuilder("Dimension,Board #,Board length (in),Cut length (in)");
            int boardNumber = 0;
            string previousDimension = null;
            foreach (Board board in Keys)
            {
                if (board.Dimension != previousDimension)
                {
                    boardNumber = 0;
                }
                boardNumber++;
                foreach (decimal length in this[board])
                {
                    csv.AppendFormat("\r\n{0},{1},{2},{3}", board.Dimension, boardNumber, board.Length, length);
                }
                previousDimension = board.Dimension;
            }
            return csv.ToString();
        }
    }
}