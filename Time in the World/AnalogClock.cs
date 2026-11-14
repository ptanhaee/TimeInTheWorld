using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Time_in_the_World;

public sealed class AnalogClock : FrameworkElement
{
    public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(
        nameof(Time), typeof(DateTimeOffset), typeof(AnalogClock),
        new FrameworkPropertyMetadata(default(DateTimeOffset), FrameworkPropertyMetadataOptions.AffectsRender));

    public DateTimeOffset Time
    {
        get => (DateTimeOffset)GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var size = Math.Min(ActualWidth, ActualHeight);
        if (size <= 0) return;
        dc.PushTransform(new TranslateTransform((ActualWidth - size) / 2, (ActualHeight - size) / 2));
        dc.PushTransform(new ScaleTransform(size / 200, size / 200));
        var face = Brush("#141B27");
        var muted = Brush("#8190A8");
        var white = Brush("#F1F4FA");
        var accent = Brush("#9CE6C5");
        dc.DrawEllipse(face, new Pen(Brush("#354055"), 1), new Point(100, 100), 96, 96);
        for (var i = 0; i < 60; i++)
        {
            var major = i % 5 == 0;
            dc.DrawLine(new Pen(muted, major ? 2 : 1), PointAt(i * 6, major ? 79 : 86), PointAt(i * 6, 90));
        }
        foreach (var hour in new[] { 12, 3, 6, 9 })
        {
            var text = new FormattedText(hour.ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 12, muted, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            var point = PointAt(hour * 30, 65);
            dc.DrawText(text, new Point(point.X - text.Width / 2, point.Y - text.Height / 2));
        }
        DrawHand(dc, (Time.Hour % 12 + Time.Minute / 60.0) * 30, 47, 5, white);
        DrawHand(dc, (Time.Minute + Time.Second / 60.0) * 6, 69, 3, white);
        DrawHand(dc, Time.Second * 6, 76, 1.5, accent);
        dc.DrawEllipse(accent, null, new Point(100, 100), 4, 4);
        dc.Pop();
        dc.Pop();
    }

    private static void DrawHand(DrawingContext dc, double angle, double length, double thickness, Brush brush)
    {
        var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawLine(pen, PointAt(angle + 180, 9), PointAt(angle, length));
    }

    private static Point PointAt(double angle, double radius)
    {
        var radians = angle * Math.PI / 180;
        return new Point(100 + Math.Sin(radians) * radius, 100 - Math.Cos(radians) * radius);
    }

    private static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
