using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SvdStudio.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; } = false;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool b = value is true;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isVis = value is Visibility v && v == Visibility.Visible;
        return Invert ? !isVis : isVis;
    }
}

public sealed class DoubleFormatConverter : IValueConverter
{
    public string Format { get; set; } = "G6";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            if (double.IsPositiveInfinity(d)) return "∞";
            if (double.IsNegativeInfinity(d)) return "-∞";
            if (double.IsNaN(d)) return "NaN";
            if (Math.Abs(d) < 1e-14) return "0";

            string fmt = parameter as string ?? Format;
            return d.ToString(fmt, CultureInfo.InvariantCulture);
        }
        return value?.ToString() ?? "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (double.TryParse(value as string, NumberStyles.Float, CultureInfo.InvariantCulture, out double res))
            return res;
        return 0.0;
    }
}

public sealed class ConditionNumberConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double d)
        {
            if (double.IsPositiveInfinity(d) || d > 1e12) return "Ill-conditioned (∞)";
            if (d < 1.05) return $"{d:F2} (Well-conditioned, close to orthogonal)";
            return $"{d:F2}";
        }
        return "-";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool hasValue = value != null;
        if (Invert) hasValue = !hasValue;
        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotImplementedException();
}
