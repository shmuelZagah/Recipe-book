using Microsoft.Maui.Graphics;
using Microsoft.Maui.Animations;
using System;
using System.Reflection;
using System.Collections.Generic;
using Animation = Microsoft.Maui.Controls.Animation;
using IImage = Microsoft.Maui.Graphics.IImage;
using Microsoft.Maui.Graphics.Platform; 

namespace Recipe_book.Views.Items;

public class LiquidBottomBar : ContentView
{
    public static readonly BindableProperty ItemsProperty =
        BindableProperty.Create(nameof(Items), typeof(IList<AnimatedBottomBarItem>), typeof(LiquidBottomBar), propertyChanged: OnItemsChanged);

    public IList<AnimatedBottomBarItem> Items
    {
        get => (IList<AnimatedBottomBarItem>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    // ──────────────────────────────────────────
    // מהירות האנימציה
    // ──────────────────────────────────────────
    private const uint AnimDuration = 540;

    private Grid _mainGrid;
    private GraphicsView _graphicsView;
    private LiquidBackground _liquidBackground;
    private Grid _iconsGrid;
    private List<Image> _icons = new();

    private int _selectedIndex = 0;
    private bool _isAnimating = false;
    public event EventHandler<int> TabSelected;

    public LiquidBottomBar()
    {
        HeightRequest = 90;
        VerticalOptions = LayoutOptions.End;
        HorizontalOptions = LayoutOptions.Fill;

        // משיכת צבע הבר מ-Colors.xaml
        Color secondary = GetResourceColor("BarNavigation", Color.FromArgb("#FFF3E3"));

        _liquidBackground = new LiquidBackground
        {
            BarColor = secondary,
        };

        // טעינת הטקסטורה של הספר מתוך Resources/Raw
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

    // מתודת עזר אסינכרונית לטעינת התמונה (מתאים לאנדרואיד ול-Windows)
    private async void LoadTextureImage(string fileName)
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
            _liquidBackground.BallTexture = PlatformImage.FromStream(stream);

            if (_graphicsView != null)
            {
                _graphicsView.Invalidate(); // ריענון הקנבס ברגע שהתמונה נטענה
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

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => OnTabTapped(index);

            var cell = new Grid { BackgroundColor = Colors.Transparent };
            cell.Children.Add(icon);
            cell.GestureRecognizers.Add(tap);

            Grid.SetColumn(cell, i);
            _iconsGrid.Children.Add(cell);
            _icons.Add(icon);
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

        // 1. האייקון הישן חוזר מיד לצבע הרגיל שלו (לא מחכה לכדור!)
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

        // 2. ממתינים שהכדור יגיע (חצי מזמן האנימציה)
        await Task.Delay((int)(AnimDuration / 2));

        // 3. רק עכשיו מחליפים את האייקון החדש לגרסה הלבנה הלחוצה
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

            double dentRadius = Math.Min(65, tabWidth * 0.85);

            if (distDent < dentRadius)
            {
                double dentFactor = 1 - (distDent / dentRadius);
                double dentEase = dentFactor * dentFactor;

                targetY = 38 + (dentEase * 65);
                targetX = (_liquidBackground.DentX - baseX) * 0.25 * dentEase;
                targetOpacity = 0.4 - (dentEase * 0.4);
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
                }
            }

            _icons[i].TranslationX = targetX;
            _icons[i].TranslationY = targetY;
            _icons[i].Scale = targetScale;
            _icons[i].Opacity = targetOpacity;
        }
    }

    // ──────────────────────────────────────────────────────────────────
    // מנוע הציור - משתני הציור נמצאים אך ורק כאן!
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

            // מיקום הכדור וגודלו (ריבוע של 50x50 פיקסלים)
            RectF ballRect = new RectF(bx - 25, by, 50, 50);

            // --- שינוי מרכזי 1: מחקנו לגמרי את שלב 1 (ציור הצל האחורי). ---
            // זה יעלים את ה"מסגרת" הלבנה שראית מסביב.

            canvas.SaveState();
            canvas.SetShadow(new SizeF(0, 6), 15, Colors.Black.WithAlpha(0.6f));
            canvas.FillColor = Colors.White;
            canvas.FillEllipse(ballRect);
            canvas.RestoreState();

            // 1. ציור הכדור עם הטקסטורה בלבד (המראה הנקי והשטוח)
            if (BallTexture != null)
            {
                canvas.SaveState(); // שמירת מצב נקי עבור החיתוך (Clip)

                // חותכים את אזור הציור לעיגול מושלם כדי שהתמונה לא תגלוש לפינות
                PathF clipPath = new PathF();
                clipPath.AppendEllipse(ballRect);
                canvas.ClipPath(clipPath);

                // --- שינוי מרכזי 2: ציור התמונה על כל שטח הריבוע (בלי ה-+2 ו--4). ---
                // זה מבטיח שהתמונה תמלא את העיגול עד הקצה ותיראה "נקייה".
                float zoom = 3;

                // מציירים את התמונה גדולה יותר, ומזיזים את נקודת ההתחלה אחורה ולמעלה כדי שתישאר ממורכזת
                canvas.DrawImage(BallTexture,
                                 ballRect.X - zoom,
                                 ballRect.Y - zoom,
                                 ballRect.Width + (zoom * 2),
                                 ballRect.Height + (zoom * 2));

                // --- שינוי מרכזי 3: מחקנו לגמרי את ה-Spherical Shading וה-BlendMode. Overlay. ---
                // עכשיו אין שום צבע או הילה מעל הטפט המקורי שלך.

                canvas.RestoreState(); // ניקוי החיתוך
            }
            else
            {
                // Fallback למקרה שהתמונה עדיין נטענה (משאירים את האפור)
                canvas.FillColor = Colors.LightGray;
                canvas.FillEllipse(ballRect);
            }

            // 2. חישוב מסלול הבר (נשאר ללא שינוי)
            PathF path = new PathF();
            path.MoveTo(0, 25);
            path.LineTo(dx - 50, 25);
            path.CurveTo(dx - 20, 25, dx - 35, 75, dx, 75);
            path.CurveTo(dx + 35, 75, dx + 20, 25, dx + 50, 25);
            path.LineTo(w, 25);
            path.LineTo(w, h);
            path.LineTo(0, h);
            path.Close();

            // 3. ציור הבר עם צל כלפי מעלה כדי שיבלוט (נשאר ללא שינוי)
            canvas.SaveState();
            canvas.SetShadow(new SizeF(0, -6), 15, Colors.Black.WithAlpha(0.2f));
            canvas.FillColor = BarColor;
            canvas.FillPath(path);
            canvas.RestoreState();
        }
    }
}