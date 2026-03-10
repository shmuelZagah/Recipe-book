namespace Recipe_book.Views.Items;
public partial class SearchBarControl : ContentView
{
    public static readonly BindableProperty SearchTextProperty =
        BindableProperty.Create(nameof(SearchText), typeof(string), typeof(SearchBarControl), defaultBindingMode: BindingMode.TwoWay);

    public static readonly BindableProperty PlaceholderTextProperty =
        BindableProperty.Create(nameof(PlaceholderText), typeof(string), typeof(SearchBarControl), "חפש מתכון...");

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    public SearchBarControl()
    {
        InitializeComponent();
        MainSearchBar.SetBinding(SearchBar.TextProperty, new Binding(nameof(SearchText), source: this));
        MainSearchBar.SetBinding(SearchBar.PlaceholderProperty, new Binding(nameof(PlaceholderText), source: this));
    }
}