using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls.Shapes; // Required for RoundRectangle
using Animation = Microsoft.Maui.Controls.Animation;

namespace Recipe_book.Views.Items.bars;

public class StretchyTabBar : ContentView
{
    public static readonly BindableProperty ItemsProperty =
        BindableProperty.Create(nameof(Items), typeof(IList<TabItem>), typeof(StretchyTabBar), propertyChanged: OnItemsChanged);

    public IList<TabItem> Items
    {
        get => (IList<TabItem>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    private const uint AnimDuration = 550;

    private Grid _mainGrid;
    private GraphicsView _graphicsView;
    private LiquidMorphPill _liquidPill;
    private ScrollView _scrollView;
    private HorizontalStackLayout _tabsLayout;

    private List<Label> _labels = new();
    private List<Rect> _tabBounds = new();

    private int _selectedIndex = 0;
    private bool _isAnimating = false;
    public event EventHandler<string> TabSelected;

    public StretchyTabBar()
    {
        HeightRequest = 70; // Increased slightly to accommodate shadows and border
        HorizontalOptions = LayoutOptions.Fill;
        FlowDirection = FlowDirection.LeftToRight;

        Color primaryColor = GetResourceColor("Primary", Color.FromArgb("#0570A0"));

        _liquidPill = new LiquidMorphPill
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
            Spacing = 5, // Tighter spacing for a premium look
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

        // Outer UI styling: Rounded corners, border, and drop shadow
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
        if (bindable is StretchyTabBar bar && bar.Items != null)
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

        // RTL → הפוך
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
            _liquidPill.PinchAmount = 0;
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

        // ─────────────────────────────────────────────────────────────
        // FIX 1 – Proper liquid stretch animation:
        //   Phase 1 (0 → ~35%): the LEADING edge races to destination
        //                        while the trailing edge barely moves.
        //   Phase 2 (~35% → 100%): the TRAILING edge catches up.
        //   This creates the "stretched blob → snaps back" look.
        // ─────────────────────────────────────────────────────────────
        var animation = new Animation(progress =>
        {
            // Leading edge: fast first half
            double leadP = Easing.CubicOut.Ease(Math.Min(progress / 0.55, 1.0));
            // Trailing edge: delayed, catches up in second half
            double trailP = Easing.CubicOut.Ease(Math.Max((progress - 0.35) / 0.65, 0.0));

            if (movingRight)
            {
                // Right edge leads, left edge trails
                _liquidPill.CurrentLeft = startLeft + (endLeft - startLeft) * trailP;
                _liquidPill.CurrentRight = startRight + (endRight - startRight) * leadP;
            }
            else
            {
                // Left edge leads, right edge trails
                _liquidPill.CurrentLeft = startLeft + (endLeft - startLeft) * leadP;
                _liquidPill.CurrentRight = startRight + (endRight - startRight) * trailP;
            }

            // Pinch peaks at maximum stretch (~35% through)
            double squeezePhase = Math.Sin(progress * Math.PI);
            _liquidPill.PinchAmount = squeezePhase * 10;

            _graphicsView.Invalidate();
        }, 0, 1);

        animation.Commit(this, "LiquidTabPhysics", length: AnimDuration, finished: (v, c) =>
        {
            _isAnimating = false;
            _liquidPill.CurrentLeft = newBounds.Left;
            _liquidPill.CurrentRight = newBounds.Right;
            _liquidPill.PinchAmount = 0;
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

    class LiquidMorphPill : IDrawable
    {
        public Color PillColor { get; set; }
        public double CurrentLeft { get; set; }
        public double CurrentRight { get; set; }
        public double PinchAmount { get; set; }

        private const float PillHeight = 36f;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (CurrentRight <= CurrentLeft) return;

            float yCenter = dirtyRect.Height / 2f;
            float r = PillHeight / 2f;

            float left = (float)CurrentLeft;
            float right = (float)CurrentRight;

            // Guard: pill must be at least a circle
            if (right - left < PillHeight) right = left + PillHeight;

            float pinch = (float)PinchAmount;

            float yTop = yCenter - r;
            float yBottom = yCenter + r;

            // Centers of the two end circles
            float lcx = left + r;   // left  circle center X
            float rcx = right - r;   // right circle center X

            // ─────────────────────────────────────────────────────────
            // FIX 2 – Build the pill entirely with cubic Bézier curves
            //         instead of AddArc, which misbehaves in MAUI.
            //
            //  Shape (LTR):
            //   - Top straight section (with inward pinch)
            //   - Right semicircle (3 o'clock → 9 o'clock, clockwise)
            //   - Bottom straight section (with inward pinch)
            //   - Left semicircle  (9 o'clock → 3 o'clock, clockwise)
            //
            //  A semicircle via 2 cubic Bézier curves uses the
            //  well-known approximation constant κ ≈ 0.5523.
            // ─────────────────────────────────────────────────────────
            const float k = 0.5523f; // Bézier circle approximation
            float kr = k * r;

            using var path = new PathF();

            // ── Top-left arc start → top straight ───────────────────
            path.MoveTo(lcx, yTop);

            // Top edge (straight with pinch bowing inward = +pinch)
            path.CurveTo(
                lcx + (rcx - lcx) * 0.33f, yTop + pinch,
                lcx + (rcx - lcx) * 0.67f, yTop + pinch,
                rcx, yTop);

            // ── Right semicircle (top → right → bottom) ─────────────
            // top-right → right-middle
            path.CurveTo(rcx + kr, yTop,
                          right, yCenter - kr,
                          right, yCenter);
            // right-middle → bottom-right
            path.CurveTo(right, yCenter + kr,
                          rcx + kr, yBottom,
                          rcx, yBottom);

            // ── Bottom edge (straight with pinch bowing inward = -pinch) ──
            path.CurveTo(
                rcx - (rcx - lcx) * 0.33f, yBottom - pinch,
                rcx - (rcx - lcx) * 0.67f, yBottom - pinch,
                lcx, yBottom);

            // ── Left semicircle (bottom → left → top) ───────────────
            // bottom-left → left-middle
            path.CurveTo(lcx - kr, yBottom,
                          left, yCenter + kr,
                          left, yCenter);
            // left-middle → top-left
            path.CurveTo(left, yCenter - kr,
                          lcx - kr, yTop,
                          lcx, yTop);

            path.Close();

            canvas.SaveState();
            canvas.FillColor = PillColor;
            canvas.FillPath(path);
            canvas.RestoreState();
        }
    }


}