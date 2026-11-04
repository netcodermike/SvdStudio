using System;
using System.Collections.Generic;
using SvdStudio.Common;
using SvdStudio.Core;

namespace SvdStudio.ViewModels;

public sealed class TruncatedSvdViewModel : ObservableObject
{
    private readonly SvdResult _svd;
    private int _targetRank;
    private int _selectedRank1Index = 0;
    private TruncatedSvdResult _currentTruncation;
    private MatrixDisplayViewModel _approxDisplay;
    private MatrixDisplayViewModel _diffDisplay;
    private MatrixDisplayViewModel _rank1Display;

    public int MaxRank => _svd.SingularValues.Length;
    public double TotalSingularValuesCount => _svd.SingularValues.Length;

    public int TargetRank
    {
        get => _targetRank;
        set
        {
            if (value >= 1 && value <= MaxRank && SetProperty(ref _targetRank, value))
            {
                UpdateTruncation();
            }
        }
    }

    public int SelectedRank1Index
    {
        get => _selectedRank1Index;
        set
        {
            if (value >= 0 && value < MaxRank && SetProperty(ref _selectedRank1Index, value))
            {
                UpdateRank1Display();
            }
        }
    }

    public double FrobeniusError => _currentTruncation.FrobeniusError;
    public double RelativeErrorPercent => _currentTruncation.RelativeError * 100.0;
    public double EnergyPercentage => _currentTruncation.EnergyPercentage;

    public MatrixDisplayViewModel ApproxDisplay => _approxDisplay;
    public MatrixDisplayViewModel DiffDisplay => _diffDisplay;
    public MatrixDisplayViewModel Rank1Display => _rank1Display;

    public IReadOnlyList<int> AvailableRanks { get; }
    public IReadOnlyList<string> Rank1ModeNames { get; }

    public TruncatedSvdViewModel(SvdResult svd)
    {
        _svd = svd ?? throw new ArgumentNullException(nameof(svd));
        _targetRank = Math.Min(svd.Rank > 0 ? svd.Rank : 1, svd.SingularValues.Length);

        var ranks = new List<int>(svd.SingularValues.Length);
        var modes = new List<string>(svd.SingularValues.Length);
        for (int i = 1; i <= svd.SingularValues.Length; i++)
        {
            ranks.Add(i);
            modes.Add($"Mode {i} (σ{i} = {Matrix.FormatNumber(svd.SingularValues[i - 1], "G4")})");
        }
        AvailableRanks = ranks;
        Rank1ModeNames = modes;

        _currentTruncation = _svd.GetTruncatedApproximation(_targetRank);
        _approxDisplay = new MatrixDisplayViewModel($"Rank-{_targetRank} Approximation (A_{_targetRank})", _currentTruncation.Approximation);
        _diffDisplay = new MatrixDisplayViewModel($"Residual Error Matrix (|A - A_{_targetRank}|)", (_svd.OriginalMatrix - _currentTruncation.Approximation));

        var rank1List = _svd.GetRank1Components();
        _rank1Display = new MatrixDisplayViewModel($"Rank-1 Mode 1 (σ₁ · u₁ · v₁ᵀ)", rank1List.Count > 0 ? rank1List[0] : Matrix.Zeros(1, 1));
    }

    private void UpdateTruncation()
    {
        _currentTruncation = _svd.GetTruncatedApproximation(_targetRank);
        _approxDisplay = new MatrixDisplayViewModel($"Rank-{_targetRank} Approximation (A_{_targetRank})", _currentTruncation.Approximation);
        _diffDisplay = new MatrixDisplayViewModel($"Residual Error Matrix (|A - A_{_targetRank}|)", (_svd.OriginalMatrix - _currentTruncation.Approximation));

        OnPropertyChanged(nameof(FrobeniusError));
        OnPropertyChanged(nameof(RelativeErrorPercent));
        OnPropertyChanged(nameof(EnergyPercentage));
        OnPropertyChanged(nameof(ApproxDisplay));
        OnPropertyChanged(nameof(DiffDisplay));
    }

    private void UpdateRank1Display()
    {
        var rank1List = _svd.GetRank1Components();
        if (_selectedRank1Index >= 0 && _selectedRank1Index < rank1List.Count)
        {
            double sigma = _svd.SingularValues[_selectedRank1Index];
            _rank1Display = new MatrixDisplayViewModel(
                $"Rank-1 Mode {_selectedRank1Index + 1} (σ_{_selectedRank1Index + 1} = {Matrix.FormatNumber(sigma, "G4")} · u · vᵀ)",
                rank1List[_selectedRank1Index]);
            OnPropertyChanged(nameof(Rank1Display));
        }
    }
}
