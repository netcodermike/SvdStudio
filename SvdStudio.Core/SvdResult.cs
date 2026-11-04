using System;
using System.Collections.Generic;

namespace SvdStudio.Core;

/// <summary>
/// Contains the complete results of a Singular Value Decomposition: A = U * S * V^T
/// </summary>
public sealed class SvdResult
{
    public Matrix OriginalMatrix { get; }
    public Matrix U { get; }
    public Matrix S { get; }
    public Matrix V { get; }
    public Matrix VT { get; }
    public double[] SingularValues { get; }
    public bool IsEconomy { get; }
    public int Rank { get; }
    public double ConditionNumber { get; }
    public double ReconstructionErrorFrobenius { get; }
    public double ReconstructionErrorMax { get; }
    public double OrthogonalityErrorU { get; }
    public double OrthogonalityErrorV { get; }
    public TimeSpan ComputeTime { get; }
    public double Tolerance { get; }

    public SvdResult(
        Matrix originalMatrix,
        Matrix u,
        Matrix s,
        Matrix v,
        Matrix vt,
        double[] singularValues,
        bool isEconomy,
        double tolerance,
        TimeSpan computeTime)
    {
        OriginalMatrix = originalMatrix ?? throw new ArgumentNullException(nameof(originalMatrix));
        U = u ?? throw new ArgumentNullException(nameof(u));
        S = s ?? throw new ArgumentNullException(nameof(s));
        V = v ?? throw new ArgumentNullException(nameof(v));
        VT = vt ?? throw new ArgumentNullException(nameof(vt));
        SingularValues = singularValues ?? throw new ArgumentNullException(nameof(singularValues));
        IsEconomy = isEconomy;
        Tolerance = tolerance;
        ComputeTime = computeTime;

        // Effective numerical rank
        int rank = 0;
        foreach (var sv in SingularValues)
        {
            if (sv > tolerance) rank++;
        }
        Rank = rank;

        // Condition number: sigma_max / sigma_min
        if (SingularValues.Length == 0)
        {
            ConditionNumber = 1.0;
        }
        else
        {
            double max = SingularValues[0];
            double min = SingularValues[^1];
            ConditionNumber = min > tolerance ? max / min : double.PositiveInfinity;
        }

        // Reconstruction diagnostics: A_rec = U * S * VT
        var reconstructed = U * S * VT;
        var diff = OriginalMatrix - reconstructed;
        ReconstructionErrorFrobenius = diff.FrobeniusNorm();
        ReconstructionErrorMax = diff.MaxAbsoluteValue();

        // Orthogonality diagnostics: U^T * U should be I, V^T * V should be I
        var utU = U.Transpose() * U;
        var eyeU = Matrix.Identity(utU.Rows);
        OrthogonalityErrorU = (utU - eyeU).FrobeniusNorm();

        var vtV = V.Transpose() * V;
        var eyeV = Matrix.Identity(vtV.Rows);
        OrthogonalityErrorV = (vtV - eyeV).FrobeniusNorm();
    }

    /// <summary>
    /// Computes the Eckart-Young-Mirsky best rank-k approximation A_k = sum_{i=1}^k sigma_i * u_i * v_i^T
    /// </summary>
    public TruncatedSvdResult GetTruncatedApproximation(int targetRank)
    {
        int p = SingularValues.Length;
        int k = Math.Clamp(targetRank, 1, p);

        int m = OriginalMatrix.Rows;
        int n = OriginalMatrix.Columns;

        // Reconstruct Ak using first k components
        var akData = new double[m * n];
        for (int r = 0; r < k; r++)
        {
            double sigma = SingularValues[r];
            var uCol = U.GetColumn(r);
            var vCol = V.GetColumn(r);

            for (int i = 0; i < m; i++)
            {
                double ui = uCol[i];
                int offset = i * n;
                for (int j = 0; j < n; j++)
                {
                    akData[offset + j] += sigma * ui * vCol[j];
                }
            }
        }

        var ak = new Matrix(m, n, akData);
        var diff = OriginalMatrix - ak;
        double frobeniusError = diff.FrobeniusNorm();
        double originalNorm = OriginalMatrix.FrobeniusNorm();
        double relativeError = originalNorm > 1e-14 ? frobeniusError / originalNorm : 0.0;

        // Energy (variance) retained: sum_{i=1}^k sigma_i^2 / sum_{i=1}^p sigma_i^2
        double totalEnergy = 0.0;
        double keptEnergy = 0.0;
        for (int i = 0; i < p; i++)
        {
            double sq = SingularValues[i] * SingularValues[i];
            totalEnergy += sq;
            if (i < k) keptEnergy += sq;
        }

        double energyPercent = totalEnergy > 1e-15 ? (keptEnergy / totalEnergy) * 100.0 : 100.0;

        // Slice Uk, Sk, VkT
        var uk = U.Submatrix(0, U.Rows, 0, k);
        var sk = Matrix.Diagonal(SingularValues[..k], k, k);
        var vkt = VT.Submatrix(0, k, 0, VT.Columns);

        return new TruncatedSvdResult(k, ak, uk, sk, vkt, frobeniusError, relativeError, energyPercent);
    }

