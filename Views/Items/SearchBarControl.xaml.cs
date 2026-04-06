using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace Recipe_book.Views.Items;

public partial class SearchBarControl : ContentView
{
    public static readonly BindableProperty SearchTextProperty =
        BindableProperty.Create(nameof(SearchText), typeof(string), typeof(SearchBarControl), string.Empty, BindingMode.TwoWay);

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public static readonly BindableProperty PlaceholderTextProperty =
        BindableProperty.Create(nameof(PlaceholderText), typeof(string), typeof(SearchBarControl), "ητω...");

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    private bool _isOpen = false;
    private bool _isFocused = false;
    private const double ClosedWidth = 50;

    public event EventHandler? SearchOpened;
    public event EventHandler? SearchClosed;

    public SearchBarControl()
    {
        InitializeComponent();

        SearchInput.Focused += OnSearchInputFocused;
        SearchInput.Unfocused += OnSearchInputUnfocused;
    }

    public bool IsSearchOpen => _isOpen;

    public void CloseSearch()
    {
        if (_isOpen)
        {
            SearchText = string.Empty;
            SearchInput.Text = string.Empty;
            MainThread.BeginInvokeOnMainThread(async () => await CloseSearchAsync());
        }
    }

    private async void OnToggleSearchClicked(object sender, EventArgs e)
    {
        if (_isOpen)
        {
            SearchText = string.Empty;
            SearchInput.Text = string.Empty;
            await CloseSearchAsync();
            return;
        }

        _isOpen = true;
        ActionIcon.Source = "close_icon.svg";

        SearchOpened?.Invoke(this, EventArgs.Empty);

        SearchInput.IsVisible = true;
        SearchInput.IsEnabled = true; // Wake up the input

        SearchBorder.StrokeThickness = 1;
        SearchBorder.Stroke = Color.FromArgb("#E0E0E0");

        double targetWidth = this.Width;

        var animation = new Animation();
        animation.Add(0, 1, new Animation(v => SearchBorder.WidthRequest = v, ClosedWidth, targetWidth, Easing.CubicOut));
        animation.Add(0, 0.8, new Animation(v => SearchBorder.BackgroundColor = Colors.White.WithAlpha((float)v), 0.0, 1.0));

        animation.Commit(this, "ExpandSearch", length: 350);

        await SearchInput.FadeTo(1, 350);

        await Task.Delay(150);
        SearchInput.Focus();
    }

    private void OnSearchCompleted(object sender, EventArgs e)
    {
        SearchInput.Unfocus();
    }

    private void OnSearchInputFocused(object sender, FocusEventArgs e)
    {
        _isFocused = true;
    }

    private void OnSearchInputUnfocused(object sender, FocusEventArgs e)
    {
        _isFocused = false;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(200);
            if (_isOpen && !_isFocused && string.IsNullOrEmpty(SearchText))
            {
                await CloseSearchAsync();
            }
        });
    }

    private async Task CloseSearchAsync()
    {
        if (!_isOpen) return;

        _isOpen = false;
        ActionIcon.Source = "search_icon.svg";

        SearchClosed?.Invoke(this, EventArgs.Empty);

        // Force keyboard dismiss on Android/iOS
        SearchInput.Unfocus();
        SearchInput.IsEnabled = false;

        _ = SearchInput.FadeTo(0, 150);

        var animation = new Animation();
        animation.Add(0, 1, new Animation(v => SearchBorder.WidthRequest = v, SearchBorder.Width, ClosedWidth, Easing.CubicInOut));
        animation.Add(0, 1, new Animation(v => SearchBorder.BackgroundColor = Colors.White.WithAlpha((float)v), 1.0, 0.0));

        animation.Commit(this, "CollapseSearch", length: 250, finished: (v, c) =>
        {
            SearchInput.IsVisible = false;
            SearchBorder.StrokeThickness = 0;
            SearchBorder.BackgroundColor = Colors.Transparent;
        });
    }
}