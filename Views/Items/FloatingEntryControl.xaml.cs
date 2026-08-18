using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace Recipe_book.Views.Items;

public partial class FloatingEntryControl : ContentView
{
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(FloatingEntryControl), default(string), BindingMode.TwoWay, propertyChanged: OnTextChanged);

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(FloatingEntryControl), default(string));

    public static readonly BindableProperty KeyboardProperty =
        BindableProperty.Create(nameof(Keyboard), typeof(Keyboard), typeof(FloatingEntryControl), Keyboard.Default);

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public Keyboard Keyboard
    {
        get => (Keyboard)GetValue(KeyboardProperty);
        set => SetValue(KeyboardProperty, value);
    }

    // ---- קבועי גיאומטריה/תזמון - במקום מספרים "קסומים" מפוזרים בקוד ----
    const double FloatScale = 0.8;   // גודל התווית והבועה במצב צף, יחסית לגודל המקורי
    const uint MoveLength = 260;     // משך תזוזת המיקום למעלה/למטה (מ"ש)
    const uint PopLength = 320;      // משך אנימציית הגודל/הופעת הבועה - קצת יותר ארוך כדי לתת ל-SpringOut מקום "לקפוץ"

    static readonly Color ActiveColor = Colors.Black;
    static readonly Color IdleColor = Color.FromArgb("#999999");
    static readonly Color IdleBorderColor = Color.FromArgb("#E0E0E0");

    bool _isLoaded;
    bool _isFloating;

    public FloatingEntryControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    void OnLoaded(object sender, EventArgs e)
    {
        // מוודא שהמצב הראשוני (למשל עריכת מתכון קיים עם טקסט כבר קיים בשדה)
        // נקבע ישר בלי אנימציה, ורק אחרי זה מתחילים להגיב לשינויים באנימציה
        _isLoaded = true;
        UpdateState(animate: false);
    }

    static void OnTextChanged(BindableObject bindable, object oldValue, object newValue)
    {
        // Binding חיצוני יכול לקבוע את Text לפני שהבקרה בכלל נטענה למסך (Loaded) -
        // במקרה כזה מתעלמים כאן, ו-OnLoaded כבר ידאג למצב הנכון ברגע שהיא תיטען
        if (bindable is FloatingEntryControl control && control._isLoaded)
            control.UpdateState(animate: true);
    }

    void OnEntryFocused(object sender, FocusEventArgs e) => UpdateState(animate: true);

    void OnEntryUnfocused(object sender, FocusEventArgs e) => UpdateState(animate: true);

    void UpdateState(bool animate)
    {
        bool isFloating = MainEntry.IsFocused || !string.IsNullOrEmpty(Text);

        // אם המצב לא באמת השתנה (למשל הקלדת תו נוסף בזמן שהשדה כבר פעיל וצף)
        // אין סיבה להריץ את האנימציה מחדש בכל הקשת מקש
        if (animate && isFloating == _isFloating)
            return;

        _isFloating = isFloating;

        FloatingLabel.TextColor = isFloating ? ActiveColor : IdleColor;
        MainBorder.Stroke = isFloating ? ActiveColor : IdleBorderColor;
        MainBorder.StrokeThickness = isFloating ? 2 : 1.5;

        double targetY = isFloating ? -(MainBorder.HeightRequest / 2) : 0;
        double targetScale = isFloating ? FloatScale : 1.0;
        double targetOpacity = isFloating ? 1 : 0;

        if (!animate)
        {
            FloatingContainer.TranslationY = targetY;
            FloatingContainer.Scale = targetScale;
            FloatingBubble.Opacity = targetOpacity;
            return;
        }

        // כניסה (float) מקבלת "קפיצה" קלה (Spring) בגודל - זה מה שנותן את תחושת ה"פופ" החיה.
        // יציאה בחזרה למצב רגיל נשארת חלקה, בלי קפיצה, כדי שלא תרגיש "מרעידה".
        Easing scaleEasing = isFloating ? Easing.SpringOut : Easing.CubicIn;

        _ = Task.WhenAll(
            FloatingContainer.TranslateTo(0, targetY, MoveLength, Easing.CubicOut),
            FloatingContainer.ScaleTo(targetScale, PopLength, scaleEasing),
            FloatingBubble.FadeTo(targetOpacity, isFloating ? PopLength : MoveLength, Easing.CubicInOut)
        );
    }
}