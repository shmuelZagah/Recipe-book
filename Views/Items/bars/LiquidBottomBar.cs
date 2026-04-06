using Microsoft.Maui.Graphics;
using Microsoft.Maui.Animations;
using System;
using System.Reflection;
using System.Collections.Generic;
using Animation = Microsoft.Maui.Controls.Animation;
using IImage = Microsoft.Maui.Graphics.IImage;
using Microsoft.Maui.Graphics.Platform;

namespace Recipe_book.Views.Items.bars;

public class LiquidBottomBar : ContentView
{
    public static readonly BindableProperty ItemsProperty =
        BindableProperty.Create(nameof(Items), typeof(IList<AnimatedBarItem>), typeof(LiquidBottomBar), propertyChanged: OnItemsChanged);

    public IList<AnimatedBarItem> Items
    {
        get => (IList<AnimatedBarItem>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    private const uint AnimDuration = 540;

    private Grid _mainGrid;
    private GraphicsView _graphicsView;
    private LiquidBackground _liquidBackground;
    private Grid _iconsGrid;
    private List<Image> _icons = new();
    private List<Label> _labels = new();

    private int _selectedIndex = 0;
    private bool _isAnimating = false; // חזרנו למנעול לחיצות!
    private int _animationOperationId = 0;
    public event EventHandler<int> TabSelected;

    public LiquidBottomBar()
    {
        HeightRequest = 90;
        VerticalOptions = LayoutOptions.End;
        HorizontalOptions = LayoutOptions.Fill;

        Color secondary = GetResourceColor("Primary", Color.FromArgb("#FFF3E3"));

        _liquidBackground = new LiquidBackground { BarColor = secondary };

        LoadTextureImage("book_texture.png");

        _graphicsView = new GraphicsView
        {
            Drawable = _liquidBackground,
            BackgroundColor = Colors.Transparent
        };

        _iconsGrid = new Grid();

        _mainGrid = new Grid { Children = { _graphicsView, _iconsGrid } };

        Content = _mainGrid;
        SizeChanged += OnSizeChanged;
    }

    private async void LoadTextureImage(string fileName)
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
            _liquidBackground.BallTexture = PlatformImage.FromStream(stream);

            if (_graphicsView != null) _graphicsView.Invalidate();
        }
        catch { }
    }

    private Color GetResourceColor(string key, Color defaultColor)
    {
        if (Application.Current != null && Application.Current.Resources.TryGetValue(key, out var value) && value is Color color)
            return color;
        return defaultColor;
    }

    private void OnSizeChanged(object sender, EventArgs e)
    {
        if (Width > 0 && Items?.Count > 0)
        {
            ForceUpdateStaticFrame(_selectedIndex);
        }
    }

