using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls.Shapes; 
using Animation = Microsoft.Maui.Controls.Animation;

namespace Recipe_book.Views.Items.bars;

public class ElasticGlideTabBar : ContentView
{
    public static readonly BindableProperty ItemsProperty =
        BindableProperty.Create(nameof(Items), typeof(IList<TabItem>), typeof(ElasticGlideTabBar), propertyChanged: OnItemsChanged);

    public IList<TabItem> Items
    {
        get => (IList<TabItem>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    private const uint AnimDuration = 550;

    private Grid _mainGrid;
    private GraphicsView _graphicsView;
    private AsymmetricPillDrawable _liquidPill;
    private ScrollView _scrollView;
    private HorizontalStackLayout _tabsLayout;

    private List<Label> _labels = new();
    private List<Rect> _tabBounds = new();

    private int _selectedIndex = 0;
    private bool _isAnimating = false;
    public event EventHandler<string> TabSelected;

    public ElasticGlideTabBar()
    {
        HeightRequest = 70;
        HorizontalOptions = LayoutOptions.Fill;
        FlowDirection = FlowDirection.LeftToRight; // RTL canvas fix

        Color primaryColor = GetResourceColor("Primary", Color.FromArgb("#0570A0"));

        _liquidPill = new AsymmetricPillDrawable
        {
            PillColor = primaryColor
        };

        _graphicsView = new GraphicsView
        {
            Drawable = _liquidPill,
            BackgroundColor = Colors.Transparent,
            Margin = new Thickness(5, 0)
        };

        _tabsLayout = new HorizontalStackLayout
        {
            Spacing = 5,
            Padding = new Thickness(10, 5),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Start
        };

        _scrollView = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = new Grid { Children = { _graphicsView, _tabsLayout } }
        };

        _mainGrid = new Grid { Children = { _scrollView } };

        // Outer styling
        var borderContainer = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(30) },
            Stroke = Color.FromArgb("#E8E8E8"),
            StrokeThickness = 1,
            BackgroundColor = Colors.White,
            Margin = new Thickness(15, 5, 15, 10),
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Offset = new Point(0, 4),
                Opacity = 0.08f,
                Radius = 8
            },
            Content = _mainGrid
        };

        Content = borderContainer;
    }

    private Color GetResourceColor(string key, Color defaultColor)
    {
        if (Application.Current != null && Application.Current.Resources.TryGetValue(key, out var value) && value is Color color)
            return color;
        return defaultColor;
    }

    private static void OnItemsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is ElasticGlideTabBar bar && bar.Items != null)
            bar.BuildTabs();
    }

    private void BuildTabs()
    {
        _tabsLayout.Children.Clear();
        _labels.Clear();
        _tabBounds.Clear();

        for (int i = 0; i < Items.Count; i++)
        {
            _labels.Add(null);
            _tabBounds.Add(Rect.Zero);
        }

        Color unselectedColor = GetResourceColor("Gray500", Color.FromArgb("#888"));

        for (int i = Items.Count - 1; i >= 0; i--)
        {
            int originalIndex = i;

            var label = new Label
            {
                Text = Items[originalIndex].Title,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = originalIndex == _selectedIndex ? Colors.White : unselectedColor,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                Padding = new Thickness(20, 10)
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => OnTabTapped(originalIndex);
            label.GestureRecognizers.Add(tap);

            label.SizeChanged += (s, e) => UpdateTabBounds();

            _tabsLayout.Children.Add(label);
            _labels[originalIndex] = label;
        }

        Device.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(150);
            await _scrollView.ScrollToAsync(_tabsLayout, ScrollToPosition.End, false);
        });
    }

    private void UpdateTabBounds()
    {
        bool allMeasured = true;
        for (int i = 0; i < _labels.Count; i++)
        {
            if (_labels[i] == null || _labels[i].Width <= 0) allMeasured = false;
            else _tabBounds[i] = new Rect(_labels[i].X, _labels[i].Y, _labels[i].Width, _labels[i].Height);
        }

        if (allMeasured && _liquidPill.CurrentRight == 0)
        {
            var activeBounds = _tabBounds[_selectedIndex];
            _liquidPill.CurrentLeft = activeBounds.Left;
            _liquidPill.CurrentRight = activeBounds.Right;
            _liquidPill.LeftHeight = 36;
            _liquidPill.RightHeight = 36;
            _graphicsView.Invalidate();
        }
    }

    private void OnTabTapped(int newIndex)
    {
        if (_selectedIndex == newIndex || _isAnimating || _tabBounds[newIndex].Width <= 0) return;

        int oldIndex = _selectedIndex;
        _selectedIndex = newIndex;
        _isAnimating = true;

        Color unselectedColor = GetResourceColor("Gray500", Color.FromArgb("#888"));
        _labels[oldIndex].TextColor = unselectedColor;
        _labels[newIndex].TextColor = Colors.White;

        TabSelected?.Invoke(this, Items[newIndex].Id);

        var oldBounds = _tabBounds[oldIndex];
        var newBounds = _tabBounds[newIndex];

        double startLeft = oldBounds.Left;
        double startRight = oldBounds.Right;
        double endLeft = newBounds.Left;
        double endRight = newBounds.Right;

        bool movingRight = endLeft > startLeft;

        var animation = new Animation(progress =>
        {
            // 1. Uniform X movement (no width stretching)
            double ease = Easing.CubicInOut.Ease(progress);
            _liquidPill.CurrentLeft = startLeft + (endLeft - startLeft) * ease;
            _liquidPill.CurrentRight = startRight + (endRight - startRight) * ease;

            // 2. Asymmetric height calculations based on leading/trailing edges
            // The leading edge shrinks and recovers first. The trailing edge happens later.
            double leadPhase = Math.Min(progress / 0.6, 1.0);
            double trailPhase = Math.Max((progress - 0.4) / 0.6, 0.0);

            if (movingRight)
            {
                // Right side arrives first
                _liquidPill.RightHeight = 36 - (Math.Sin(leadPhase * Math.PI) * 20);
                _liquidPill.LeftHeight = 36 - (Math.Sin(trailPhase * Math.PI) * 20);
            }
            else
            {
                // Left side arrives first
                _liquidPill.LeftHeight = 36 - (Math.Sin(leadPhase * Math.PI) * 20);
                _liquidPill.RightHeight = 36 - (Math.Sin(trailPhase * Math.PI) * 20);
            }

            _graphicsView.Invalidate();
        }, 0, 1);

        animation.Commit(this, "LiquidTabPhysics", length: AnimDuration, finished: (v, c) =>
        {
            _isAnimating = false;
            _liquidPill.CurrentLeft = newBounds.Left;
            _liquidPill.CurrentRight = newBounds.Right;
            _liquidPill.LeftHeight = 36;
            _liquidPill.RightHeight = 36;
            _graphicsView.Invalidate();
        });
    }

    public void SelectTab(int index)
    {
        if (index >= 0 && index < Items.Count)
        {
            OnTabTapped(index);
        }
    }

    // ──────────────────────────────────────────────────────────────────
    // Drawing Engine: Robust asymmetric pill using pure Bezier curves
    // ──────────────────────────────────────────────────────────────────
    class AsymmetricPillDrawable : IDrawable
    {
        public Color PillColor { get; set; }
        public double CurrentLeft { get; set; }
        public double CurrentRight { get; set; }
        public double LeftHeight { get; set; }
        public double RightHeight { get; set; }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (CurrentRight <= CurrentLeft) return;

            float yCenter = dirtyRect.Height / 2f;

            float hL = Math.Max((float)LeftHeight, 4f);
            float hR = Math.Max((float)RightHeight, 4f);

            float rL = hL / 2f;
            float rR = hR / 2f;

            float left = (float)CurrentLeft;
            float right = (float)CurrentRight;

            // FIX: Prevent circles from overlapping when text is very short (e.g., "א")
            if (right - left < rL + rR)
            {
                float diff = (rL + rR) - (right - left);
                left -= diff / 2f;
                right += diff / 2f;
            }

            float cxL = left + rL;
            float cxR = right - rR;

            const float k = 0.55228f;
            float krL = k * rL;
            float krR = k * rR;

            using var path = new PathF();

            // Top line
            path.MoveTo(cxL, yCenter - rL);
            path.LineTo(cxR, yCenter - rR);

            // Right curve
            path.CurveTo(cxR + krR, yCenter - rR, right, yCenter - krR, right, yCenter);
            path.CurveTo(right, yCenter + krR, cxR + krR, yCenter + rR, cxR, yCenter + rR);

            // Bottom line
            path.LineTo(cxL, yCenter + rL);

            // Left curve
            path.CurveTo(cxL - krL, yCenter + rL, left, yCenter + krL, left, yCenter);
            path.CurveTo(left, yCenter - krL, cxL - krL, yCenter - rL, cxL, yCenter - rL);

            path.Close();

            canvas.SaveState();
            canvas.FillColor = PillColor;
            canvas.FillPath(path);
            canvas.RestoreState();
        }
    }
}