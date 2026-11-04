using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using SvdStudio.Common;
using SvdStudio.Core;
using Matrix = SvdStudio.Core.Matrix;

namespace SvdStudio.ViewModels;

public sealed class CellViewModel
{
    public int Row { get; }
    public int Column { get; }
    public double Value { get; }
    public string FormattedText { get; }
    public Brush BackgroundBrush { get; }
    public Brush ForegroundBrush { get; }

    public CellViewModel(int row, int col, double val, double maxAbs, string format = "G5")
    {
        Row = row;
        Column = col;
        Value = val;
        FormattedText = Matrix.FormatNumber(val, format);

        // Heatmap color logic:
        // Positive: Cyan/Blue gradient (0 to maxAbs)
        // Negative: Coral/Red gradient (0 to maxAbs)
        // Zero: Subtle dark slate
        double intensity = maxAbs > 1e-12 ? Math.Min(1.0, Math.Abs(val) / maxAbs) : 0.0;
        byte alpha = (byte)(20 + (int)(intensity * 180));

        if (Math.Abs(val) < 1e-12)
        {
            BackgroundBrush = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
            ForegroundBrush = new SolidColorBrush(Color.FromRgb(140, 145, 160));
        }
        else if (val > 0)
        {
            BackgroundBrush = new SolidColorBrush(Color.FromArgb(alpha, 56, 189, 248)); // Tailwind Sky-400
            ForegroundBrush = Brushes.White;
        }
        else
        {
            BackgroundBrush = new SolidColorBrush(Color.FromArgb(alpha, 244, 63, 94));  // Tailwind Rose-500
            ForegroundBrush = Brushes.White;
        }

        BackgroundBrush.Freeze();
        ForegroundBrush.Freeze();
    }
}

public sealed class MatrixRowViewModel
{
    public int RowIndex { get; }
    public string HeaderText => $"R{RowIndex + 1}";
    public IReadOnlyList<CellViewModel> Cells { get; }

    public MatrixRowViewModel(int rowIndex, IReadOnlyList<CellViewModel> cells)
    {
        RowIndex = rowIndex;
        Cells = cells;
    }
}

public sealed class MatrixDisplayViewModel : ObservableObject
{
    private string _numberFormat = "G5";

    public string Title { get; }
    public Matrix Matrix { get; }
    public int Rows => Matrix.Rows;
    public int Columns => Matrix.Columns;
    public string DimensionsBadge => $"{Rows} × {Columns}";

    public IReadOnlyList<string> ColumnHeaders { get; }
    public IReadOnlyList<MatrixRowViewModel> RowsList { get; private set; }

    public RelayCommand CopyCsvCommand { get; }
    public RelayCommand CopyLatexCommand { get; }
    public RelayCommand CopyMatlabCommand { get; }
    public RelayCommand CopyJsonCommand { get; }

    public string NumberFormat
    {
        get => _numberFormat;
        set
        {
            if (SetProperty(ref _numberFormat, value))
            {
                RebuildRows();
            }
        }
    }

    public MatrixDisplayViewModel(string title, Matrix matrix, string numberFormat = "G5")
    {
        Title = title;
        Matrix = matrix ?? throw new ArgumentNullException(nameof(matrix));
        _numberFormat = numberFormat;

        var colHeaders = new List<string>(matrix.Columns);
        for (int j = 0; j < matrix.Columns; j++)
        {
            colHeaders.Add($"C{j + 1}");
        }
        ColumnHeaders = colHeaders;

        RowsList = BuildRowsList();

        CopyCsvCommand = new RelayCommand(() => SafeSetClipboard(Matrix.ToCsvString(NumberFormat)));
        CopyLatexCommand = new RelayCommand(() => SafeSetClipboard(Matrix.ToLatexString(NumberFormat)));
        CopyMatlabCommand = new RelayCommand(() => SafeSetClipboard(Matrix.ToMatlabString(NumberFormat)));
        CopyJsonCommand = new RelayCommand(() => SafeSetClipboard(Matrix.ToJsonString(NumberFormat)));
    }

    private void RebuildRows()
    {
        RowsList = BuildRowsList();
        OnPropertyChanged(nameof(RowsList));
    }

    private IReadOnlyList<MatrixRowViewModel> BuildRowsList()
    {
        double maxAbs = Matrix.MaxAbsoluteValue();
        var rows = new List<MatrixRowViewModel>(Matrix.Rows);

        for (int i = 0; i < Matrix.Rows; i++)
        {
            var cells = new List<CellViewModel>(Matrix.Columns);
            for (int j = 0; j < Matrix.Columns; j++)
            {
                cells.Add(new CellViewModel(i, j, Matrix[i, j], maxAbs, _numberFormat));
            }
            rows.Add(new MatrixRowViewModel(i, cells));
        }

        return rows;
    }

    private static void SafeSetClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            // Ignore clipboard locking exceptions
        }
    }
}
