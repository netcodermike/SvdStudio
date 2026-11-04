using System;
using System.Globalization;
using System.Text;

namespace SvdStudio.Core;

/// <summary>
/// Represents an immutable 2D real matrix with linear algebra operations and formatting tools.
/// </summary>
public sealed class Matrix : IEquatable<Matrix>
{
    private readonly double[] _data;

    public int Rows { get; }
    public int Columns { get; }

    public double this[int row, int col]
    {
        get
        {
            if ((uint)row >= (uint)Rows || (uint)col >= (uint)Columns)
                throw new IndexOutOfRangeException($"Index [{row}, {col}] is outside matrix dimensions {Rows}x{Columns}.");
            return _data[row * Columns + col];
        }
    }

    public Matrix(int rows, int cols)
    {
        if (rows <= 0 || cols <= 0)
            throw new ArgumentException("Dimensions must be positive integers.");

        Rows = rows;
        Columns = cols;
        _data = new double[rows * cols];
    }

    public Matrix(int rows, int cols, double[] data)
    {
        if (rows <= 0 || cols <= 0)
            throw new ArgumentException("Dimensions must be positive integers.");
        if (data == null || data.Length != rows * cols)
            throw new ArgumentException($"Data array length ({data?.Length ?? 0}) does not match dimensions {rows}x{cols}.");

        Rows = rows;
        Columns = cols;
        _data = (double[])data.Clone();
    }

