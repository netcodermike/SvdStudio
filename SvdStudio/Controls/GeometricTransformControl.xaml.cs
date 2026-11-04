using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SvdStudio.ViewModels;

namespace SvdStudio.Controls;

public partial class GeometricTransformControl : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(Geometric2DViewModel),
            typeof(GeometricTransformControl),
            new PropertyMetadata(null, OnViewModelChanged));

    public Geometric2DViewModel? ViewModel
    {
        get => (Geometric2DViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public GeometricTransformControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Redraw();
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GeometricTransformControl ctrl)
        {
            ctrl.Redraw();
        }
    }

    private void TransformCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Redraw();
    }

    public void Redraw()
    {
        TransformCanvas.Children.Clear();

        var vm = ViewModel ?? DataContext as Geometric2DViewModel;
        if (vm == null || !vm.Is2DAvailable)
        {
            var txt = new TextBlock
            {
                Text = vm?.StatusMessage ?? "Geometric 2D visualization unavailable.",
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                FontSize = 12
            };
            Canvas.SetLeft(txt, 20);
            Canvas.SetTop(txt, 20);
            TransformCanvas.Children.Add(txt);
            return;
        }

        double w = TransformCanvas.ActualWidth;
        double h = TransformCanvas.ActualHeight;
        if (w < 80 || h < 80) return;

        double cx = w / 2.0;
        double cy = h / 2.0;
        double maxDim = Math.Min(w, h) / 2.0 - 30.0;
        double extent = Math.Max(1.8, vm.MaxCoordinateExtent);
        double scale = maxDim / extent;

        // Coordinate axes
        var axisBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
        var gridBrush = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));

        // Background grid circles
        for (double r = 1.0; r <= extent; r += 1.0)
        {
            double pixR = r * scale;
            var circle = new Ellipse
            {
                Width = pixR * 2,
                Height = pixR * 2,
                Stroke = gridBrush,
                StrokeThickness = 1
            };
            Canvas.SetLeft(circle, cx - pixR);
            Canvas.SetTop(circle, cy - pixR);
            TransformCanvas.Children.Add(circle);
        }

        // X and Y Axes
        var xAxis = new Line { X1 = 15, Y1 = cy, X2 = w - 15, Y2 = cy, Stroke = axisBrush, StrokeThickness = 1 };
        var yAxis = new Line { X1 = cx, Y1 = 15, X2 = cx, Y2 = h - 15, Stroke = axisBrush, StrokeThickness = 1 };
        TransformCanvas.Children.Add(xAxis);
        TransformCanvas.Children.Add(yAxis);

        // 1. Draw Unit Circle
        double unitR = 1.0 * scale;
        var unitCircle = new Ellipse
        {
            Width = unitR * 2,
            Height = unitR * 2,
            Stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248)), // Sky-400
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 }
        };
        Canvas.SetLeft(unitCircle, cx - unitR);
        Canvas.SetTop(unitCircle, cy - unitR);
        TransformCanvas.Children.Add(unitCircle);

        // 2. Draw Transformed Ellipse
        var ellipsePoints = new PointCollection();
        foreach (var pt in vm.TransformedEllipsePoints)
        {
            double px = cx + pt.X * scale;
            double py = cy - pt.Y * scale; // Inverted Y in WPF
            ellipsePoints.Add(new Point(px, py));
        }

        var ellipsePoly = new Polyline
        {
            Points = ellipsePoints,
            Stroke = new SolidColorBrush(Color.FromRgb(192, 132, 252)), // Purple-400
            StrokeThickness = 2.5
        };
        TransformCanvas.Children.Add(ellipsePoly);

        // 3. Draw Right Singular Vectors v1, v2 (Domain)
        DrawArrow(cx, cy, cx + vm.V1Point.X * scale, cy - vm.V1Point.Y * scale,
            Color.FromRgb(56, 189, 248), 2.0, "v₁");
        DrawArrow(cx, cy, cx + vm.V2Point.X * scale, cy - vm.V2Point.Y * scale,
            Color.FromRgb(56, 189, 248), 2.0, "v₂");

        // 4. Draw Transformed Principal Axes sigma1 * u1, sigma2 * u2 (Codomain)
        DrawArrow(cx, cy, cx + vm.TransformedU1Point.X * scale, cy - vm.TransformedU1Point.Y * scale,
            Color.FromRgb(52, 211, 153), 3.0, "σ₁·u₁"); // Emerald
        DrawArrow(cx, cy, cx + vm.TransformedU2Point.X * scale, cy - vm.TransformedU2Point.Y * scale,
            Color.FromRgb(251, 191, 36), 3.0, "σ₂·u₂"); // Amber
    }

    private void DrawArrow(double x1, double y1, double x2, double y2, Color color, double thickness, string label)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();

        var line = new Line
        {
            X1 = x1,
            Y1 = y1,
            X2 = x2,
            Y2 = y2,
            Stroke = brush,
            StrokeThickness = thickness
        };
        TransformCanvas.Children.Add(line);

        // Arrowhead
        double dx = x2 - x1;
        double dy = y2 - y1;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len > 5)
        {
            double udx = dx / len;
            double udy = dy / len;
            double headSize = 7;

            var poly = new Polygon
            {
                Fill = brush,
                Points = new PointCollection
                {
                    new(x2, y2),
                    new(x2 - headSize * udx - (headSize / 2.0) * -udy, y2 - headSize * udy - (headSize / 2.0) * udx),
                    new(x2 - headSize * udx + (headSize / 2.0) * -udy, y2 - headSize * udy + (headSize / 2.0) * udx)
                }
            };
            TransformCanvas.Children.Add(poly);
        }

        // Label
        var txt = new TextBlock
        {
            Text = label,
            Foreground = brush,
            FontSize = 11,
            FontWeight = FontWeights.Bold
        };
        Canvas.SetLeft(txt, x2 + (x2 >= x1 ? 5 : -25));
        Canvas.SetTop(txt, y2 + (y2 >= y1 ? 5 : -15));
        TransformCanvas.Children.Add(txt);
    }
}
