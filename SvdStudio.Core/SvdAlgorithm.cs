using System;
using System.Diagnostics;

namespace SvdStudio.Core;

/// <summary>
/// High-precision, numerically stable implementation of Singular Value Decomposition (SVD)
/// based on the Golub-Kahan-Reinsch bidiagonalization and implicit symmetric QR algorithm.
/// Supports both Full and Economy/Thin SVD for any m x n matrix (square or rectangular).
/// </summary>
public static class SvdAlgorithm
{
    private const double Epsilon = 2.2204460492503131e-16; // IEEE 754 double machine precision

    /// <summary>
    /// Computes the Singular Value Decomposition of any matrix A: A = U * S * V^T.
    /// </summary>
    /// <param name="matrix">Input matrix of size m x n.</param>
    /// <param name="economy">If true, computes Thin/Economy SVD; otherwise Full SVD.</param>
    /// <param name="customTolerance">Optional numerical tolerance for rank determination.</param>
    /// <returns>The complete SVD result.</returns>
    public static SvdResult Decompose(Matrix matrix, bool economy = true, double? customTolerance = null)
    {
        if (matrix == null) throw new ArgumentNullException(nameof(matrix));
        if (customTolerance is double tolerance && (!double.IsFinite(tolerance) || tolerance < 0))
            throw new ArgumentOutOfRangeException(nameof(customTolerance), "Tolerance must be finite and non-negative.");
        for (int row = 0; row < matrix.Rows; row++)
            for (int col = 0; col < matrix.Columns; col++)
                if (!double.IsFinite(matrix[row, col]))
                    throw new ArgumentException("Matrix entries must be finite real numbers.", nameof(matrix));

        var stopwatch = Stopwatch.StartNew();

        int m = matrix.Rows;
        int n = matrix.Columns;

        // If m < n, utilize transposition duality:
        // A = U * S * V^T  <=>  A^T = V * S^T * U^T
        // SVD of A^T gives (U_trans, S_trans, V_trans).
        // Then U = V_trans, V = U_trans, S = S_trans^T, V^T = U_trans^T.
        if (m < n)
        {
            var aTransposed = matrix.Transpose();
            // Notice: For thin SVD of A (m x n, m < n), U is m x m, S is m x m, VT is m x n.
            // For full SVD of A, U is m x m, S is m x n, VT is n x n.
            var resTransposed = DecomposeCore(aTransposed, fullSvd: !economy);

            stopwatch.Stop();

            var u_a = resTransposed.V;
            var v_a = resTransposed.U;
            var vt_a = resTransposed.U.Transpose();

            // Construct S_a
            Matrix s_a;
            int p = Math.Min(m, n);
            if (economy)
            {
                // Thin SVD: U is m x m, S is m x m, VT is m x n
                s_a = Matrix.Diagonal(resTransposed.SingularValues, m, m);
                var vt_thin = vt_a.Submatrix(0, m, 0, n);
                double tol = customTolerance ?? ComputeDefaultTolerance(m, n, resTransposed.SingularValues[0]);
                return new SvdResult(matrix, u_a, s_a, v_a.Submatrix(0, n, 0, m), vt_thin, resTransposed.SingularValues, true, tol, stopwatch.Elapsed);
            }
            else
            {
                // Full SVD: U is m x m, S is m x n, VT is n x n
                s_a = Matrix.Diagonal(resTransposed.SingularValues, m, n);
                double tol = customTolerance ?? ComputeDefaultTolerance(m, n, resTransposed.SingularValues[0]);
                return new SvdResult(matrix, u_a, s_a, v_a, vt_a, resTransposed.SingularValues, false, tol, stopwatch.Elapsed);
            }
        }

        // Here m >= n
        var result = DecomposeCore(matrix, fullSvd: !economy);
        stopwatch.Stop();

        int minDim = Math.Min(m, n);
        double maxSingular = result.SingularValues.Length > 0 ? result.SingularValues[0] : 0.0;
        double defaultTol = customTolerance ?? ComputeDefaultTolerance(m, n, maxSingular);

        if (economy)
        {
            // Thin SVD: U is m x n, S is n x n, VT is n x n
            var uThin = result.U.Submatrix(0, m, 0, n);
            var sThin = Matrix.Diagonal(result.SingularValues, n, n);
            var vThin = result.V;
            var vtThin = result.V.Transpose();

            return new SvdResult(matrix, uThin, sThin, vThin, vtThin, result.SingularValues, true, defaultTol, stopwatch.Elapsed);
        }
        else
        {
            // Full SVD: U is m x m, S is m x n, VT is n x n
            var sFull = Matrix.Diagonal(result.SingularValues, m, n);
            var vtFull = result.V.Transpose();

            return new SvdResult(matrix, result.U, sFull, result.V, vtFull, result.SingularValues, false, defaultTol, stopwatch.Elapsed);
        }
    }