    private static void OnItemsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is LiquidBottomBar bar && bar.Items != null)
            bar.BuildIcons();
    }

    private void BuildIcons()
    {
        _iconsGrid.ColumnDefinitions.Clear();
        _iconsGrid.Children.Clear();
        _icons.Clear();
        _labels.Clear();

        for (int i = 0; i < Items.Count; i++)
        {
            _iconsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

            int index = i;

            var icon = new Image
            {
                Source = (index == _selectedIndex && !string.IsNullOrEmpty(Items[i].SelectedIconSource))
                                      ? Items[i].SelectedIconSource
                                      : Items[i].IconSource,
                WidthRequest = 28,
                HeightRequest = 28,
                VerticalOptions = LayoutOptions.Start,
                HorizontalOptions = LayoutOptions.Center
            };

            var label = new Label
            {
                Text = Items[i].Title,
                FontSize = 12,
                TextColor = GetResourceColor("Secondary", Color.FromArgb("#FFF3E3")),
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Start,
                TranslationY = 66
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => OnTabTapped(index);

            var cell = new Grid { BackgroundColor = Colors.Transparent };
            cell.Children.Add(icon);
            cell.Children.Add(label);
            cell.GestureRecognizers.Add(tap);

            Grid.SetColumn(cell, i);
            _iconsGrid.Children.Add(cell);

            _icons.Add(icon);
            _labels.Add(label);
        }

        if (Width > 0) ForceUpdateStaticFrame(_selectedIndex);
    }

    private void ForceUpdateStaticFrame(int index)
    {
        if (Width <= 0 || Items == null || Items.Count == 0) return;
        double tabWidth = Width / Items.Count;
        int visualNew = (Items.Count - 1) - index;
        _liquidBackground.BallX = (visualNew * tabWidth) + (tabWidth / 2);
        _liquidBackground.DentX = _liquidBackground.BallX;
        _liquidBackground.BallY = 10;
        ApplyPhysicsToItems(tabWidth);
        _graphicsView.Invalidate();
    }

    // ──────────────────────────────────────────────────────────
    // 1. התנהגות בלחיצה ידנית (ננעלת כדי למנוע באגים)
    // ──────────────────────────────────────────────────────────
    private async void OnTabTapped(int newIndex)
    {
        // המנעול: אם אנחנו כבר באנימציה מלחיצה קודמת, מתעלמים (בדיוק כמו שרצית)
        if (_selectedIndex == newIndex || _isAnimating) return;

        _isAnimating = true;
        _selectedIndex = newIndex;
        TabSelected?.Invoke(this, newIndex); // קורא לעמוד הראשי להזיז את המסכים

        // הפונקציה הזו מריצה את האנימציה, אבל מחזירה שליטה אחרי *חצי* מהזמן (270ms)
        await PerformLiquidAnimation(newIndex);

        // התיקון: אנחנו ממתינים את החצי השני של הזמן, כדי שהמנעול יישאר נעול לכל ה-540ms!
        // ככה העמוד הראשי (שמסיים אחרי 350ms) תמיד יהיה מוכן לפני שהבר ישתחרר שוב.
        await Task.Delay((int)(AnimDuration / 2));

        _isAnimating = false; // רק עכשיו אפשר ללחוץ שוב
    }

    // ──────────────────────────────────────────────────────────
    // 2. התנהגות מגלילה (VIP - חותכת הכל ומתיישרת למסך)
    // ──────────────────────────────────────────────────────────
    public async void UpdateFromSwipe(int newIndex)
    {
        if (_selectedIndex == newIndex) return;

        _selectedIndex = newIndex;
        // שימו לב: לא מפעילים כאן TabSelected כדי לא לעשות לולאה כפולה עם העמוד הראשי!

        _isAnimating = true; // נועלים כדי שהמשתמש לא יוכל ללחוץ בזמן שההחלקה מתיישרת
        await PerformLiquidAnimation(newIndex);
        _isAnimating = false;
    }

    // מנוע האנימציה הראשי (פנימי)
    private async Task PerformLiquidAnimation(int newIndex)
    {
        _animationOperationId++;
        int myOperation = _animationOperationId;

        this.AbortAnimation("LiquidPhysics");

        // איפוס צבעים מהיר
        for (int i = 0; i < _icons.Count; i++)
        {
            _icons[i].Source = Items[i].IconSource;
        }

        double tabWidth = Width / Items.Count;
        int visualNew = (Items.Count - 1) - newIndex;
        double targetX = (visualNew * tabWidth) + (tabWidth / 2);

        // תופסים את המיקום הפיזי *הנוכחי* של הכדור, גם ב-X וגם ב-Y!
        double startX = _liquidBackground.BallX;
        if (startX <= 0) startX = targetX;

        double startY = _liquidBackground.BallY;
        if (startY < 10) startY = 10;

        // חישוב גובה הקפיצה: קפיצה קצרה = גובה נמוך, קפיצה ארוכה = גובה מקסימלי (60)
        double distanceX = Math.Abs(targetX - startX);
        double peakHeight = Math.Min(60, distanceX * 0.6);

        var animation = new Animation(progress =>
        {
            _liquidBackground.BallX = startX + (targetX - startX) * progress;
            _liquidBackground.DentX = _liquidBackground.BallX;

            // התיקון: שילוב חלק של הנפילה מהקפיצה הקודמת, אל תוך הקפיצה החדשה באוויר!
            double arc = peakHeight * Math.Sin(progress * Math.PI);
            double decay = (startY - 10) * (1 - progress);
            _liquidBackground.BallY = 10 + arc + decay;

            ApplyPhysicsToItems(tabWidth);
            _graphicsView.Invalidate();

        }, 0, 1, Easing.CubicInOut);

        animation.Commit(this, "LiquidPhysics", length: AnimDuration, finished: (v, c) =>
        {
            if (myOperation == _animationOperationId)
            {
                ForceUpdateStaticFrame(newIndex);
            }
        });

        await Task.Delay((int)(AnimDuration / 2));

        if (myOperation == _animationOperationId && newIndex >= 0 && newIndex < _icons.Count)
        {
            if (!string.IsNullOrEmpty(Items[newIndex].SelectedIconSource))
                _icons[newIndex].Source = Items[newIndex].SelectedIconSource;
        }
    }
    private void ApplyPhysicsToItems(double tabWidth)
    {
        for (int i = 0; i < _icons.Count; i++)
        {
            int visualI = (Items.Count - 1) - i;
            double baseX = (visualI * tabWidth) + (tabWidth / 2);

            double distDent = Math.Abs(_liquidBackground.DentX - baseX);
            double distBall = Math.Abs(_liquidBackground.BallX - baseX);

            double targetY = 38, targetX = 0, targetScale = 1.0, targetOpacity = 0.4, labelOpacity = 1.0;
            double dentRadius = Math.Min(65, tabWidth * 0.85);

            if (distDent < dentRadius)
            {
                double dentFactor = 1 - (distDent / dentRadius);
                double dentEase = dentFactor * dentFactor;

                targetY = 38 + (dentEase * 65);
                targetX = (_liquidBackground.DentX - baseX) * 0.25 * dentEase;
                targetOpacity = 0.4 - (dentEase * 0.4);
                labelOpacity = Math.Max(0, 1.0 - (dentEase * 2.0));
            }

            double ballRadius = 45;
            if (distBall < ballRadius)
            {
                double ballFactor = 1 - (distBall / ballRadius);
                double ballEase = Math.Pow(ballFactor, 1.5);

                double riderY = _liquidBackground.BallY + 12;
                double riderX = (_liquidBackground.BallX - baseX) * 0.15;
                double riderScale = 1.0 + (ballFactor * 0.05);
                double riderOpacity = 0.4 + (ballFactor * 0.6);

                targetY = targetY + (riderY - targetY) * ballEase;
                targetX = targetX + (riderX - targetX) * ballEase;
                targetScale = targetScale + (riderScale - targetScale) * ballEase;
                targetOpacity = targetOpacity + (riderOpacity - targetOpacity) * ballEase;
                labelOpacity = Math.Max(0, labelOpacity - ballFactor);
            }

            _icons[i].TranslationX = targetX;
            _icons[i].TranslationY = targetY;
            _icons[i].Scale = targetScale;
            _icons[i].Opacity = targetOpacity;
            _labels[i].Opacity = labelOpacity;
        }
    }

    class LiquidBackground : IDrawable
    {
        public Color BarColor { get; set; }
        public IImage BallTexture { get; set; }
        public double DentX { get; set; } = -1;
        public double BallX { get; set; } = -1;
        public double BallY { get; set; } = 10;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            if (DentX == -1) return;

            float w = dirtyRect.Width;
            float h = dirtyRect.Height;
            float dx = (float)DentX;
            float bx = (float)BallX;
            float by = (float)BallY;

            RectF ballRect = new RectF(bx - 25, by, 50, 50);

            canvas.SaveState();
            canvas.SetShadow(new SizeF(0, 6), 15, Colors.Black.WithAlpha(0.6f));
            canvas.FillColor = Colors.White;
            canvas.FillEllipse(ballRect);
            canvas.RestoreState();

            if (BallTexture != null)
            {
                canvas.SaveState();
                PathF clipPath = new PathF();
                clipPath.AppendEllipse(ballRect);
                canvas.ClipPath(clipPath);

                float zoom = 3;
                canvas.DrawImage(BallTexture, ballRect.X - zoom, ballRect.Y - zoom, ballRect.Width + (zoom * 2), ballRect.Height + (zoom * 2));
                canvas.RestoreState();
            }
            else
            {
                canvas.FillColor = Colors.LightGray;
                canvas.FillEllipse(ballRect);
            }

            PathF path = new PathF();
            path.MoveTo(0, 25);
            path.LineTo(dx - 50, 25);
            path.CurveTo(dx - 20, 25, dx - 35, 75, dx, 75);
            path.CurveTo(dx + 35, 75, dx + 20, 25, dx + 50, 25);
            path.LineTo(w, 25);
            path.LineTo(w, h);
            path.LineTo(0, h);
            path.Close();

            canvas.SaveState();
            canvas.SetShadow(new SizeF(0, -6), 15, Colors.Black.WithAlpha(0.2f));
            canvas.FillColor = BarColor;
            canvas.FillPath(path);
            canvas.RestoreState();
        }
    }
}