using System;
using System.Diagnostics;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;

namespace YAFC.Model
{
    public static class MarkovClusters
    {
        public static int[] ComputeMarkovClustering(Matrix<float> matrix, int size)
        {
            var sw = Stopwatch.StartNew();
            Console.WriteLine("ColumnNorm "+sw.ElapsedMilliseconds);
            var matrix2 = matrix.Clone();
            //Console.Write(matrix.ToString(100, 100));
            //Matrix<float> prev = null;
            for (var i = 0; i < 20; i++)
            {
                var matrix3 = matrix.NormalizeColumns(1);
                matrix3.CoerceZero(0.001f);
                /*if (prev != null && prev.AlmostEqual(matrix3, 0.01d))
                    break;*/
                //prev = matrix3;
                matrix3.Power(size, matrix2);
                Console.WriteLine("Expand "+sw.ElapsedMilliseconds);
                matrix2.PointwisePower(6f, matrix);
                Console.WriteLine("Inflate "+sw.ElapsedMilliseconds);
                //Console.Write(matrix.ToString(100, 100));
            }

            var lastGroup = 0;
            var result = new int[matrix.RowCount];
            for (var i = 0; i < matrix.RowCount; i++)
            {
                if (result[i] != 0)
                    continue;
                if (matrix.Storage[i, i] != 0f)
                {
                    lastGroup++;
                    var row = matrix.Row(i);
                    for (var j = 0; j < matrix.RowCount; j++)
                    {
                        if (row[j] != 0f)
                            result[j] = lastGroup;
                    }
                }
            }
            return result;
        }
    }
}