    private static double ComputeDefaultTolerance(int m, int n, double maxSingular)
    {
        return Math.Max(m, n) * Epsilon * Math.Max(maxSingular, 1.0);
    }

    private sealed record RawSvd(Matrix U, Matrix V, double[] SingularValues);

    /// <summary>
    /// Core Golub-Kahan-Reinsch SVD algorithm for m >= n.
    /// Computes full U (m x m) and V (n x n).
    /// </summary>
    private static RawSvd DecomposeCore(Matrix matrix, bool fullSvd)
    {
        int m = matrix.Rows;
        int n = matrix.Columns;

        // Working copy of A
        double[,] a = matrix.ToArray2D();

        int nu = fullSvd ? m : n;
        double[,] u = new double[m, nu];
        double[,] v = new double[n, n];

        double[] s = new double[Math.Min(m + 1, n)];
        double[] e = new double[n];
        double[] work = new double[m];

        int nct = Math.Min(m - 1, n);
        int nrt = Math.Max(0, Math.Min(n - 2, m));

        // Step 1: Reduce A to bidiagonal form using Householder reflections
        for (int k = 0; k < Math.Max(nct, nrt); k++)
        {
            if (k < nct)
            {
                // Compute 2-norm of column k below diagonal
                s[k] = 0;
                for (int i = k; i < m; i++)
                {
                    s[k] = Hypotenuse(s[k], a[i, k]);
                }

                if (Math.Abs(s[k]) > 0.0)
                {
                    if (a[k, k] < 0.0) s[k] = -s[k];
                    for (int i = k; i < m; i++) a[i, k] /= s[k];
                    a[k, k] += 1.0;
                }
                s[k] = -s[k];
            }

            for (int j = k + 1; j < n; j++)
            {
                if (k < nct && Math.Abs(s[k]) > 0.0)
                {
                    // Apply left Householder reflection to column j
                    double t = 0;
                    for (int i = k; i < m; i++) t += a[i, k] * a[i, j];
                    t = -t / a[k, k];
                    for (int i = k; i < m; i++) a[i, j] += t * a[i, k];
                }
                e[j] = a[k, j];
            }

            if (k < nct)
            {
                for (int i = k; i < m; i++) u[i, k] = a[i, k];
            }

            if (k < nrt)
            {
                // Compute 2-norm of row k to the right of superdiagonal
                e[k] = 0;
                for (int i = k + 1; i < n; i++)
                {
                    e[k] = Hypotenuse(e[k], e[i]);
                }

                if (Math.Abs(e[k]) > 0.0)
                {
                    if (e[k + 1] < 0.0) e[k] = -e[k];
                    for (int i = k + 1; i < n; i++) e[i] /= e[k];
                    e[k + 1] += 1.0;
                }
                e[k] = -e[k];

                if (k + 1 < m && Math.Abs(e[k]) > 0.0)
                {
                    // Apply right Householder reflection to trailing rows
                    for (int i = k + 1; i < m; i++) work[i] = 0.0;
                    for (int j = k + 1; j < n; j++)
                    {
                        for (int i = k + 1; i < m; i++) work[i] += e[j] * a[i, j];
                    }
                    for (int j = k + 1; j < n; j++)
                    {
                        double t = -e[j] / e[k + 1];
                        for (int i = k + 1; i < m; i++) a[i, j] += t * work[i];
                    }
                }

                for (int i = k + 1; i < n; i++) v[i, k] = e[i];
            }
        }

        // Set up the final bidiagonal matrix of order p
        int p = Math.Min(n, m + 1);
        if (nct < n) s[nct] = a[nct, nct];
        if (m < p) s[p - 1] = 0.0;
        if (nrt + 1 < p) e[nrt] = a[nrt, p - 1];
        e[p - 1] = 0.0;

        // Step 2: Accumulate left transformations U
        for (int j = nct; j < nu; j++)
        {
            for (int i = 0; i < m; i++) u[i, j] = 0.0;
            if (j < m) u[j, j] = 1.0;
        }

        for (int k = nct - 1; k >= 0; k--)
        {
            if (Math.Abs(s[k]) > 0.0)
            {
                for (int j = k + 1; j < nu; j++)
                {
                    double t = 0;
                    for (int i = k; i < m; i++) t += u[i, k] * u[i, j];
                    t = -t / u[k, k];
                    for (int i = k; i < m; i++) u[i, j] += t * u[i, k];
                }
                for (int i = k; i < m; i++) u[i, k] = -u[i, k];
                u[k, k] = 1.0 + u[k, k];
                for (int i = 0; i < k - 1; i++) u[i, k] = 0.0;
            }
            else
            {
                for (int i = 0; i < m; i++) u[i, k] = 0.0;
                u[k, k] = 1.0;
            }
        }

        // Step 3: Accumulate right transformations V
        for (int k = n - 1; k >= 0; k--)
        {
            if (k < nrt && Math.Abs(e[k]) > 0.0)
            {
                for (int j = k + 1; j < n; j++)
                {
                    double t = 0;
                    for (int i = k + 1; i < n; i++) t += v[i, k] * v[i, j];
                    t = -t / v[k + 1, k];
                    for (int i = k + 1; i < n; i++) v[i, j] += t * v[i, k];
                }
            }
            for (int i = 0; i < n; i++) v[i, k] = 0.0;
            v[k, k] = 1.0;
        }

        // Step 4: Main QR iteration loop for the bidiagonal matrix
        int pp = p - 1;
        int iter = 0;
        double eps = Math.Pow(2.0, -52.0);
        double tiny = Math.Pow(2.0, -966.0);

        while (p > 0)
        {
            int k;
            int kase;

            // Check for maximum iterations (guard against non-convergence)
            if (iter > 150 * n)
            {
                throw new InvalidOperationException("SVD failed to converge within the iteration limit.");
            }

            // Test for negligible elements in s and e
            for (k = p - 2; k >= -1; k--)
            {
                if (k == -1) break;
                if (Math.Abs(e[k]) <= tiny + eps * (Math.Abs(s[k]) + Math.Abs(s[k + 1])))
                {
                    e[k] = 0.0;
                    break;
                }
            }

            if (k == p - 2)
            {
                kase = 4; // Convergence of singular value p-1
            }
            else
            {
                int ks;
                for (ks = p - 1; ks >= k; ks--)
                {
                    if (ks == k) break;
                    double t = (ks != p ? Math.Abs(e[ks]) : 0.0) + (ks != k + 1 ? Math.Abs(e[ks - 1]) : 0.0);
                    if (Math.Abs(s[ks]) <= tiny + eps * t)
                    {
                        s[ks] = 0.0;
                        break;
                    }
                }

                if (ks == k)
                {
                    kase = 3; // Perform QR step on submatrix [k+1, p-1]
                }
                else if (ks == p - 1)
                {
                    kase = 1; // Deflate negligible element at bottom
                }
                else
                {
                    kase = 2; // Split at ks
                    k = ks;
                }
            }
            k++;

            // Execute corresponding case
            switch (kase)
            {
                // Case 1: Deflate negligible s[p-1]
                case 1:
                    {
                        double f = e[p - 2];
                        e[p - 2] = 0.0;
                        for (int j = p - 2; j >= k; j--)
                        {
                            double t = Hypotenuse(s[j], f);
                            double cs = s[j] / t;
                            double sn = f / t;
                            s[j] = t;
                            if (j != k)
                            {
                                f = -sn * e[j - 1];
                                e[j - 1] = cs * e[j - 1];
                            }
                            for (int i = 0; i < n; i++)
                            {
                                t = cs * v[i, j] + sn * v[i, p - 1];
                                v[i, p - 1] = -sn * v[i, j] + cs * v[i, p - 1];
                                v[i, j] = t;
                            }
                        }
                    }
                    break;

                // Case 2: Split at negligible s[k]
                case 2:
                    {
                        double f = e[k - 1];
                        e[k - 1] = 0.0;
                        for (int j = k; j < p; j++)
                        {
                            double t = Hypotenuse(s[j], f);
                            double cs = s[j] / t;
                            double sn = f / t;
                            s[j] = t;
                            f = -sn * e[j];
                            e[j] = cs * e[j];
                            for (int i = 0; i < m; i++)
                            {
                                t = cs * u[i, j] + sn * u[i, k - 1];
                                u[i, k - 1] = -sn * u[i, j] + cs * u[i, k - 1];
                                u[i, j] = t;
                            }
                        }
                    }
                    break;

                // Case 3: Perform QR step with Wilkinson shift
                case 3:
                    {
                        // Calculate the shift from the bottom 2x2 of the bidiagonal matrix
                        double scale = Math.Max(Math.Max(Math.Max(Math.Max(
                            Math.Abs(s[p - 1]), Math.Abs(s[p - 2])), Math.Abs(e[p - 2])),
                            Math.Abs(s[k])), Math.Abs(e[k]));

                        double sp = s[p - 1] / scale;
                        double spm1 = s[p - 2] / scale;
                        double epm1 = e[p - 2] / scale;
                        double sk = s[k] / scale;
                        double ek = e[k] / scale;
                        double b = ((spm1 + sp) * (spm1 - sp) + epm1 * epm1) / 2.0;
                        double c = (sp * epm1) * (sp * epm1);
                        double shift = 0.0;
                        if (Math.Abs(b) > 0.0 || Math.Abs(c) > 0.0)
                        {
                            shift = Math.Sqrt(b * b + c);
                            if (b < 0.0) shift = -shift;
                            shift = c / (b + shift);
                        }
                        double f = (sk + sp) * (sk - sp) + shift;
                        double g = sk * ek;

                        // Chase the bulge down the diagonal
                        for (int j = k; j < p - 1; j++)
                        {
                            double t = Hypotenuse(f, g);
                            double cs = f / t;
                            double sn = g / t;
                            if (j != k) e[j - 1] = t;

                            f = cs * s[j] + sn * e[j];
                            e[j] = cs * e[j] - sn * s[j];
                            g = sn * s[j + 1];
                            s[j + 1] = cs * s[j + 1];

                            for (int i = 0; i < n; i++)
                            {
                                t = cs * v[i, j] + sn * v[i, j + 1];
                                v[i, j + 1] = -sn * v[i, j] + cs * v[i, j + 1];
                                v[i, j] = t;
                            }

                            t = Hypotenuse(f, g);
                            cs = f / t;
                            sn = g / t;
                            s[j] = t;
                            f = cs * e[j] + sn * s[j + 1];
                            s[j + 1] = -sn * e[j] + cs * s[j + 1];
                            g = sn * e[j + 1];
                            e[j + 1] = cs * e[j + 1];

                            if (j < m - 1)
                            {
                                for (int i = 0; i < m; i++)
                                {
                                    t = cs * u[i, j] + sn * u[i, j + 1];
                                    u[i, j + 1] = -sn * u[i, j] + cs * u[i, j + 1];
                                    u[i, j] = t;
                                }
                            }
                        }
                        e[p - 2] = f;
                        iter++;
                    }
                    break;

                // Case 4: Convergence of singular value
                case 4:
                    {
                        // Make the singular value positive
                        if (s[k] <= 0.0)
                        {
                            s[k] = (s[k] < 0.0 ? -s[k] : 0.0);
                            for (int i = 0; i <= pp; i++) v[i, k] = -v[i, k];
                        }

                        // Order the singular values descending
                        while (k < pp)
                        {
                            if (s[k] >= s[k + 1]) break;

                            double t = s[k];
                            s[k] = s[k + 1];
                            s[k + 1] = t;

                            if (k < n - 1)
                            {
                                for (int i = 0; i < n; i++)
                                {
                                    t = v[i, k];
                                    v[i, k] = v[i, k + 1];
                                    v[i, k + 1] = t;
                                }
                            }

                            if (k < m - 1)
                            {
                                for (int i = 0; i < m; i++)
                                {
                                    t = u[i, k];
                                    u[i, k] = u[i, k + 1];
                                    u[i, k + 1] = t;
                                }
                            }
                            k++;
                        }
                        iter = 0;
                        p--;
                    }
                    break;
            }
        }

        // Extract final singular values array of size min(m, n)
        int minMN = Math.Min(m, n);
        double[] finalS = new double[minMN];
        Array.Copy(s, finalS, minMN);

        // Final sanity check: guarantee non-negative singular values and strictly sorted
        for (int i = 0; i < minMN; i++)
        {
            if (finalS[i] < 0.0)
            {
                finalS[i] = -finalS[i];
                for (int r = 0; r < m; r++) u[r, i] = -u[r, i];
            }
        }

        return new RawSvd(new Matrix(u), new Matrix(v), finalS);
    }

    /// <summary>
    /// Computes sqrt(a^2 + b^2) without underflow/overflow.
    /// </summary>
    private static double Hypotenuse(double a, double b)
    {
        double r;
        if (Math.Abs(a) > Math.Abs(b))
        {
            r = b / a;
            return Math.Abs(a) * Math.Sqrt(1 + r * r);
        }
        if (b != 0)
        {
            r = a / b;
            return Math.Abs(b) * Math.Sqrt(1 + r * r);
        }
        return 0.0;
    }
}
