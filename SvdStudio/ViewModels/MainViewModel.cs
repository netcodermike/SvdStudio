using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using SvdStudio.Common;
using SvdStudio.Core;

namespace SvdStudio.ViewModels;

public sealed class SingularValueItem
{
    public int Index { get; }
    public string Name => $"σ{Index}";
    public double Value { get; }
    public string FormattedValue => Matrix.FormatNumber(Value, "G6");
    public double EnergyPercent { get; }
    public double CumulativeEnergyPercent { get; }
    public string FormattedEnergy => $"{EnergyPercent:F2}%";
    public string FormattedCumulative => $"{CumulativeEnergyPercent:F2}%";
    public double BarHeightNormalized { get; } // 0.0 to 1.0 for bar chart

    public SingularValueItem(int index, double value, double energy, double cumulative, double maxVal)
    {
        Index = index;
        Value = value;
        EnergyPercent = energy;
        CumulativeEnergyPercent = cumulative;
        BarHeightNormalized = maxVal > 1e-14 ? Math.Clamp(value / maxVal, 0.01, 1.0) : 0.01;
    }
}

public sealed class MainViewModel : ObservableObject
{
    private string _inputMatrixText = string.Empty;
    private string? _errorMessage;
    private bool _hasError;
    private bool _isEconomy = true;
    private MatrixPreset? _selectedPreset;
    private SvdResult? _svdResult;

    // Random generator fields
    private int _randomRows = 4;
    private int _randomCols = 3;
    private double _randomMin = -10.0;
    private double _randomMax = 10.0;
    private bool _randomIntegerOnly = true;

    // Display sub-viewmodels
    private MatrixDisplayViewModel? _matrixA;
    private MatrixDisplayViewModel? _matrixU;
    private MatrixDisplayViewModel? _matrixS;
    private MatrixDisplayViewModel? _matrixV;
    private MatrixDisplayViewModel? _matrixVT;
    private MatrixDisplayViewModel? _matrixReconstructed;
    private MatrixDisplayViewModel? _matrixPInv;
    private TruncatedSvdViewModel? _truncatedSvd;
    private Geometric2DViewModel? _geometric2D;
    private ImageCompressionViewModel _imageCompression;

