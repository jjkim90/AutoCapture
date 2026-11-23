using System.Windows;
using System.Windows.Media;

namespace AutoCapture.Wpf.Controls
{
    /// <summary>0~1 진행률을 원형 호로 그린다.</summary>
    public class ProgressRing : FrameworkElement
    {
        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register(nameof(Progress), typeof(double), typeof(ProgressRing),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StrokeThicknessProperty =
            DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(ProgressRing),
                new FrameworkPropertyMetadata(3.5, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TrackBrushProperty =
            DependencyProperty.Register(nameof(TrackBrush), typeof(Brush), typeof(ProgressRing),
                new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IndicatorBrushProperty =
            DependencyProperty.Register(nameof(IndicatorBrush), typeof(Brush), typeof(ProgressRing),
                new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));

        public double Progress
        {
            get => (double)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        public double StrokeThickness
        {
            get => (double)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        public Brush TrackBrush
        {
            get => (Brush)GetValue(TrackBrushProperty);
            set => SetValue(TrackBrushProperty, value);
        }

        public Brush IndicatorBrush
        {
            get => (Brush)GetValue(IndicatorBrushProperty);
            set => SetValue(IndicatorBrushProperty, value);
        }

        protected override void OnRender(DrawingContext dc)
        {
            var size = Math.Min(ActualWidth, ActualHeight);
            if (size <= 0) return;

            var thickness = StrokeThickness;
            var radius = (size - thickness) / 2;
            var center = new Point(ActualWidth / 2, ActualHeight / 2);
            dc.DrawEllipse(null, new Pen(TrackBrush, thickness), center, radius, radius);

            var progress = Math.Clamp(Progress, 0, 1);
            if (progress <= 0) return;

            var pen = new Pen(IndicatorBrush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            if (progress >= 0.9999)
            {
                dc.DrawEllipse(null, pen, center, radius, radius);
                return;
            }

            var angle = progress * 2 * Math.PI;
            var start = new Point(center.X, center.Y - radius);
            var end = new Point(center.X + radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle));
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(start, false, false);
                context.ArcTo(end, new Size(radius, radius), 0, progress > 0.5, SweepDirection.Clockwise, true, false);
            }
            geometry.Freeze();
            dc.DrawGeometry(null, pen, geometry);
        }
    }
}
