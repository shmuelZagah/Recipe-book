using Microsoft.Maui.Controls.Shapes;
using System.Windows.Input;

namespace Recipe_book.Views.Items;

public partial class FloatingActionControl : ContentView
{
    public FloatingActionControl()
    {
        InitializeComponent();
        UpdateShape();
    }

    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(FloatingActionControl), string.Empty, propertyChanged: (b, o, n) => ((FloatingActionControl)b).OnPropertyChanged(nameof(HasText)));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool HasText => !string.IsNullOrEmpty(Text);

    public static readonly BindableProperty IconSourceProperty = BindableProperty.Create(nameof(IconSource), typeof(ImageSource), typeof(FloatingActionControl), null);
    public ImageSource IconSource { get => (ImageSource)GetValue(IconSourceProperty); set => SetValue(IconSourceProperty, value); }

    public static readonly BindableProperty ButtonColorProperty = BindableProperty.Create(nameof(ButtonColor), typeof(Color), typeof(FloatingActionControl), Color.FromArgb("#0570A0"));
    public Color ButtonColor { get => (Color)GetValue(ButtonColorProperty); set => SetValue(ButtonColorProperty, value); }

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(FloatingActionControl), Colors.White);
    public Color TextColor { get => (Color)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }

    // Attached a property changed event to recalculate shape when size changes
    public static readonly BindableProperty ButtonSizeProperty = BindableProperty.Create(
        nameof(ButtonSize),
        typeof(double),
        typeof(FloatingActionControl),
        60.0,
        propertyChanged: OnButtonSizeChanged);

    public double ButtonSize { get => (double)GetValue(ButtonSizeProperty); set => SetValue(ButtonSizeProperty, value); }

    public static readonly BindableProperty IconSizeProperty = BindableProperty.Create(nameof(IconSize), typeof(double), typeof(FloatingActionControl), 26.0);
    public double IconSize { get => (double)GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(FloatingActionControl), null);
    public ICommand Command { get => (ICommand)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(FloatingActionControl), null);
    public object CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    private static void OnButtonSizeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is FloatingActionControl control)
        {
            control.UpdateShape();
        }
    }

    // Calculates corner radius proportionally to maintain the rounded square ratio
    private void UpdateShape()
    {
        double radiusRatio = 0.3; // 30% of the size
        MainContainer.StrokeShape = new RoundRectangle
        {
            CornerRadius = new CornerRadius(ButtonSize * radiusRatio)
        };
    }
}