    /// <summary>
    /// Computes the individual rank-1 components A_i = sigma_i * u_i * v_i^T.
    /// </summary>
    public IReadOnlyList<Matrix> GetRank1Components()
    {
        var list = new List<Matrix>(SingularValues.Length);
        for (int i = 0; i < SingularValues.Length; i++)
        {
            var uCol = U.GetColumn(i);
            var vCol = V.GetColumn(i);
            var outer = Matrix.OuterProduct(uCol, vCol) * SingularValues[i];
            list.Add(outer);
        }
        return list;
    }

    /// <summary>
    /// Computes the cumulative energy captured by each singular value.
    /// </summary>
    public double[] GetCumulativeEnergyPercentages()
    {
        int p = SingularValues.Length;
        if (p == 0) return Array.Empty<double>();

        double total = 0.0;
        foreach (var sv in SingularValues) total += sv * sv;

        var result = new double[p];
        if (total < 1e-15)
        {
            Array.Fill(result, 100.0);
            return result;
        }

        double running = 0.0;
        for (int i = 0; i < p; i++)
        {
            running += SingularValues[i] * SingularValues[i];
            result[i] = (running / total) * 100.0;
        }
        return result;
    }

    /// <summary>
    /// Computes the Moore-Penrose pseudo-inverse A^+ = sum_{i=1}^r (1 / sigma_i) * v_i * u_i^T.
    /// </summary>
    public Matrix ComputePseudoInverse()
    {
        int m = OriginalMatrix.Rows;
        int n = OriginalMatrix.Columns;
        var pinvData = new double[n * m];

        for (int k = 0; k < SingularValues.Length; k++)
        {
            double sigma = SingularValues[k];
            if (sigma > Tolerance)
            {
                double invSigma = 1.0 / sigma;
                var uCol = U.GetColumn(k);
                var vCol = V.GetColumn(k);

                for (int i = 0; i < n; i++)
                {
                    double vi = vCol[i];
                    int offset = i * m;
                    for (int j = 0; j < m; j++)
                    {
                        pinvData[offset + j] += invSigma * vi * uCol[j];
                    }
                }
            }
        }

        return new Matrix(n, m, pinvData);
    }
}

/// <summary>
/// Represents the result of a truncated SVD (rank-k approximation).
/// </summary>
public sealed class TruncatedSvdResult
{
    public int Rank { get; }
    public Matrix Approximation { get; }
    public Matrix Uk { get; }
    public Matrix Sk { get; }
    public Matrix VkT { get; }
    public double FrobeniusError { get; }
    public double RelativeError { get; }
    public double EnergyPercentage { get; }

    public TruncatedSvdResult(
        int rank,
        Matrix approximation,
        Matrix uk,
        Matrix sk,
        Matrix vkt,
        double frobeniusError,
        double relativeError,
        double energyPercentage)
    {
        Rank = rank;
        Approximation = approximation;
        Uk = uk;
        Sk = sk;
        VkT = vkt;
        FrobeniusError = frobeniusError;
        RelativeError = relativeError;
        EnergyPercentage = energyPercentage;
    }
}
