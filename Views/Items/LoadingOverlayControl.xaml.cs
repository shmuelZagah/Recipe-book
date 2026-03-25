namespace Recipe_book.Views.Items;

public partial class LoadingOverlayControl : ContentView
{
    // Bindable property to toggle visibility
    public static readonly BindableProperty IsLoadingProperty =
        BindableProperty.Create(nameof(IsLoading), typeof(bool), typeof(LoadingOverlayControl), false);

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    // Bindable property to dynamically update the text
    public static readonly BindableProperty LoadingTextProperty =
        BindableProperty.Create(nameof(LoadingText), typeof(string), typeof(LoadingOverlayControl), "иетп...");

    public string LoadingText
    {
        get => (string)GetValue(LoadingTextProperty);
        set => SetValue(LoadingTextProperty, value);
    }

    public LoadingOverlayControl()
    {
        InitializeComponent();
    }
}