using System;
using System.Collections.Generic;

namespace SvdStudio.Core;

public sealed record MatrixPreset(string Name, string Category, string Description, Func<Matrix> Generator);

/// <summary>
/// Curated presets showcasing various geometric, structural, and numerical properties in SVD.
/// </summary>
public static class PresetMatrices
{
    public static IReadOnlyList<MatrixPreset> Presets { get; } = new List<MatrixPreset>
    {
        // 2x2 Geometric cases
        new(
            "2x2 Pure Rotation (45°)",
            "Geometric (2x2)",
            "Orthogonal rotation matrix where singular values are exactly [1, 1].",
            () =>
            {
                double angle = Math.PI / 4.0;
                double c = Math.Cos(angle);
                double s = Math.Sin(angle);
                return new Matrix(new double[,] { { c, -s }, { s, c } });
            }),

        new(
            "2x2 Horizontal Shear",
            "Geometric (2x2)",
            "Transforms unit square into a parallelogram. Exhibits unequal singular values.",
            () => new Matrix(new double[,] { { 1, 1.5 }, { 0, 1 } })),

        new(
            "2x2 Non-Symmetric Ellipse",
            "Geometric (2x2)",
            "Stretches and rotates the 2D plane.",
            () => new Matrix(new double[,] { { 3, 1 }, { 1, 2 } })),

        // Classic Square Cases
        new(
            "3x3 Lo Shu Magic Square",
            "Classic Square",
            "Famous 3x3 magic square with rows/cols/diagonals summing to 15.",
            () => new Matrix(new double[,] {
                { 4, 9, 2 },
                { 3, 5, 7 },
                { 8, 1, 6 }
            })),

        new(
            "3x3 Identity Matrix",
            "Classic Square",
            "Identity matrix where U = V = I, and all singular values equal 1.",
            () => Matrix.Identity(3)),

        new(
            "4x4 Pascal Matrix",
            "Classic Square",
            "Symmetric positive definite matrix formed by binomial coefficients.",
            () =>
            {
                int n = 4;
                var data = new double[n, n];
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        data[i, j] = Binomial(i + j, i);
                    }
                }
                return new Matrix(data);
            }),

        // Rectangular Cases
        new(
            "4x2 Tall Rectangular",
            "Rectangular",
            "Over-determined system (m > n). Full SVD produces 4x4 U and 2x2 V.",
            () => new Matrix(new double[,] {
                { 1, 2 },
                { 3, 4 },
                { 5, 6 },
                { 7, 8 }
            })),

        new(
            "2x5 Wide Rectangular",
            "Rectangular",
            "Under-determined system (m < n). Full SVD produces 2x2 U and 5x5 V.",
            () => new Matrix(new double[,] {
                { 1, 2, 3, 4, 5 },
                { 6, 7, 8, 9, 10 }
            })),

        new(
            "5x3 Rectangular",
            "Rectangular",
            "5x3 matrix representing multivariate data.",
            () => new Matrix(new double[,] {
                { 2, 0, 1 },
                { 1, 3, 0 },
                { 0, 1, 4 },
                { 2, 2, 2 },
                { 1, 0, 3 }
            })),

        // Numerical / Rank-Deficient
        new(
            "4x4 Rank-1 Matrix (Outer Product)",
            "Rank Deficient",
            "Outer product of vectors [1, 2, 3, 4] and [2, 1, -1, 3]. Only 1 non-zero singular value.",
            () => Matrix.OuterProduct(new double[] { 1, 2, 3, 4 }, new double[] { 2, 1, -1, 3 })),

        new(
            "5x5 Rank-2 Matrix",
            "Rank Deficient",
            "Sum of two rank-1 matrices. Exactly 2 non-zero singular values; others are zero.",
            () =>
            {
                var r1 = Matrix.OuterProduct(new double[] { 1, 0, 2, 1, 3 }, new double[] { 2, 1, 0, -1, 1 }) * 5.0;
                var r2 = Matrix.OuterProduct(new double[] { 0, 3, 1, -1, 2 }, new double[] { 1, 4, 2, 0, 1 }) * 2.0;
                return r1 + r2;
            }),

        new(
            "4x4 Hilbert Matrix (Ill-Conditioned)",
            "Ill-Conditioned",
            "H[i,j] = 1/(i+j+1). Condition number grows exponentially with size.",
            () =>
            {
                int n = 4;
                var data = new double[n, n];
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        data[i, j] = 1.0 / (i + j + 1.0);
                return new Matrix(data);
            }),

        new(
            "4x4 Vandermonde Matrix",
            "Special Forms",
            "Columns are powers of vector [1, 2, 3, 4]. Common in polynomial fitting.",
            () =>
            {
                double[] x = { 1, 2, 3, 4 };
                int n = 4;
                var data = new double[n, n];
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        data[i, j] = Math.Pow(x[i], j);
                return new Matrix(data);
            })
    };

    private static double Binomial(int n, int k)
    {
        if (k < 0 || k > n) return 0;
        if (k == 0 || k == n) return 1;
        double c = 1;
        for (int i = 1; i <= k; i++)
        {
            c = c * (n - (k - i)) / i;
        }
        return c;
    }

    /// <summary>
    /// Generates a random matrix with forced target rank.
    /// </summary>
    public static Matrix GenerateRandomWithRank(int rows, int cols, int rank, double min = -5.0, double max = 5.0, int? seed = null)
    {
        rank = Math.Clamp(rank, 1, Math.Min(rows, cols));
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();

        var result = Matrix.Zeros(rows, cols);
        for (int r = 0; r < rank; r++)
        {
            var u = new double[rows];
            var v = new double[cols];
            for (int i = 0; i < rows; i++) u[i] = Math.Round(min + rng.NextDouble() * (max - min), 2);
            for (int j = 0; j < cols; j++) v[j] = Math.Round(min + rng.NextDouble() * (max - min), 2);

            result += Matrix.OuterProduct(u, v);
        }

        return result;
    }
}
