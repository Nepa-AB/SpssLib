﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Cone;
using SpssLib.DataReader;
using SpssLib.FileParser;
using SpssLib.SpssDataset;

namespace Test.SpssLib
{
    [Describe(typeof(SpssReader))]
    public class TestSpssReader
    {
        public void TestReadFile()
        {
            FileStream fileStream = new FileStream("TestFiles/test.sav", FileMode.Open, FileAccess.Read,
                FileShare.Read, 2048*10, FileOptions.SequentialScan);

            int[] varenieValues = {1, 2 ,1};
            string[] streetValues = { "Landsberger Straße", "Fröbelplatz", "Bayerstraße" };

            int varCount;
            int rowCount;
            try
            {
                ReadData(fileStream, out varCount, out rowCount,
                    new Dictionary<int, Action<int, Variable>>
                    {
                        {0, (i, variable) =>
                        {
                            Check.That(
                                () => "varaible ñ" == variable.Label, // "Label mismatch"
                                () => DataType.Numeric == variable.Type); // "First file variable should be  a Number"
                        }},
                        {1, (i, variable) =>
                        {
                            Check.That(
                                () => "straße" == variable.Label, // "Label mismatch"
                                () => DataType.Text == variable.Type); // "Second file variable should be  a text"
                        }}
                    },
                    new Dictionary<int, Action<int, int, Variable, object>>
                    {
                        {0, (r, c, variable, value) =>
                        {   // All numeric values are doubles
                            Check.That(() => value is double); // "First row variable should be a Number"
                            double v = (double) value;
                            Check.That(() => varenieValues[r] == v); // "Int value is different"
                        }},
                        {1, (r, c, variable, value) =>
                        {
                            Check.That(() => value is string); // "Second row variable should be  a text"
                            string v = (string) value;
                            Check.That(() => streetValues[r] == v); // "String value is different"
                        }}
                    });
            }
            finally
            {
                fileStream.Close();
            }

            Check.That(
                () => varCount == 3, // "Variable count does not match");
                () => rowCount == 3); // "Rows count does not match");
        }

        public void TestEmptyStream()
        {
            int varCount;
            int rowCount;
            Check.Exception<SpssFileFormatException>(() =>
                ReadData(new MemoryStream(new byte[0]), out varCount, out rowCount));
        }

        public void TestReadMissingValuesAsNull()
        {
            FileStream fileStream = new FileStream("TestFiles/MissingValues.sav", FileMode.Open, FileAccess.Read,
                FileShare.Read, 2048 * 10, FileOptions.SequentialScan);

            double?[][] varValues =
            {
                new double?[]{ 0, 1, 2, 3, 4, 5, 6, 7 }, // No missing values
                new double?[]{ 0, null, 2, 3, 4, 5, 6, 7 }, // One mssing value
                new double?[]{ 0, null, null, 3, 4, 5, 6, 7 }, // Two missing values
                new double?[]{ 0, null, null, null, 4, 5, 6, 7 }, // Three missing values
                new double?[]{ 0, null, null, null, null, null, 6, 7 }, // Range
                new double?[]{ 0, null, null, null, null, null, 6, null }, // Range & one value
            };

            Action<int, int, Variable, object> rowCheck = (r, c, variable, value) =>
            {
                Check.That(() => varValues[c][r] == value as double?); // $"Wrong value: row {r}, variable {c}"
            };


            try
            {
                int varCount, rowCount;
                ReadData(fileStream, out varCount, out rowCount, new Dictionary<int, Action<int, Variable>>
                    {
                        {0, (i, variable) => Check.That(() => MissingValueType.NoMissingValues == variable.MissingValueType)},
                        {1, (i, variable) => Check.That(() => MissingValueType.OneDiscreteMissingValue == variable.MissingValueType)},
                        {2, (i, variable) => Check.That(() => MissingValueType.TwoDiscreteMissingValue == variable.MissingValueType)},
                        {3, (i, variable) => Check.That(() => MissingValueType.ThreeDiscreteMissingValue == variable.MissingValueType)},
                        {4, (i, variable) => Check.That(() => MissingValueType.Range == variable.MissingValueType)},
                        {5, (i, variable) => Check.That(() => MissingValueType.RangeAndDiscrete == variable.MissingValueType)},
                    },
                    new Dictionary<int, Action<int, int, Variable, object>>
                    {
                        {0, rowCheck},
                        {1, rowCheck},
                        {2, rowCheck},
                        {3, rowCheck},
                        {4, rowCheck},
                        {5, rowCheck},
                        {6, rowCheck},
                    });
            }
            finally
            {
                fileStream.Close();
            }
        }

        internal static void ReadData(Stream fileStream, out int varCount, out int rowCount) =>
            ReadData(fileStream, out varCount, out rowCount, null, null);

        internal static void ReadData(Stream fileStream, out int varCount, out int rowCount,
            IDictionary<int, Action<int, Variable>> variableValidators, IDictionary<int, Action<int, int, Variable, object>> valueValidators)
        {
            SpssReader spssDataset = new SpssReader(fileStream);

            varCount = 0;
            rowCount = 0;

            var variables = spssDataset.Variables;
            foreach (var variable in variables)
            {
                Debug.WriteLine("{0} - {1}", variable.Name, variable.Label);
                foreach (KeyValuePair<double, string> label in variable.ValueLabels)
                {
                    Debug.WriteLine(" {0} - {1}", label.Key, label.Value);
                }

                Action<int, Variable> checkVariable;
                if (variableValidators != null && variableValidators.TryGetValue(varCount, out checkVariable))
                {
                    checkVariable(varCount, variable);
                }

                varCount++;
            }

            foreach (var record in spssDataset.Records)
            {
                var varIndex = 0;
                foreach (var variable in variables)
                {
                    Debug.Write(variable.Name);
                    Debug.Write(':');
                    var value = record.GetValue(variable);
                    Debug.Write(value);
                    Debug.Write('\t');

                    Action<int, int, Variable, object> checkValue;
                    if (valueValidators != null && valueValidators.TryGetValue(varIndex, out checkValue))
                    {
                        checkValue(rowCount, varIndex, variable, value);
                    }

                    varIndex++;
                }
                Debug.WriteLine("");

                rowCount++;
            }
        }
    }
}
