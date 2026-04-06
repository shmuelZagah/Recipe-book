using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls.Shapes;
using Animation = Microsoft.Maui.Controls.Animation;

namespace Recipe_book.Views.Items.bars;

public class LiquidTabBar : ContentView
{
    public static readonly BindableProperty ItemsProperty =
        BindableProperty.Create(nameof(Items), typeof(IList<TabItem>), typeof(LiquidTabBar), propertyChanged: OnItemsChanged);

    public IList<TabItem> Items
    {
        get => (IList<TabItem>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    private const uint AnimDuration = 520;

    private Grid _mainGrid;
    private GraphicsView _graphicsView;
    private PillDrawable _pill;
    private ScrollView _scrollView;
    private HorizontalStackLayout _tabsLayout;

    private List<Label> _labels = new();
    private List<Rect> _tabBounds = new();

    private int _selectedIndex = 0;
    private bool _isAnimating = false;
    public event EventHandler<string> TabSelected;

    public LiquidTabBar()
    {
        HeightRequest = 70;
        HorizontalOptions = LayoutOptions.Fill;
        FlowDirection = FlowDirection.LeftToRight;

        Color primaryColor = GetResourceColor("Primary", Color.FromArgb("#0570A0"));

        _pill = new PillDrawable
        {
            PillColor = primaryColor
        };

        _graphicsView = new GraphicsView
        {
            Drawable = _pill,
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

        // עיצוב חיצוני יוקרתי (מסגרת, צל ופינות)
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
        if (bindable is LiquidTabBar bar && bar.Items != null)
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

        // נשאר הפוך (כי RTL)
        for (int i = Items.Count - 1; i >= 0; i--)
        {
            int idx = i;

            var lbl = new Label
            {
                Text = Items[idx].Title,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = idx == _selectedIndex ? Colors.White : unselectedColor,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                Padding = new Thickness(20, 10)
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => OnTabTapped(idx);
            lbl.GestureRecognizers.Add(tap);

            lbl.SizeChanged += (_, _) => UpdateTabBounds();

            _tabsLayout.Children.Add(lbl);
            _labels[idx] = lbl;
        }

        Device.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(150);

            // 🔥 זה הפתרון האמיתי:
            // ב-RTL צריך לגלול ל-END כדי להגיע לימין
            await _scrollView.ScrollToAsync(_tabsLayout, ScrollToPosition.End, false);
        });
    }

    private void UpdateTabBounds()
    {
        bool allMeasured = true;
        for (int i = 0; i < _labels.Count; i++)
        {
            if (_labels[i] == null || _labels[i].Width <= 0) { allMeasured = false; continue; }
            _tabBounds[i] = new Rect(_labels[i].X, _labels[i].Y, _labels[i].Width, _labels[i].Height);
        }

        if (allMeasured && _pill.PillLeft == 0 && _pill.PillRight == 0)
        {
            var b = _tabBounds[_selectedIndex];
            _pill.PillLeft = b.Left;
            _pill.PillRight = b.Right;
            _pill.SrcLeft = b.Left;
            _pill.SrcRight = b.Right;
            _pill.DstLeft = b.Left;
            _pill.DstRight = b.Right;
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

        var src = _tabBounds[oldIndex];
        var dst = _tabBounds[newIndex];

        _pill.SrcLeft = src.Left;
        _pill.SrcRight = src.Right;
        _pill.DstLeft = dst.Left;
        _pill.DstRight = dst.Right;

        double startL = src.Left, startR = src.Right;
        double endL = dst.Left, endR = dst.Right;

        var anim = new Animation(raw =>
        {
            // תנועה חלקה ואחידה של כל המלבן יחד - הרוחב נשאר זהה
            double pos = Easing.CubicInOut.Ease(raw);
            _pill.PillLeft = startL + (endL - startL) * pos;
            _pill.PillRight = startR + (endR - startR) * pos;

            _graphicsView.Invalidate();
        }, 0, 1);

        anim.Commit(this, "LiquidTabPhysics", length: AnimDuration, finished: (_, _) =>
        {
            _isAnimating = false;
            _pill.PillLeft = endL;
            _pill.PillRight = endR;
            _pill.SrcLeft = endL;
            _pill.SrcRight = endR;
            _pill.DstLeft = endL;
            _pill.DstRight = endR;
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
    // מנוע הציור מבוסס מפת הגבהים - גרסה חסינת באגים ואורגנית
    // ──────────────────────────────────────────────────────────────────
    class PillDrawable : IDrawable
    {
        public Color PillColor { get; set; }
        public double PillLeft { get; set; }
        public double PillRight { get; set; }
        public double SrcLeft { get; set; }
        public double SrcRight { get; set; }
        public double DstLeft { get; set; }
        public double DstRight { get; set; }

        private const float FullH = 36f; // גובה על המילה
        private const float MinH = 10f;  // גובה ה"צינור" ברווח

        // הגדלנו את רדיוס ההשפעה כדי לתת מראה שואב, מגנטי וחי יותר (לא רובוטי)
        private const float EdgeSm = 35f;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            float left = (float)PillLeft;
            float right = (float)PillRight;
            if (right <= left + 1f) return;

            float cy = dirtyRect.Height / 2f;

            // מרכזי העיגולים של הקצוות. מגן מפני קריסה אם המילה קצרה מדי.
            float cxL = left + (FullH / 2f);
            float cxR = right - (FullH / 2f);
            if (cxR < cxL) cxR = cxL;

            // בדיקת מפת הגבהים בקצוות (קובע את הרדיוס של חצאי העיגול)
            float rL = HeightAt(cxL) / 2f;
            float rR = HeightAt(cxR) / 2f;

            using var path = new PathF();
            const float k = 0.55228f; // קבוע לחצאי עיגול מושלמים
            float krL = k * rL;
            float krR = k * rR;

            // 1. קו עליון: עובר דרך נקודות במפת הגבהים
            path.MoveTo(cxL, cy - rL);
            const int N = 25; // רזולוציית הדגימה של הצינור
            for (int i = 1; i <= N; i++)
            {
                float t = (float)i / N;
                float x = cxL + t * (cxR - cxL);
                float h = HeightAt(x);
                path.LineTo(x, cy - h / 2f);
            }

            // 2. קצה ימני מעוגל בצורה מושלמת תמיד
            path.CurveTo(cxR + krR, cy - rR, cxR + rR, cy - krR, cxR + rR, cy);
            path.CurveTo(cxR + rR, cy + krR, cxR + krR, cy + rR, cxR, cy + rR);

            // 3. קו תחתון: חזרה שמאלה
            for (int i = N - 1; i >= 0; i--)
            {
                float t = (float)i / N;
                float x = cxL + t * (cxR - cxL);
                float h = HeightAt(x);
                path.LineTo(x, cy + h / 2f);
            }

            // 4. קצה שמאלי מעוגל בצורה מושלמת תמיד
            path.CurveTo(cxL - krL, cy + rL, cxL - rL, cy + krL, cxL - rL, cy);
            path.CurveTo(cxL - rL, cy - krL, cxL - krL, cy - rL, cxL, cy - rL);

            path.Close();

            canvas.SaveState();
            canvas.FillColor = PillColor;
            canvas.FillPath(path);
            canvas.RestoreState();
        }

        // פונקציה לבדיקת הגובה לפי מפת הגבהים של המילה הנוכחית והמילה ביעד
        private float HeightAt(float x)
        {
            float src = ZoneScale(x, (float)SrcLeft, (float)SrcRight);
            float dst = ZoneScale(x, (float)DstLeft, (float)DstRight);
            return MinH + (FullH - MinH) * Math.Max(src, dst);
        }

        // חישוב מתמטי רך שיוצר את הצינור בין המילים
        private static float ZoneScale(float x, float zL, float zR)
        {
            if (x >= zL && x <= zR) return 1f; // אנחנו על מילה - גובה מקסימלי

            // ריכוך (Smoothstep) כשאנחנו מתקרבים/מתרחקים מהמילה
            if (x < zL)
            {
                float t = Math.Max(0, 1f - (zL - x) / EdgeSm);
                return t * t * (3f - 2f * t);
            }
            else
            {
                float t = Math.Max(0, 1f - (x - zR) / EdgeSm);
                return t * t * (3f - 2f * t);
            }
        }
    }
}