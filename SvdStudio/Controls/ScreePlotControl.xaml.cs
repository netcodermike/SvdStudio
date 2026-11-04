using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SvdStudio.ViewModels;

namespace SvdStudio.Controls;

public partial class ScreePlotControl : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(ScreePlotControl),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ScreePlotControl()
    {
        InitializeComponent();
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScreePlotControl control)
        {
            if (e.OldValue is INotifyCollectionChanged oldColl)
                oldColl.CollectionChanged -= control.OnCollectionChanged;

            if (e.NewValue is INotifyCollectionChanged newColl)
                newColl.CollectionChanged += control.OnCollectionChanged;

            control.RedrawPlot();
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RedrawPlot();
    }

    private void PlotCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RedrawPlot();
    }

    private void RedrawPlot()
    {
        PlotCanvas.Children.Clear();

        double w = PlotCanvas.ActualWidth;
        double h = PlotCanvas.ActualHeight;
        if (w < 80 || h < 60) return;

        double padLeft = 45;
        double padRight = 45;
        double padTop = 20;
        double padBottom = 30;

        double plotW = w - padLeft - padRight;
        double plotH = h - padTop - padBottom;
        if (plotW <= 0 || plotH <= 0) return;

        var items = new List<SingularValueItem>();
        if (ItemsSource != null)
        {
            foreach (var item in ItemsSource)
            {
                if (item is SingularValueItem sv) items.Add(sv);
            }
        }

        if (items.Count == 0)
        {
            var emptyTxt = new TextBlock
            {
                Text = "No singular values to plot.",
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                FontSize = 12
            };
            Canvas.SetLeft(emptyTxt, w / 2 - 70);
            Canvas.SetTop(emptyTxt, h / 2 - 10);
            PlotCanvas.Children.Add(emptyTxt);
            return;
        }

        double maxVal = 0.0;
        foreach (var it in items)
        {
            if (it.Value > maxVal) maxVal = it.Value;
        }
        if (maxVal < 1e-12) maxVal = 1.0;

        // Draw horizontal grid lines & labels (0%, 25%, 50%, 75%, 100%)
        for (int step = 0; step <= 4; step++)
        {
            double ratio = step / 4.0;
            double y = padTop + plotH * (1.0 - ratio);

            var gridLine = new Line
            {
                X1 = padLeft,
                Y1 = y,
                X2 = padLeft + plotW,
                Y2 = y,
                Stroke = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                StrokeDashArray = new DoubleCollection { 2, 2 },
                StrokeThickness = 1
            };
            PlotCanvas.Children.Add(gridLine);

            // Left label: Singular value scale
            double svLabelVal = ratio * maxVal;
            var svLabel = new TextBlock
            {
                Text = svLabelVal < 10 ? svLabelVal.ToString("F1") : svLabelVal.ToString("G3"),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                FontSize = 9,
                TextAlignment = TextAlignment.Right,
                Width = padLeft - 6
            };
            Canvas.SetLeft(svLabel, 2);
            Canvas.SetTop(svLabel, y - 7);
            PlotCanvas.Children.Add(svLabel);

            // Right label: Cumulative percentage
            var pctLabel = new TextBlock
            {
                Text = $"{ratio * 100:0}%",
                Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                FontSize = 9,
                Width = padRight - 6
            };
            Canvas.SetLeft(pctLabel, padLeft + plotW + 6);
            Canvas.SetTop(pctLabel, y - 7);
            PlotCanvas.Children.Add(pctLabel);
        }

        // Draw Bars for Singular Values
        int count = items.Count;
        double slotW = plotW / count;
        double barW = Math.Clamp(slotW * 0.65, 6, 40);

        var cumulativePoints = new PointCollection();

        for (int i = 0; i < count; i++)
        {
            var it = items[i];
            double cx = padLeft + i * slotW + slotW / 2.0;
            double barH = (it.Value / maxVal) * plotH;
            double barX = cx - barW / 2.0;
            double barY = padTop + (plotH - barH);

            var bar = new Rectangle
            {
                Width = barW,
                Height = Math.Max(2, barH),
                RadiusX = 3,
                RadiusY = 3,
                Fill = new LinearGradientBrush(
                    Color.FromRgb(56, 189, 248),
                    Color.FromRgb(37, 99, 235),
                    90),
                ToolTip = $"{it.Name} = {it.Value:G6}\nVariance: {it.FormattedEnergy}\nCumulative: {it.FormattedCumulative}"
            };
            Canvas.SetLeft(bar, barX);
            Canvas.SetTop(bar, barY);
            PlotCanvas.Children.Add(bar);

            // Bottom X-axis label (sigma_i)
            var xLabel = new TextBlock
            {
                Text = it.Name,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 10,
                TextAlignment = TextAlignment.Center,
                Width = slotW
            };
            Canvas.SetLeft(xLabel, padLeft + i * slotW);
            Canvas.SetTop(xLabel, padTop + plotH + 4);
            PlotCanvas.Children.Add(xLabel);

            // Cumulative energy point
            double cumY = padTop + plotH * (1.0 - Math.Clamp(it.CumulativeEnergyPercent / 100.0, 0.0, 1.0));
            cumulativePoints.Add(new Point(cx, cumY));
        }

        // Draw Cumulative Energy Polyline
        if (cumulativePoints.Count > 1)
        {
            var polyline = new Polyline
            {
                Points = cumulativePoints,
                Stroke = new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Amber
                StrokeThickness = 2.5
            };
            PlotCanvas.Children.Add(polyline);
        }

        // Draw points for each cumulative node
        foreach (var pt in cumulativePoints)
        {
            var dot = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                Stroke = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                StrokeThickness = 1.5
            };
            Canvas.SetLeft(dot, pt.X - 3.5);
            Canvas.SetTop(dot, pt.Y - 3.5);
            PlotCanvas.Children.Add(dot);
        }
    }
}
