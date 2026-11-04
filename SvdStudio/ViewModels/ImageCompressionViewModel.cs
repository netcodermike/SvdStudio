using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SvdStudio.Common;
using SvdStudio.Core;
using Matrix = SvdStudio.Core.Matrix;

namespace SvdStudio.ViewModels;

public sealed class ImagePresetOption
{
    public string Name { get; }
    public Func<int, Matrix> Generator { get; }

    public ImagePresetOption(string name, Func<int, Matrix> generator)
    {
        Name = name;
        Generator = generator;
    }

    public override string ToString() => Name;
}

public sealed class ImageCompressionViewModel : ObservableObject
{
    private Matrix _originalMatrix;
    private SvdResult _svdResult;
    private int _rank = 5;
    private BitmapSource? _originalBitmap;
    private BitmapSource? _reconstructedBitmap;
    private ImagePresetOption _selectedPreset;

    public IReadOnlyList<ImagePresetOption> Presets { get; }

    public ImagePresetOption SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value) && value != null)
            {
                LoadMatrix(value.Generator(48));
            }
        }
    }

    public int MaxRank => _svdResult?.SingularValues.Length ?? 1;

    public int Rank
    {
        get => _rank;
        set
        {
            if (SetProperty(ref _rank, Math.Clamp(value, 1, MaxRank)))
            {
                UpdateReconstructedImage();
            }
        }
    }

    public BitmapSource? OriginalBitmap
    {
        get => _originalBitmap;
        private set => SetProperty(ref _originalBitmap, value);
    }

    public BitmapSource? ReconstructedBitmap
    {
        get => _reconstructedBitmap;
        private set => SetProperty(ref _reconstructedBitmap, value);
    }

    public string ResolutionText => $"{_originalMatrix?.Rows} × {_originalMatrix?.Columns}";
    public string CompressionRatioText { get; private set; } = "-";
    public string StorageSavingsText { get; private set; } = "-";
    public string EnergyText { get; private set; } = "-";
    public string ReconstructionErrorText { get; private set; } = "-";

    public RelayCommand LoadFileCommand { get; }

    public ImageCompressionViewModel()
    {
        Presets = new List<ImagePresetOption>
        {
            new("Geometric Letter 'S'", GenerateLetterS),
            new("Concentric Ripples", GenerateRipples),
            new("Checkerboard (8x8)", GenerateCheckerboard),
            new("Gaussian Peak & Valley", GenerateGaussianPeak),
            new("Diagonal Wave Interference", GenerateWaves)
        };

        _selectedPreset = Presets[0];
        _originalMatrix = _selectedPreset.Generator(48);
        _svdResult = SvdAlgorithm.Decompose(_originalMatrix, economy: true);

        LoadFileCommand = new RelayCommand(ExecuteLoadFile);

        LoadMatrix(_originalMatrix);
    }

    private void ExecuteLoadFile()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|All Files (*.*)|*.*",
            Title = "Select Image for SVD Compression"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(dlg.FileName);
                bi.DecodePixelWidth = 64; // Resize to reasonable matrix dimensions
                bi.EndInit();
                bi.Freeze();

                var fcb = new FormatConvertedBitmap(bi, PixelFormats.Gray8, null, 0);
                int w = fcb.PixelWidth;
                int h = fcb.PixelHeight;
                byte[] raw = new byte[w * h];
                fcb.CopyPixels(raw, w, 0);

                var matData = new double[h * w];
                for (int i = 0; i < raw.Length; i++) matData[i] = raw[i];

                var mat = new Matrix(h, w, matData);
                LoadMatrix(mat);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to load image: {ex.Message}", "Image Load Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    public void LoadMatrix(Matrix matrix)
    {
        _originalMatrix = matrix;
        _svdResult = SvdAlgorithm.Decompose(_originalMatrix, economy: true);
        _rank = Math.Min(Math.Max(1, _svdResult.Rank / 2), _svdResult.SingularValues.Length);

        OriginalBitmap = MatrixToBitmap(_originalMatrix);

        OnPropertyChanged(nameof(MaxRank));
        OnPropertyChanged(nameof(Rank));
        OnPropertyChanged(nameof(ResolutionText));

        UpdateReconstructedImage();
    }

    private void UpdateReconstructedImage()
    {
        if (_svdResult == null) return;

        var trunc = _svdResult.GetTruncatedApproximation(_rank);
        ReconstructedBitmap = MatrixToBitmap(trunc.Approximation);

        int m = _originalMatrix.Rows;
        int n = _originalMatrix.Columns;
        int originalEntries = m * n;
        int compressedEntries = _rank * (m + n + 1);

        double ratio = (double)compressedEntries / originalEntries * 100.0;
        double savings = Math.Max(0.0, 100.0 - ratio);

        CompressionRatioText = $"{compressedEntries} / {originalEntries} values ({ratio:F1}%)";
        StorageSavingsText = $"{savings:F1}% Storage Reduction";
        EnergyText = $"{trunc.EnergyPercentage:F2}% Energy Retained";
        ReconstructionErrorText = $"Frobenius Error: {trunc.FrobeniusError:F2} (Rel: {trunc.RelativeError * 100:F2}%)";

        OnPropertyChanged(nameof(CompressionRatioText));
        OnPropertyChanged(nameof(StorageSavingsText));
        OnPropertyChanged(nameof(EnergyText));
        OnPropertyChanged(nameof(ReconstructionErrorText));
    }

    private static BitmapSource MatrixToBitmap(Matrix m)
    {
        int rows = m.Rows;
        int cols = m.Columns;
        byte[] pixels = new byte[rows * cols];

        for (int i = 0; i < rows; i++)
        {
            int offset = i * cols;
            for (int j = 0; j < cols; j++)
            {
                double val = m[i, j];
                pixels[offset + j] = (byte)Math.Clamp((int)Math.Round(val), 0, 255);
            }
        }

        var bs = BitmapSource.Create(cols, rows, 96, 96, PixelFormats.Gray8, null, pixels, cols);
        bs.Freeze();
        return bs;
    }

    #region Procedural Pattern Generators

    private static Matrix GenerateLetterS(int size)
    {
        var data = new double[size * size];
        for (int i = 0; i < size; i++)
        {
            double y = (double)i / size;
            for (int j = 0; j < size; j++)
            {
                double x = (double)j / size;
                bool isS = false;
                // Top bar
                if (y >= 0.15 && y <= 0.28 && x >= 0.2 && x <= 0.8) isS = true;
                // Middle bar
                if (y >= 0.44 && y <= 0.56 && x >= 0.2 && x <= 0.8) isS = true;
                // Bottom bar
                if (y >= 0.72 && y <= 0.85 && x >= 0.2 && x <= 0.8) isS = true;
                // Top left vertical
                if (y >= 0.20 && y <= 0.50 && x >= 0.2 && x <= 0.35) isS = true;
                // Bottom right vertical
                if (y >= 0.50 && y <= 0.80 && x >= 0.65 && x <= 0.8) isS = true;

                data[i * size + j] = isS ? 245.0 : 25.0;
            }
        }
        return new Matrix(size, size, data);
    }

    private static Matrix GenerateRipples(int size)
    {
        var data = new double[size * size];
        double center = size / 2.0;
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                double dist = Math.Sqrt((i - center) * (i - center) + (j - center) * (j - center));
                double wave = (Math.Sin(dist * 0.6) + 1.0) * 0.5;
                data[i * size + j] = wave * 255.0;
            }
        }
        return new Matrix(size, size, data);
    }

    private static Matrix GenerateCheckerboard(int size)
    {
        var data = new double[size * size];
        int squareSize = Math.Max(2, size / 8);
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                bool isWhite = ((i / squareSize) + (j / squareSize)) % 2 == 0;
                data[i * size + j] = isWhite ? 230.0 : 30.0;
            }
        }
        return new Matrix(size, size, data);
    }

    private static Matrix GenerateGaussianPeak(int size)
    {
        var data = new double[size * size];
        double c1 = size * 0.35;
        double c2 = size * 0.65;
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                double d1 = ((i - c1) * (i - c1) + (j - c1) * (j - c1)) / (size * 3.5);
                double d2 = ((i - c2) * (i - c2) + (j - c2) * (j - c2)) / (size * 3.5);
                double val = Math.Exp(-d1) * 220.0 + (1.0 - Math.Exp(-d2)) * 30.0;
                data[i * size + j] = Math.Clamp(val, 0, 255);
            }
        }
        return new Matrix(size, size, data);
    }

    private static Matrix GenerateWaves(int size)
    {
        var data = new double[size * size];
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                double w1 = Math.Sin(i * 0.25);
                double w2 = Math.Cos(j * 0.25);
                double w3 = Math.Sin((i + j) * 0.15);
                double val = ((w1 + w2 + w3) / 3.0 + 1.0) * 0.5 * 255.0;
                data[i * size + j] = Math.Clamp(val, 0, 255);
            }
        }
        return new Matrix(size, size, data);
    }

    #endregion
}
