using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using StockExchangeSimulator.ViewModels;

namespace StockExchangeSimulator.Helpers
{
    public static class EquityChartBehavior
    {
        private static readonly Dictionary<Canvas, NotifyCollectionChangedEventHandler> _handlers = new();

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.RegisterAttached(
                "ItemsSource",
                typeof(IEnumerable<PortfolioSnapshotItemViewModel>),
                typeof(EquityChartBehavior),
                new PropertyMetadata(null, OnItemsSourceChanged));

        public static void SetItemsSource(DependencyObject element, IEnumerable<PortfolioSnapshotItemViewModel> value)
        {
            element.SetValue(ItemsSourceProperty, value);
        }

        public static IEnumerable<PortfolioSnapshotItemViewModel>? GetItemsSource(DependencyObject element)
        {
            return (IEnumerable<PortfolioSnapshotItemViewModel>?)element.GetValue(ItemsSourceProperty);
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Canvas canvas)
                return;

            DetachCollectionHandler(canvas, e.OldValue);
            AttachCollectionHandler(canvas, e.NewValue);

            canvas.SizeChanged -= Canvas_SizeChanged;
            canvas.SizeChanged += Canvas_SizeChanged;

            Draw(canvas);
        }

        private static void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Canvas canvas)
            {
                Draw(canvas);
            }
        }

        private static void AttachCollectionHandler(Canvas canvas, object? source)
        {
            if (source is not INotifyCollectionChanged observable)
                return;

            NotifyCollectionChangedEventHandler handler = (_, _) => Draw(canvas);
            observable.CollectionChanged += handler;
            _handlers[canvas] = handler;
        }

        private static void DetachCollectionHandler(Canvas canvas, object? source)
        {
            if (source is not INotifyCollectionChanged observable)
                return;

            if (_handlers.TryGetValue(canvas, out var handler))
            {
                observable.CollectionChanged -= handler;
                _handlers.Remove(canvas);
            }
        }

        private static void Draw(Canvas canvas)
        {
            canvas.Children.Clear();

            var items = GetItemsSource(canvas)?
                .OrderBy(x => x.Timestamp)
                .ToList();

            if (items == null || items.Count == 0)
            {
                DrawEmptyText(canvas, "Нет данных для equity curve");
                return;
            }

            double width = canvas.ActualWidth;
            double height = canvas.ActualHeight;

            if (width <= 20 || height <= 20)
                return;

            const double leftPad = 55;
            const double rightPad = 20;
            const double topPad = 20;
            const double bottomPad = 35;

            double plotWidth = Math.Max(10, width - leftPad - rightPad);
            double plotHeight = Math.Max(10, height - topPad - bottomPad);

            var values = items.Select(x => (double)x.TotalValue).ToList();

            double min = values.Min();
            double max = values.Max();

            if (Math.Abs(max - min) < 0.0001)
            {
                min -= 1;
                max += 1;
            }

            double padding = (max - min) * 0.1;
            if (padding <= 0)
                padding = 1;

            min -= padding;
            max += padding;

            DrawGrid(canvas, leftPad, topPad, plotWidth, plotHeight, min, max);

            var polyline = new Polyline
            {
                Stroke = Brushes.DeepSkyBlue,
                StrokeThickness = 2.2,
                SnapsToDevicePixels = true
            };

            for (int i = 0; i < items.Count; i++)
            {
                double x = leftPad + (items.Count == 1 ? plotWidth / 2 : (double)i / (items.Count - 1) * plotWidth);
                double y = topPad + (max - values[i]) / (max - min) * plotHeight;
                polyline.Points.Add(new Point(x, y));
            }

            canvas.Children.Add(polyline);

            DrawLastPoint(canvas, polyline.Points.Last(), values.Last());
            DrawTimeLabels(canvas, items, leftPad, topPad, plotWidth, plotHeight);
        }

        private static void DrawGrid(Canvas canvas, double leftPad, double topPad, double plotWidth, double plotHeight, double min, double max)
        {
            int horizontalLines = 5;

            for (int i = 0; i <= horizontalLines; i++)
            {
                double y = topPad + i * plotHeight / horizontalLines;

                var line = new Line
                {
                    X1 = leftPad,
                    X2 = leftPad + plotWidth,
                    Y1 = y,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromRgb(220, 225, 232)),
                    StrokeThickness = 1
                };
                canvas.Children.Add(line);

                double value = max - i * (max - min) / horizontalLines;

                var label = new TextBlock
                {
                    Text = value.ToString("F2"),
                    FontSize = 11,
                    Foreground = Brushes.DimGray
                };

                Canvas.SetLeft(label, 4);
                Canvas.SetTop(label, y - 10);
                canvas.Children.Add(label);
            }

            var yAxis = new Line
            {
                X1 = leftPad,
                X2 = leftPad,
                Y1 = topPad,
                Y2 = topPad + plotHeight,
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            };
            canvas.Children.Add(yAxis);

            var xAxis = new Line
            {
                X1 = leftPad,
                X2 = leftPad + plotWidth,
                Y1 = topPad + plotHeight,
                Y2 = topPad + plotHeight,
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            };
            canvas.Children.Add(xAxis);
        }

        private static void DrawLastPoint(Canvas canvas, Point point, double value)
        {
            var ellipse = new Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = Brushes.DeepSkyBlue,
                Stroke = Brushes.White,
                StrokeThickness = 1
            };

            Canvas.SetLeft(ellipse, point.X - 4);
            Canvas.SetTop(ellipse, point.Y - 4);
            canvas.Children.Add(ellipse);

            var label = new TextBlock
            {
                Text = value.ToString("F2"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black,
                Background = Brushes.White
            };

            Canvas.SetLeft(label, Math.Max(0, point.X - 20));
            Canvas.SetTop(label, Math.Max(0, point.Y - 24));
            canvas.Children.Add(label);
        }

        private static void DrawTimeLabels(
            Canvas canvas,
            List<PortfolioSnapshotItemViewModel> items,
            double leftPad,
            double topPad,
            double plotWidth,
            double plotHeight)
        {
            if (items.Count == 0)
                return;

            int labelCount = Math.Min(4, items.Count);

            for (int i = 0; i < labelCount; i++)
            {
                int index = labelCount == 1
                    ? 0
                    : (int)Math.Round(i * (items.Count - 1.0) / (labelCount - 1));

                double x = leftPad + (items.Count == 1 ? plotWidth / 2 : (double)index / (items.Count - 1) * plotWidth);

                var label = new TextBlock
                {
                    Text = items[index].Timestamp.ToString("HH:mm"),
                    FontSize = 11,
                    Foreground = Brushes.DimGray
                };

                Canvas.SetLeft(label, Math.Max(0, x - 18));
                Canvas.SetTop(label, topPad + plotHeight + 6);
                canvas.Children.Add(label);
            }
        }

        private static void DrawEmptyText(Canvas canvas, string text)
        {
            var label = new TextBlock
            {
                Text = text,
                FontSize = 14,
                Foreground = Brushes.Gray
            };

            Canvas.SetLeft(label, 20);
            Canvas.SetTop(label, 20);
            canvas.Children.Add(label);
        }
    }
}