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

    // ──────────────────────────────────────────
    // Animation Speed
    // ──────────────────────────────────────────
    private const uint AnimDuration = 540;

    private Grid _mainGrid;
    private GraphicsView _graphicsView;
    private LiquidBackground _liquidBackground;
    private Grid _iconsGrid;
    private List<Image> _icons = new();
    private List<Label> _labels = new(); // Added list for the text labels

    private int _selectedIndex = 0;
    private bool _isAnimating = false;
    public event EventHandler<int> TabSelected;

    public LiquidBottomBar()
    {
        HeightRequest = 90;
        VerticalOptions = LayoutOptions.End;
        HorizontalOptions = LayoutOptions.Fill;

        // Fetch bar color from Colors.xaml
        Color secondary = GetResourceColor("Primary", Color.FromArgb("#FFF3E3"));

        _liquidBackground = new LiquidBackground
        {
            BarColor = secondary,
        };

        // Load book texture from Resources/Raw
        LoadTextureImage("book_texture.png");

        _graphicsView = new GraphicsView
        {
            Drawable = _liquidBackground,
            BackgroundColor = Colors.Transparent
        };

        _iconsGrid = new Grid();

        _mainGrid = new Grid
        {
            Children = { _graphicsView, _iconsGrid }
        };

        Content = _mainGrid;
        SizeChanged += OnSizeChanged;
    }

    // Async helper method to load the image (supports Android and Windows)
    private async void LoadTextureImage(string fileName)
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
            _liquidBackground.BallTexture = PlatformImage.FromStream(stream);

            if (_graphicsView != null)
            {
                _graphicsView.Invalidate(); // Refresh canvas once image is loaded
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading texture: {ex.Message}");
        }
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
            UpdatePhysicsFrame(_selectedIndex, _selectedIndex, 1.0);
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
                TextColor = GetResourceColor("Secondary", Color.FromArgb("#FFF3E3")), // Dark gray for inactive tabs
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Start,
                TranslationY = 66 // Positioned beautifully right below the icon
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

        if (Width > 0)
        {
            UpdatePhysicsFrame(_selectedIndex, _selectedIndex, 1.0);
        }
    }

    private async void OnTabTapped(int newIndex)
    {
        if (_selectedIndex == newIndex || _isAnimating) return;

        int oldIndex = _selectedIndex;
        _selectedIndex = newIndex;
        _isAnimating = true;
        TabSelected?.Invoke(this, newIndex);

        // 1. Old icon returns to its normal color immediately (doesn't wait for the ball!)
        if (oldIndex >= 0 && oldIndex < _icons.Count)
        {
            _icons[oldIndex].Source = Items[oldIndex].IconSource;
        }

        var animation = new Animation(progress =>
        {
            UpdatePhysicsFrame(oldIndex, newIndex, progress);
        }, 0, 1, Easing.CubicInOut);

        animation.Commit(this, "LiquidPhysics", length: AnimDuration, finished: (v, c) =>
        {
            _isAnimating = false;
            UpdatePhysicsFrame(oldIndex, newIndex, 1.0);
        });

        // 2. Wait for the ball to arrive (half the animation duration)
        await Task.Delay((int)(AnimDuration / 2));

        // 3. Now change the new icon to the pressed white version
        if (newIndex >= 0 && newIndex < _icons.Count && !string.IsNullOrEmpty(Items[newIndex].SelectedIconSource))
        {
            _icons[newIndex].Source = Items[newIndex].SelectedIconSource;
        }
    }

    private void UpdatePhysicsFrame(int oldIndex, int newIndex, double p)
    {
        if (Width <= 0 || Items == null || Items.Count == 0 || _icons.Count != Items.Count) return;

        double tabWidth = Width / Items.Count;

        int visualOld = (Items.Count - 1) - oldIndex;
        int visualNew = (Items.Count - 1) - newIndex;

        double startX = (visualOld * tabWidth) + (tabWidth / 2);
        double endX = (visualNew * tabWidth) + (tabWidth / 2);

        _liquidBackground.BallX = startX + (endX - startX) * p;
        _liquidBackground.DentX = _liquidBackground.BallX;

        _liquidBackground.BallY = 10 + 60 * Math.Sin(p * Math.PI);

        _graphicsView.Invalidate();

        for (int i = 0; i < _icons.Count; i++)
        {
            int visualI = (Items.Count - 1) - i;
            double baseX = (visualI * tabWidth) + (tabWidth / 2);

            double distDent = Math.Abs(_liquidBackground.DentX - baseX);
            double distBall = Math.Abs(_liquidBackground.BallX - baseX);

            double targetY = 38;
            double targetX = 0;
            double targetScale = 1.0;
            double targetOpacity = 0.4;

            // Text label starts fully visible
            double labelOpacity = 1.0;

            double dentRadius = Math.Min(65, tabWidth * 0.85);

            if (distDent < dentRadius)
            {
                double dentFactor = 1 - (distDent / dentRadius);
                double dentEase = dentFactor * dentFactor;

                targetY = 38 + (dentEase * 65);
                targetX = (_liquidBackground.DentX - baseX) * 0.25 * dentEase;
                targetOpacity = 0.4 - (dentEase * 0.4);

                // Fade out label quickly when the liquid pushes down on it
                labelOpacity = Math.Max(0, 1.0 - (dentEase * 2.0));
            }

            if (i == oldIndex || i == newIndex)
            {
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

                    // Fade out the label smoothly as the ball picks up the icon
                    labelOpacity = Math.Max(0, labelOpacity - ballFactor);
                }
            }

            _icons[i].TranslationX = targetX;
            _icons[i].TranslationY = targetY;
            _icons[i].Scale = targetScale;
            _icons[i].Opacity = targetOpacity;

            // Apply opacity physics to the label
            _labels[i].Opacity = labelOpacity;
        }
    }

    public void SelectTab(int index)
    {
        OnTabTapped(index);
    }



    // ──────────────────────────────────────────────────────────────────
    // Drawing Engine - Drawing variables belong here only!
    // ──────────────────────────────────────────────────────────────────
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

            // Ball position and size (50x50 pixels square)
            RectF ballRect = new RectF(bx - 25, by, 50, 50);

            // --- Major Change 1: Completely removed step 1 (drawing the back shadow). ---
            // This removes the white "frame" you saw around it.

            canvas.SaveState();
            canvas.SetShadow(new SizeF(0, 6), 15, Colors.Black.WithAlpha(0.6f));
            canvas.FillColor = Colors.White;
            canvas.FillEllipse(ballRect);
            canvas.RestoreState();

            // 1. Draw the ball with texture only (clean and flat look)
            if (BallTexture != null)
            {
                canvas.SaveState(); // Save clean state for clipping

                // Clip the drawing area to a perfect circle so the image doesn't overflow to corners
                PathF clipPath = new PathF();
                clipPath.AppendEllipse(ballRect);
                canvas.ClipPath(clipPath);

                // --- Major Change 2: Draw the image on the full square area (without +2 and -4). ---
                // This ensures the image fills the circle to the edge and looks "clean".
                float zoom = 3;

                // Draw the image larger, and move the starting point back and up to keep it centered
                canvas.DrawImage(BallTexture,
                                 ballRect.X - zoom,
                                 ballRect.Y - zoom,
                                 ballRect.Width + (zoom * 2),
                                 ballRect.Height + (zoom * 2));

                // --- Major Change 3: Completely removed Spherical Shading and BlendMode.Overlay. ---
                // Now there is no color or halo above your original wallpaper.

                canvas.RestoreState(); // Clear clipping
            }
            else
            {
                // Fallback in case the image hasn't loaded yet (leave gray)
                canvas.FillColor = Colors.LightGray;
                canvas.FillEllipse(ballRect);
            }

            // 2. Calculate bar path (remains unchanged)
            PathF path = new PathF();
            path.MoveTo(0, 25);
            path.LineTo(dx - 50, 25);
            path.CurveTo(dx - 20, 25, dx - 35, 75, dx, 75);
            path.CurveTo(dx + 35, 75, dx + 20, 25, dx + 50, 25);
            path.LineTo(w, 25);
            path.LineTo(w, h);
            path.LineTo(0, h);
            path.Close();

            // 3. Draw the bar with an upward shadow so it pops out (remains unchanged)
            canvas.SaveState();
            canvas.SetShadow(new SizeF(0, -6), 15, Colors.Black.WithAlpha(0.2f));
            canvas.FillColor = BarColor;
            canvas.FillPath(path);
            canvas.RestoreState();
        }
    }
}