    public Matrix(double[,] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        Rows = data.GetLength(0);
        Columns = data.GetLength(1);
        if (Rows == 0 || Columns == 0)
            throw new ArgumentException("Matrix dimensions must be non-empty.");

        _data = new double[Rows * Columns];
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                _data[i * Columns + j] = data[i, j];
            }
        }
    }

    public Matrix(double[][] data)
    {
        if (data == null || data.Length == 0)
            throw new ArgumentException("Data must contain at least one row.");

        Rows = data.Length;
        Columns = data[0].Length;
        if (Columns == 0)
            throw new ArgumentException("Columns must be greater than zero.");

        _data = new double[Rows * Columns];
        for (int i = 0; i < Rows; i++)
        {
            if (data[i] == null || data[i].Length != Columns)
                throw new ArgumentException($"Row {i} has length {data[i]?.Length ?? 0}, expected {Columns}.");

            for (int j = 0; j < Columns; j++)
            {
                _data[i * Columns + j] = data[i][j];
            }
        }
    }

    /// <summary>
    /// Creates a deep copy of the raw 2D array.
    /// </summary>
    public double[,] ToArray2D()
    {
        var result = new double[Rows, Columns];
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                result[i, j] = _data[i * Columns + j];
            }
        }
        return result;
    }

    public double[] ToFlatArray() => (double[])_data.Clone();

    public double[] GetRow(int row)
    {
        if ((uint)row >= (uint)Rows)
            throw new IndexOutOfRangeException(nameof(row));

        var result = new double[Columns];
        Array.Copy(_data, row * Columns, result, 0, Columns);
        return result;
    }

    public double[] GetColumn(int col)
    {
        if ((uint)col >= (uint)Columns)
            throw new IndexOutOfRangeException(nameof(col));

        var result = new double[Rows];
        for (int i = 0; i < Rows; i++)
        {
            result[i] = _data[i * Columns + col];
        }
        return result;
    }

    public Matrix Transpose()
    {
        var resultData = new double[Columns * Rows];
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                resultData[j * Rows + i] = _data[i * Columns + j];
            }
        }
        return new Matrix(Columns, Rows, resultData);
    }

    public static Matrix operator +(Matrix a, Matrix b)
    {
        if (a.Rows != b.Rows || a.Columns != b.Columns)
            throw new ArgumentException($"Matrix dimension mismatch for addition: ({a.Rows}x{a.Columns}) + ({b.Rows}x{b.Columns}).");

        var len = a._data.Length;
        var res = new double[len];
        for (int i = 0; i < len; i++)
            res[i] = a._data[i] + b._data[i];

        return new Matrix(a.Rows, a.Columns, res);
    }

    public static Matrix operator -(Matrix a, Matrix b)
    {
        if (a.Rows != b.Rows || a.Columns != b.Columns)
            throw new ArgumentException($"Matrix dimension mismatch for subtraction: ({a.Rows}x{a.Columns}) - ({b.Rows}x{b.Columns}).");

        var len = a._data.Length;
        var res = new double[len];
        for (int i = 0; i < len; i++)
            res[i] = a._data[i] - b._data[i];

        return new Matrix(a.Rows, a.Columns, res);
    }

    public static Matrix operator *(Matrix a, double scalar)
    {
        var len = a._data.Length;
        var res = new double[len];
        for (int i = 0; i < len; i++)
            res[i] = a._data[i] * scalar;

        return new Matrix(a.Rows, a.Columns, res);
    }

    public static Matrix operator *(double scalar, Matrix a) => a * scalar;

    public static Matrix operator *(Matrix a, Matrix b)
    {
        if (a.Columns != b.Rows)
            throw new ArgumentException($"Matrix multiplication dimension mismatch: ({a.Rows}x{a.Columns}) * ({b.Rows}x{b.Columns}). Inner dimensions must match.");

        int m = a.Rows;
        int n = a.Columns;
        int p = b.Columns;

        var res = new double[m * p];

        // Cache-friendly blocked or transposed multiplication
        var bTransposed = b.Transpose()._data;

        for (int i = 0; i < m; i++)
        {
            int iOffset = i * n;
            int outOffset = i * p;

            for (int j = 0; j < p; j++)
            {
                int jOffset = j * n;
                double sum = 0.0;
                for (int k = 0; k < n; k++)
                {
                    sum += a._data[iOffset + k] * bTransposed[jOffset + k];
                }
                res[outOffset + j] = sum;
            }
        }

        return new Matrix(m, p, res);
    }

    /// <summary>
    /// Computes the outer product u * v^T of two vectors.
    /// </summary>
    public static Matrix OuterProduct(double[] u, double[] v)
    {
        if (u == null) throw new ArgumentNullException(nameof(u));
        if (v == null) throw new ArgumentNullException(nameof(v));

        int m = u.Length;
        int n = v.Length;
        var res = new double[m * n];

        for (int i = 0; i < m; i++)
        {
            double ui = u[i];
            int offset = i * n;
            for (int j = 0; j < n; j++)
            {
                res[offset + j] = ui * v[j];
            }
        }
        return new Matrix(m, n, res);
    }

    /// <summary>
    /// Computes the Frobenius norm of this matrix.
    /// </summary>
    public double FrobeniusNorm()
    {
        double sumSq = 0.0;
        foreach (var v in _data)
        {
            sumSq += v * v;
        }
        return Math.Sqrt(sumSq);
    }

    /// <summary>
    /// Computes the maximum absolute value of any element.
    /// </summary>
    public double MaxAbsoluteValue()
    {
        double max = 0.0;
        foreach (var v in _data)
        {
            double abs = Math.Abs(v);
            if (abs > max) max = abs;
        }
        return max;
    }

    /// <summary>
    /// Creates an identity matrix of size n x n.
    /// </summary>
    public static Matrix Identity(int n)
    {
        if (n <= 0) throw new ArgumentException("Size must be positive.", nameof(n));
        var res = new double[n * n];
        for (int i = 0; i < n; i++)
            res[i * n + i] = 1.0;
        return new Matrix(n, n, res);
    }

    /// <summary>
    /// Creates an all-zeros matrix.
    /// </summary>
    public static Matrix Zeros(int rows, int cols) => new Matrix(rows, cols);

    /// <summary>
    /// Creates a diagonal or rectangular matrix with diagonal entries set to values.
    /// </summary>
    public static Matrix Diagonal(double[] values, int rows = -1, int cols = -1)
    {
        if (values == null) throw new ArgumentNullException(nameof(values));
        int m = rows > 0 ? rows : values.Length;
        int n = cols > 0 ? cols : values.Length;

        var res = new double[m * n];
        int count = Math.Min(Math.Min(m, n), values.Length);
        for (int i = 0; i < count; i++)
        {
            res[i * n + i] = values[i];
        }
        return new Matrix(m, n, res);
    }

    /// <summary>
    /// Generates a random matrix with entries in [min, max].
    /// </summary>
    public static Matrix Random(int rows, int cols, double min = -10.0, double max = 10.0, int? seed = null, bool integerOnly = false)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        var data = new double[rows * cols];
        for (int i = 0; i < data.Length; i++)
        {
            double val = min + rng.NextDouble() * (max - min);
            data[i] = integerOnly ? Math.Round(val) : Math.Round(val, 4);
        }
        return new Matrix(rows, cols, data);
    }

    /// <summary>
    /// Constructs a matrix from column vectors.
    /// </summary>
    public static Matrix FromColumns(params double[][] cols)
    {
        if (cols == null || cols.Length == 0)
            throw new ArgumentException("Must provide at least one column.");

        int rows = cols[0].Length;
        int numCols = cols.Length;
        var data = new double[rows * numCols];

        for (int j = 0; j < numCols; j++)
        {
            if (cols[j].Length != rows)
                throw new ArgumentException($"Column {j} has length {cols[j].Length}, expected {rows}.");

            for (int i = 0; i < rows; i++)
            {
                data[i * numCols + j] = cols[j][i];
            }
        }
        return new Matrix(rows, numCols, data);
    }

    /// <summary>
    /// Constructs a submatrix with given row and column ranges.
    /// </summary>
    public Matrix Submatrix(int startRow, int rowCount, int startCol, int colCount)
    {
        if (startRow < 0 || startRow + rowCount > Rows || startCol < 0 || startCol + colCount > Columns)
            throw new ArgumentOutOfRangeException("Submatrix range is out of bounds.");

        var res = new double[rowCount * colCount];
        for (int i = 0; i < rowCount; i++)
        {
            for (int j = 0; j < colCount; j++)
            {
                res[i * colCount + j] = this[startRow + i, startCol + j];
            }
        }
        return new Matrix(rowCount, colCount, res);
    }

    #region Formatting & Exports

    public string ToDelimitedString(string delimiter = "\t", string format = "G6")
    {
        var sb = new StringBuilder();
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                if (j > 0) sb.Append(delimiter);
                sb.Append(FormatNumber(this[i, j], format));
            }
            if (i < Rows - 1) sb.AppendLine();
        }
        return sb.ToString();
    }

    public string ToCsvString(string format = "G6") => ToDelimitedString(",", format);

    public string ToMatlabString(string format = "G6")
    {
        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < Rows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                if (j > 0) sb.Append(", ");
                sb.Append(FormatNumber(this[i, j], format));
            }
            if (i < Rows - 1) sb.Append("; ");
        }
        sb.Append(']');
        return sb.ToString();
    }

    public string ToLatexString(string format = "G6")
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"\begin{bmatrix}");
        for (int i = 0; i < Rows; i++)
        {
            sb.Append("  ");
            for (int j = 0; j < Columns; j++)
            {
                if (j > 0) sb.Append(" & ");
                sb.Append(FormatNumber(this[i, j], format));
            }
            if (i < Rows - 1) sb.AppendLine(@" \\");
            else sb.AppendLine();
        }
        sb.Append(@"\end{bmatrix}");
        return sb.ToString();
    }

    public string ToJsonString(string format = "G6")
    {
        var sb = new StringBuilder();
        sb.AppendLine("[");
        for (int i = 0; i < Rows; i++)
        {
            sb.Append("  [");
            for (int j = 0; j < Columns; j++)
            {
                if (j > 0) sb.Append(", ");
                sb.Append(FormatNumber(this[i, j], format));
            }
            sb.Append(']');
            if (i < Rows - 1) sb.AppendLine(",");
            else sb.AppendLine();
        }
        sb.Append("]");
        return sb.ToString();
    }

    public static string FormatNumber(double val, string format = "G6")
    {
        // Snap very small values to 0 for clean display
        if (Math.Abs(val) < 1e-14) return "0";
        return val.ToString(format, CultureInfo.InvariantCulture);
    }

    public override string ToString() => $"{Rows}x{Columns} Matrix:\n{ToDelimitedString("  ")}";

    #endregion

    #region Equality

    public bool Equals(Matrix? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Rows != other.Rows || Columns != other.Columns) return false;

        for (int i = 0; i < _data.Length; i++)
        {
            if (Math.Abs(_data[i] - other._data[i]) > 1e-12)
                return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is Matrix m && Equals(m);

    public override int GetHashCode() => HashCode.Combine(Rows, Columns, _data.Length > 0 ? _data[0] : 0);

    #endregion
}
