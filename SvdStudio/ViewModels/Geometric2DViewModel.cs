using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using SvdStudio.Common;
using SvdStudio.Core;
using Matrix = SvdStudio.Core.Matrix;

namespace SvdStudio.ViewModels;

public sealed class Geometric2DViewModel : ObservableObject
{
    private readonly Matrix _matrix;
    private readonly SvdResult _svd;

    public bool Is2DAvailable { get; }
    public string StatusMessage { get; }

    public double Sigma1 { get; }
    public double Sigma2 { get; }
    public string VectorV1Text { get; }
    public string VectorV2Text { get; }
    public string VectorU1Text { get; }
    public string VectorU2Text { get; }
    public string AreaScalingText { get; }

    // Coordinates in mathematical 2D space (x, y)
    public PointCollection DomainCirclePoints { get; } = new();
    public PointCollection TransformedEllipsePoints { get; } = new();

    public Point V1Point { get; }
    public Point V2Point { get; }
    public Point TransformedU1Point { get; } // sigma1 * u1
    public Point TransformedU2Point { get; } // sigma2 * u2

    public double MaxCoordinateExtent { get; }

    public Geometric2DViewModel(Matrix matrix, SvdResult svd)
    {
        _matrix = matrix;
        _svd = svd;

        if (matrix.Rows == 2 && matrix.Columns == 2)
        {
            Is2DAvailable = true;
            StatusMessage = "Full 2D Transformation (2x2 Matrix)";

            Sigma1 = svd.SingularValues.Length > 0 ? svd.SingularValues[0] : 1.0;
            Sigma2 = svd.SingularValues.Length > 1 ? svd.SingularValues[1] : 0.0;

            var v1 = svd.V.GetColumn(0);
            var v2 = svd.V.GetColumn(1);
            var u1 = svd.U.GetColumn(0);
            var u2 = svd.U.GetColumn(1);

            V1Point = new Point(v1[0], v1[1]);
            V2Point = new Point(v2[0], v2[1]);

            TransformedU1Point = new Point(Sigma1 * u1[0], Sigma1 * u1[1]);
            TransformedU2Point = new Point(Sigma2 * u2[0], Sigma2 * u2[1]);

            VectorV1Text = $"v₁ = [{v1[0]:F3}, {v1[1]:F3}]ᵀ";
            VectorV2Text = $"v₂ = [{v2[0]:F3}, {v2[1]:F3}]ᵀ";
            VectorU1Text = $"σ₁ · u₁ = [{TransformedU1Point.X:F3}, {TransformedU1Point.Y:F3}]ᵀ";
            VectorU2Text = $"σ₂ · u₂ = [{TransformedU2Point.X:F3}, {TransformedU2Point.Y:F3}]ᵀ";

            double detScaling = Sigma1 * Sigma2;
            AreaScalingText = $"Area Scaling |det| = σ₁ · σ₂ = {detScaling:F4}";

            // Generate points for the unit circle and ellipse
            const int sampleCount = 120;
            double maxExt = 1.5;

            // Project 2x2 submatrix if dimensions > 2
            double a00 = matrix[0, 0];
            double a01 = matrix[0, 1];
            double a10 = matrix[1, 0];
            double a11 = matrix[1, 1];

            for (int i = 0; i <= sampleCount; i++)
            {
                double theta = (2.0 * Math.PI * i) / sampleCount;
                double x = Math.Cos(theta);
                double y = Math.Sin(theta);
                DomainCirclePoints.Add(new Point(x, y));

                // Transformed point: A * [x, y]^T
                double tx = a00 * x + a01 * y;
                double ty = a10 * x + a11 * y;
                TransformedEllipsePoints.Add(new Point(tx, ty));

                maxExt = Math.Max(maxExt, Math.Max(Math.Abs(tx), Math.Abs(ty)));
            }

            maxExt = Math.Max(maxExt, Math.Max(Math.Abs(TransformedU1Point.X), Math.Abs(TransformedU1Point.Y)));
            maxExt = Math.Max(maxExt, Math.Max(Math.Abs(TransformedU2Point.X), Math.Abs(TransformedU2Point.Y)));
            MaxCoordinateExtent = maxExt * 1.25;
        }
        else
        {
            Is2DAvailable = false;
            StatusMessage = "Select a 2x2 matrix to view its geometric transformation.";
            VectorV1Text = "-";
            VectorV2Text = "-";
            VectorU1Text = "-";
            VectorU2Text = "-";
            AreaScalingText = "-";
            MaxCoordinateExtent = 2.0;
        }
    }
}
