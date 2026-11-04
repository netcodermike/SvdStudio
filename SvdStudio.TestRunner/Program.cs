using System;
using SvdStudio.Core;

namespace SvdStudio.TestRunner;

internal class Program
{
    private static int _passedCount = 0;
    private static int _failedCount = 0;

    static int Main(string[] args)
    {
        Console.WriteLine("====================================================");
        Console.WriteLine("       SVD STUDIO NUMERICAL VERIFICATION SUITE       ");
        Console.WriteLine("====================================================");

        Test1x1Matrix();
        Test2x2Rotation();
        Test3x3MagicSquare();
        TestTallRectangular4x2();
        TestWideRectangular2x5();
        TestRankDeficientRank1();
        TestRankDeficientRank2();
        TestHilbertMatrix();
        TestLargeRandomMatrix();
        TestParserFormats();
        TestTruncatedApproximation();
        TestPseudoInverse();
        TestEdgeCases();

        Console.WriteLine("====================================================");
        Console.WriteLine($"RESULTS: {_passedCount} PASSED, {_failedCount} FAILED.");
        Console.WriteLine("====================================================");

        return _failedCount == 0 ? 0 : 1;
    }

    private static void Assert(bool condition, string testName, string? details = null)
    {
        if (condition)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("[PASS] ");
            Console.ResetColor();
            Console.WriteLine(testName);
            _passedCount++;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("[FAIL] ");
            Console.ResetColor();
            Console.WriteLine($"{testName} - {details ?? "Assertion failed"}");
            _failedCount++;
        }
    }

    private static void VerifySvd(Matrix A, bool economy, string testName)
    {
        var svd = SvdAlgorithm.Decompose(A, economy);

        // 1. Reconstruction error: ||A - U S V^T||_F < 1e-10
        Assert(svd.ReconstructionErrorFrobenius < 1e-10,
            $"{testName} (Reconstruction)",
            $"Frobenius norm diff = {svd.ReconstructionErrorFrobenius:E4}");

        // 2. Singular values are non-negative and descending
        bool sorted = svd.SingularValues.All(value => double.IsFinite(value) && value >= 0.0);
        for (int i = 0; i < svd.SingularValues.Length - 1; i++)
        {
            if (svd.SingularValues[i] < svd.SingularValues[i + 1] - 1e-12) sorted = false;
            if (svd.SingularValues[i] < -1e-12) sorted = false;
        }
        Assert(sorted, $"{testName} (Singular values non-negative & descending)");

        // 3. Orthogonality: U^T U = I
        Assert(svd.OrthogonalityErrorU < 1e-10,
            $"{testName} (Orthogonality U)",
            $"U^T U error = {svd.OrthogonalityErrorU:E4}");

        // 4. Orthogonality: V^T V = I
        Assert(svd.OrthogonalityErrorV < 1e-10,
            $"{testName} (Orthogonality V)",
            $"V^T V error = {svd.OrthogonalityErrorV:E4}");

        // 5. Dimension checks
        if (economy)
        {
            int p = Math.Min(A.Rows, A.Columns);
            Assert(svd.U.Rows == A.Rows && svd.U.Columns == p, $"{testName} (U dimensions economy {svd.U.Rows}x{svd.U.Columns})");
            Assert(svd.S.Rows == p && svd.S.Columns == p, $"{testName} (S dimensions economy {svd.S.Rows}x{svd.S.Columns})");
            Assert(svd.VT.Rows == p && svd.VT.Columns == A.Columns, $"{testName} (VT dimensions economy {svd.VT.Rows}x{svd.VT.Columns})");
        }
        else
        {
            Assert(svd.U.Rows == A.Rows && svd.U.Columns == A.Rows, $"{testName} (U dimensions full {svd.U.Rows}x{svd.U.Columns})");
            Assert(svd.S.Rows == A.Rows && svd.S.Columns == A.Columns, $"{testName} (S dimensions full {svd.S.Rows}x{svd.S.Columns})");
            Assert(svd.VT.Rows == A.Columns && svd.VT.Columns == A.Columns, $"{testName} (VT dimensions full {svd.VT.Rows}x{svd.VT.Columns})");
        }
    }

    private static void Test1x1Matrix()
    {
        var m = new Matrix(new double[,] { { -7.5 } });
        VerifySvd(m, economy: true, "1x1 Matrix Thin");
        VerifySvd(m, economy: false, "1x1 Matrix Full");
    }

    private static void Test2x2Rotation()
    {
        double angle = Math.PI / 6.0;
        var rot = new Matrix(new double[,] {
            { Math.Cos(angle), -Math.Sin(angle) },
            { Math.Sin(angle), Math.Cos(angle) }
        });
        VerifySvd(rot, economy: true, "2x2 Rotation Thin");

        var svd = SvdAlgorithm.Decompose(rot);
        Assert(Math.Abs(svd.SingularValues[0] - 1.0) < 1e-10 && Math.Abs(svd.SingularValues[1] - 1.0) < 1e-10,
            "2x2 Rotation Singular Values == 1.0",
            $"Got {svd.SingularValues[0]}, {svd.SingularValues[1]}");
    }

    private static void Test3x3MagicSquare()
    {
        var magic = new Matrix(new double[,] {
            { 4, 9, 2 },
            { 3, 5, 7 },
            { 8, 1, 6 }
        });
        VerifySvd(magic, economy: true, "3x3 Magic Square Thin");
        VerifySvd(magic, economy: false, "3x3 Magic Square Full");
    }

    private static void TestTallRectangular4x2()
    {
        var tall = new Matrix(new double[,] {
            { 1, 2 },
            { 3, 4 },
            { 5, 6 },
            { 7, 8 }
        });
        VerifySvd(tall, economy: true, "4x2 Tall Rectangular Thin");
        VerifySvd(tall, economy: false, "4x2 Tall Rectangular Full");
    }

    private static void TestWideRectangular2x5()
    {
        var wide = new Matrix(new double[,] {
            { 1, 2, 3, 4, 5 },
            { 6, 7, 8, 9, 10 }
        });
        VerifySvd(wide, economy: true, "2x5 Wide Rectangular Thin");
        VerifySvd(wide, economy: false, "2x5 Wide Rectangular Full");
    }

    private static void TestRankDeficientRank1()
    {
        var u = new double[] { 1, 2, 3, 4 };
        var v = new double[] { 2, 1, -1, 3 };
        var m = Matrix.OuterProduct(u, v);
        VerifySvd(m, economy: true, "4x4 Rank-1 Thin");
        var svd = SvdAlgorithm.Decompose(m);
        Assert(svd.Rank == 1, $"Rank-1 Matrix Rank == 1 (detected {svd.Rank})");
    }

    private static void TestRankDeficientRank2()
    {
        var r1 = Matrix.OuterProduct(new double[] { 1, 0, 2, 1, 3 }, new double[] { 2, 1, 0, -1, 1 }) * 5.0;
        var r2 = Matrix.OuterProduct(new double[] { 0, 3, 1, -1, 2 }, new double[] { 1, 4, 2, 0, 1 }) * 2.0;
        var m = r1 + r2;
        VerifySvd(m, economy: true, "5x5 Rank-2 Thin");
        var svd = SvdAlgorithm.Decompose(m);
        Assert(svd.Rank == 2, $"Rank-2 Matrix Rank == 2 (detected {svd.Rank})");
    }

    private static void TestHilbertMatrix()
    {
        int n = 4;
        var data = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                data[i, j] = 1.0 / (i + j + 1.0);
        var hilbert = new Matrix(data);
        VerifySvd(hilbert, economy: true, "4x4 Hilbert Matrix Thin");
    }

    private static void TestLargeRandomMatrix()
    {
        var rand = Matrix.Random(20, 15, -10.0, 10.0, seed: 42);
        VerifySvd(rand, economy: true, "20x15 Random Thin");
        VerifySvd(rand, economy: false, "20x15 Random Full");
    }

    private static void TestParserFormats()
    {
        // 1. Line-by-line whitespace
        var m1 = MatrixParser.Parse("1 2 3\n4 5 6");
        Assert(m1.Rows == 2 && m1.Columns == 3 && m1[1, 2] == 6.0, "Parser: Line-by-line whitespace");

        // 2. CSV
        var m2 = MatrixParser.Parse("1, 2, 3\n4, 5, 6");
        Assert(m2.Rows == 2 && m2.Columns == 3 && m2[0, 1] == 2.0, "Parser: CSV");

        // 3. MATLAB bracketed syntax
        var m3 = MatrixParser.Parse("[1, 2, 3; 4, 5, 6]");
        Assert(m3.Rows == 2 && m3.Columns == 3 && m3[1, 0] == 4.0, "Parser: MATLAB syntax");

        // 4. JSON 2D array
        var m4 = MatrixParser.Parse("[[1, 2], [3, 4]]");
        Assert(m4.Rows == 2 && m4.Columns == 2 && m4[1, 1] == 4.0, "Parser: JSON array");
    }

    private static void TestTruncatedApproximation()
    {
        var m = Matrix.Random(6, 6, -5.0, 5.0, seed: 123);
        var svd = SvdAlgorithm.Decompose(m);

        double lastError = double.MaxValue;
        for (int k = 1; k <= 6; k++)
        {
            var trunc = svd.GetTruncatedApproximation(k);
            Assert(trunc.FrobeniusError <= lastError + 1e-12,
                $"Eckart-Young Error Monotonicity (rank {k})",
                $"Error = {trunc.FrobeniusError:E4}");
            lastError = trunc.FrobeniusError;
        }
        Assert(lastError < 1e-10, "Rank 6 Approximation matches full matrix", $"Error = {lastError:E4}");
    }

    private static void TestPseudoInverse()
    {
        var tall = new Matrix(new double[,] {
            { 1, 2 },
            { 3, 4 },
            { 5, 6 }
        });
        var svd = SvdAlgorithm.Decompose(tall);
        var pinv = svd.ComputePseudoInverse();

        // Check Penrose property 1: A * A^+ * A = A
        var prop1 = tall * pinv * tall;
        var diff1 = (prop1 - tall).FrobeniusNorm();
        Assert(diff1 < 1e-10, "Pseudo-Inverse Property 1: A A^+ A = A", $"Diff = {diff1:E4}");

        // Check Penrose property 2: A^+ * A * A^+ = A^+
        var prop2 = pinv * tall * pinv;
        var diff2 = (prop2 - pinv).FrobeniusNorm();
        Assert(diff2 < 1e-10, "Pseudo-Inverse Property 2: A^+ A A^+ = A^+", $"Diff = {diff2:E4}");
    }

    private static void AssertThrows<T>(Action action, string name) where T : Exception
    {
        try { action(); Assert(false, name, "Expected exception was not thrown."); }
        catch (T) { Assert(true, name); }
        catch (Exception ex) { Assert(false, name, $"Unexpected {ex.GetType().Name}"); }
    }

    private static void TestEdgeCases()
    {
        foreach (var shape in new[] { (1, 4), (4, 1), (3, 5), (5, 3) })
        {
            var zero = new Matrix(shape.Item1, shape.Item2);
            VerifySvd(zero, true, $"Zero {shape} thin");
            VerifySvd(zero, false, $"Zero {shape} full");
            Assert(SvdAlgorithm.Decompose(zero).Rank == 0, $"Zero {shape} rank");
            var random = Matrix.Random(shape.Item1, shape.Item2, -2, 2, seed: 19);
            VerifySvd(random, true, $"Edge {shape} thin");
            VerifySvd(random, false, $"Edge {shape} full");
        }
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            AssertThrows<ArgumentException>(() => SvdAlgorithm.Decompose(new Matrix(new double[,] { { invalid } })), "Reject non-finite input");
            AssertThrows<ArgumentOutOfRangeException>(() => SvdAlgorithm.Decompose(Matrix.Identity(2), customTolerance: invalid), "Reject non-finite tolerance");
        }
        AssertThrows<ArgumentOutOfRangeException>(() => SvdAlgorithm.Decompose(Matrix.Identity(2), customTolerance: -1), "Reject negative tolerance");
        foreach (var a in new[] {
            new Matrix(new double[,] { { 1, 2, 3 }, { 2, 4, 6 } }),
            new Matrix(new double[,] { { 1, 2 }, { 2, 4 }, { 3, 6 } }) })
        {
            var result = SvdAlgorithm.Decompose(a);
            var inverse = result.ComputePseudoInverse();
            var left = a * inverse;
            var right = inverse * a;
            Assert((left * a - a).FrobeniusNorm() < 1e-10, "Rank-deficient Penrose 1");
            Assert((right * inverse - inverse).FrobeniusNorm() < 1e-10, "Rank-deficient Penrose 2");
            Assert((left.Transpose() - left).FrobeniusNorm() < 1e-10, "Rank-deficient Penrose 3");
            Assert((right.Transpose() - right).FrobeniusNorm() < 1e-10, "Rank-deficient Penrose 4");
        }
        var sample = SvdAlgorithm.Decompose(Matrix.Random(6, 4, -3, 3, seed: 72));
        for (int k = 1; k <= 4; k++)
        {
            double tail = Math.Sqrt(sample.SingularValues.Skip(k).Sum(value => value * value));
            Assert(Math.Abs(sample.GetTruncatedApproximation(k).FrobeniusError - tail) < 1e-10, $"Eckart-Young tail formula k={k}");
        }
        var small = SvdAlgorithm.Decompose(Matrix.Identity(2) * 1e-20);
        Assert(small.Rank == 0, "Documented absolute floor in default rank tolerance");
        Assert(SvdAlgorithm.Decompose(Matrix.Identity(2) * 1e-20, customTolerance: 0).Rank == 2, "Explicit tolerance override");
    }
}
