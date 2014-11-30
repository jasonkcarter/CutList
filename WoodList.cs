using System;
using System.Collections.Generic;
using System.IO;

namespace CutList
{
    /// <summary>
    ///     Represents several sorted lists of of wood lengths, grouped by dimension.
    /// </summary>
    public class WoodList : Dictionary<string, List<decimal>>
    {
        /// <summary>
        ///     Read in the contents of a given CSV file and group them by dimension
        /// </summary>
        /// <param name="fileName">The fully-qualified file path of the CSV containing the data.</param>
        public void Load(string fileName)
        {
            // Read in grouped item values from CSV file
            using (FileStream fs = File.OpenRead(fileName))
            {
                using (var reader = new StreamReader(fs))
                {
                    string line = reader.ReadLine();
                    if (line == null)
                    {
                        throw new ApplicationException(string.Format("Empty CSV file {0}", fileName));
                    }
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] values = line.Split(',');
                        if (values.Length < 3) continue;
                        string dimension = values[0];
                        decimal length = decimal.Parse(values[1]);
                        int quantity = int.Parse(values[2]);
                        List<decimal> dimensionList;
                        if (!TryGetValue(dimension, out dimensionList))
                        {
                            dimensionList = new List<decimal>();
                            Add(dimension, dimensionList);
                        }
                        for (int i = 0; i < quantity; i++)
                        {
                            dimensionList.Add(length);
                        }
                    }
                }
            }

            // Sort the groups
            foreach (string dimension in Keys)
            {
                List<decimal> dimensionList = this[dimension];
                dimensionList.Sort();
            }
        }
    }
}