    public string InputMatrixText
    {
        get => _inputMatrixText;
        set => SetProperty(ref _inputMatrixText, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                HasError = !string.IsNullOrEmpty(value);
            }
        }
    }

    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    public bool IsEconomy
    {
        get => _isEconomy;
        set
        {
            if (SetProperty(ref _isEconomy, value))
            {
                ComputeSvd();
            }
        }
    }

    public IReadOnlyList<MatrixPreset> Presets => PresetMatrices.Presets;

    public MatrixPreset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetProperty(ref _selectedPreset, value) && value != null)
            {
                LoadPreset(value);
            }
        }
    }

    public ObservableCollection<SingularValueItem> SingularValueItems { get; } = new();

    #region Metric Properties

    public string MatrixDimensionText => _svdResult != null ? $"{_svdResult.OriginalMatrix.Rows} × {_svdResult.OriginalMatrix.Columns}" : "-";
    public string UDimensionText => _svdResult != null ? $"{_svdResult.U.Rows} × {_svdResult.U.Columns}" : "-";
    public string SDimensionText => _svdResult != null ? $"{_svdResult.S.Rows} × {_svdResult.S.Columns}" : "-";
    public string VTDimensionText => _svdResult != null ? $"{_svdResult.VT.Rows} × {_svdResult.VT.Columns}" : "-";

    public int Rank => _svdResult?.Rank ?? 0;
    public double ConditionNumber => _svdResult?.ConditionNumber ?? 0.0;
    public double FrobeniusNorm => _svdResult?.OriginalMatrix.FrobeniusNorm() ?? 0.0;
    public double SpectralNorm => _svdResult != null && _svdResult.SingularValues.Length > 0 ? _svdResult.SingularValues[0] : 0.0;
    public double NuclearNorm
    {
        get
        {
            if (_svdResult == null) return 0.0;
            double sum = 0.0;
            foreach (var s in _svdResult.SingularValues) sum += s;
            return sum;
        }
    }

    public string DeterminantText
    {
        get
        {
            if (_svdResult == null || _svdResult.OriginalMatrix.Rows != _svdResult.OriginalMatrix.Columns)
                return "N/A (Rectangular)";
            double prod = 1.0;
            foreach (var s in _svdResult.SingularValues) prod *= s;
            return $"|det| ≈ {Matrix.FormatNumber(prod, "G5")}";
        }
    }

    public double ReconstructionError => _svdResult?.ReconstructionErrorFrobenius ?? 0.0;
    public double OrthogonalityErrorU => _svdResult?.OrthogonalityErrorU ?? 0.0;
    public double OrthogonalityErrorV => _svdResult?.OrthogonalityErrorV ?? 0.0;
    public double ComputeTimeMs => _svdResult?.ComputeTime.TotalMilliseconds ?? 0.0;

    #endregion

    #region Sub-ViewModels

    public MatrixDisplayViewModel? MatrixA => _matrixA;
    public MatrixDisplayViewModel? MatrixU => _matrixU;
    public MatrixDisplayViewModel? MatrixS => _matrixS;
    public MatrixDisplayViewModel? MatrixV => _matrixV;
    public MatrixDisplayViewModel? MatrixVT => _matrixVT;
    public MatrixDisplayViewModel? MatrixReconstructed => _matrixReconstructed;
    public MatrixDisplayViewModel? MatrixPInv => _matrixPInv;
    public TruncatedSvdViewModel? TruncatedSvd => _truncatedSvd;
    public Geometric2DViewModel? Geometric2D => _geometric2D;
    public ImageCompressionViewModel ImageCompression => _imageCompression;

    #endregion

    #region Random Generator Options

    public int RandomRows
    {
        get => _randomRows;
        set => SetProperty(ref _randomRows, Math.Clamp(value, 1, 30));
    }

    public int RandomCols
    {
        get => _randomCols;
        set => SetProperty(ref _randomCols, Math.Clamp(value, 1, 30));
    }

    public double RandomMin
    {
        get => _randomMin;
        set => SetProperty(ref _randomMin, value);
    }

    public double RandomMax
    {
        get => _randomMax;
        set => SetProperty(ref _randomMax, value);
    }

    public bool RandomIntegerOnly
    {
        get => _randomIntegerOnly;
        set => SetProperty(ref _randomIntegerOnly, value);
    }

    #endregion

    #region Commands

    public RelayCommand ComputeSvdCommand { get; }
    public RelayCommand ClearInputCommand { get; }
    public RelayCommand GenerateRandomCommand { get; }
    public RelayCommand ImportFileCommand { get; }
    public RelayCommand ExportSummaryCommand { get; }

    #endregion

    public MainViewModel()
    {
        _imageCompression = new ImageCompressionViewModel();

        ComputeSvdCommand = new RelayCommand(ComputeSvd);
        ClearInputCommand = new RelayCommand(() => InputMatrixText = string.Empty);
        GenerateRandomCommand = new RelayCommand(ExecuteGenerateRandom);
        ImportFileCommand = new RelayCommand(ExecuteImportFile);
        ExportSummaryCommand = new RelayCommand(ExecuteExportSummary);

        // Load default initial matrix: 3x3 Magic Square
        var initial = Presets[3];
        _selectedPreset = initial;
        LoadPreset(initial);
    }

    private void LoadPreset(MatrixPreset preset)
    {
        try
        {
            var mat = preset.Generator();
            InputMatrixText = mat.ToDelimitedString("  ", "G5");
            ComputeSvd();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load preset: {ex.Message}";
        }
    }

    public void ComputeSvd()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(InputMatrixText))
        {
            ErrorMessage = "Please enter matrix numbers or select a preset.";
            return;
        }

        if (!MatrixParser.TryParse(InputMatrixText, out var matrix, out var parseError) || matrix == null)
        {
            ErrorMessage = $"Matrix parse error: {parseError}";
            return;
        }

        try
        {
            _svdResult = SvdAlgorithm.Decompose(matrix, _isEconomy);

            // Populate Sub-ViewModels
            _matrixA = new MatrixDisplayViewModel($"Input Matrix A ({matrix.Rows}×{matrix.Columns})", _svdResult.OriginalMatrix);
            _matrixU = new MatrixDisplayViewModel($"Left Singular Vectors U ({_svdResult.U.Rows}×{_svdResult.U.Columns})", _svdResult.U);
            _matrixS = new MatrixDisplayViewModel($"Singular Values Σ ({_svdResult.S.Rows}×{_svdResult.S.Columns})", _svdResult.S);
            _matrixV = new MatrixDisplayViewModel($"Right Singular Vectors V ({_svdResult.V.Rows}×{_svdResult.V.Columns})", _svdResult.V);
            _matrixVT = new MatrixDisplayViewModel($"Right Singular Vectors Transposed Vᵀ ({_svdResult.VT.Rows}×{_svdResult.VT.Columns})", _svdResult.VT);
            _matrixReconstructed = new MatrixDisplayViewModel($"Reconstructed Matrix A' = U · Σ · Vᵀ", _svdResult.U * _svdResult.S * _svdResult.VT);
            _matrixPInv = new MatrixDisplayViewModel($"Moore-Penrose Pseudo-Inverse A⁺ ({_svdResult.OriginalMatrix.Columns}×{_svdResult.OriginalMatrix.Rows})", _svdResult.ComputePseudoInverse());

            _truncatedSvd = new TruncatedSvdViewModel(_svdResult);
            _geometric2D = new Geometric2DViewModel(_svdResult.OriginalMatrix, _svdResult);

            // Populate singular value items
            SingularValueItems.Clear();
            double totalEnergy = 0.0;
            foreach (var sv in _svdResult.SingularValues) totalEnergy += sv * sv;

            double maxVal = _svdResult.SingularValues.Length > 0 ? _svdResult.SingularValues[0] : 1.0;
            double running = 0.0;

            for (int i = 0; i < _svdResult.SingularValues.Length; i++)
            {
                double sv = _svdResult.SingularValues[i];
                double sq = sv * sv;
                running += sq;
                double energy = totalEnergy > 1e-15 ? (sq / totalEnergy) * 100.0 : 100.0 / _svdResult.SingularValues.Length;
                double cumulative = totalEnergy > 1e-15 ? (running / totalEnergy) * 100.0 : 100.0;

                SingularValueItems.Add(new SingularValueItem(i + 1, sv, energy, cumulative, maxVal));
            }

            // Notify UI
            NotifyAllProperties();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"SVD Computation error: {ex.Message}";
        }
    }

    private void ExecuteGenerateRandom()
    {
        var mat = Matrix.Random(RandomRows, RandomCols, RandomMin, RandomMax, integerOnly: RandomIntegerOnly);
        InputMatrixText = mat.ToDelimitedString("  ", "G5");
        ComputeSvd();
    }

    private void ExecuteImportFile()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Matrix / Text Files (*.txt;*.csv;*.tsv;*.dat)|*.txt;*.csv;*.tsv;*.dat|All Files (*.*)|*.*",
            Title = "Import Matrix File"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                string text = File.ReadAllText(dlg.FileName);
                InputMatrixText = text;
                ComputeSvd();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to read file: {ex.Message}";
            }
        }
    }

    private void ExecuteExportSummary()
    {
        if (_svdResult == null) return;

        var dlg = new SaveFileDialog
        {
            Filter = "Text Report (*.txt)|*.txt",
            FileName = "SVD_Decomposition_Report.txt",
            Title = "Export SVD Decomposition Report"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("================================================================================");
                sb.AppendLine("                     SINGULAR VALUE DECOMPOSITION REPORT                        ");
                sb.AppendLine("================================================================================");
                sb.AppendLine($"Matrix Dimensions: {MatrixDimensionText}");
                sb.AppendLine($"Mode: {(IsEconomy ? "Thin / Economy SVD" : "Full SVD")}");
                sb.AppendLine($"Numerical Rank: {Rank}");
                sb.AppendLine($"Condition Number: {ConditionNumber:G6}");
                sb.AppendLine($"Frobenius Norm: {FrobeniusNorm:G6}");
                sb.AppendLine($"Spectral Norm (σ₁): {SpectralNorm:G6}");
                sb.AppendLine($"Nuclear Norm: {NuclearNorm:G6}");
                sb.AppendLine($"Determinant: {DeterminantText}");
                sb.AppendLine($"Reconstruction Frobenius Error: {ReconstructionError:E4}");
                sb.AppendLine($"U Orthogonality Error: {OrthogonalityErrorU:E4}");
                sb.AppendLine($"V Orthogonality Error: {OrthogonalityErrorV:E4}");
                sb.AppendLine();
                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine("SINGULAR VALUES:");
                for (int i = 0; i < _svdResult.SingularValues.Length; i++)
                {
                    sb.AppendLine($"  σ_{i + 1} = {_svdResult.SingularValues[i]:G8}");
                }
                sb.AppendLine();
                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine("MATRIX A:");
                sb.AppendLine(_svdResult.OriginalMatrix.ToDelimitedString("\t"));
                sb.AppendLine();
                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine("MATRIX U (Left Singular Vectors):");
                sb.AppendLine(_svdResult.U.ToDelimitedString("\t"));
                sb.AppendLine();
                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine("MATRIX Σ (Singular Values):");
                sb.AppendLine(_svdResult.S.ToDelimitedString("\t"));
                sb.AppendLine();
                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine("MATRIX Vᵀ (Right Singular Vectors Transposed):");
                sb.AppendLine(_svdResult.VT.ToDelimitedString("\t"));
                sb.AppendLine();
                sb.AppendLine("--------------------------------------------------------------------------------");
                sb.AppendLine("MOORE-PENROSE PSEUDO-INVERSE A⁺:");
                sb.AppendLine(_svdResult.ComputePseudoInverse().ToDelimitedString("\t"));
                sb.AppendLine("================================================================================");

                File.WriteAllText(dlg.FileName, sb.ToString());
                MessageBox.Show("Report exported successfully!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void NotifyAllProperties()
    {
        OnPropertyChanged(nameof(MatrixDimensionText));
        OnPropertyChanged(nameof(UDimensionText));
        OnPropertyChanged(nameof(SDimensionText));
        OnPropertyChanged(nameof(VTDimensionText));
        OnPropertyChanged(nameof(Rank));
        OnPropertyChanged(nameof(ConditionNumber));
        OnPropertyChanged(nameof(FrobeniusNorm));
        OnPropertyChanged(nameof(SpectralNorm));
        OnPropertyChanged(nameof(NuclearNorm));
        OnPropertyChanged(nameof(DeterminantText));
        OnPropertyChanged(nameof(ReconstructionError));
        OnPropertyChanged(nameof(OrthogonalityErrorU));
        OnPropertyChanged(nameof(OrthogonalityErrorV));
        OnPropertyChanged(nameof(ComputeTimeMs));

        OnPropertyChanged(nameof(MatrixA));
        OnPropertyChanged(nameof(MatrixU));
        OnPropertyChanged(nameof(MatrixS));
        OnPropertyChanged(nameof(MatrixV));
        OnPropertyChanged(nameof(MatrixVT));
        OnPropertyChanged(nameof(MatrixReconstructed));
        OnPropertyChanged(nameof(MatrixPInv));
        OnPropertyChanged(nameof(TruncatedSvd));
        OnPropertyChanged(nameof(Geometric2D));
    }
}
