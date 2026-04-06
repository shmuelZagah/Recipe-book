using Recipe_book.ViewModels;

namespace Recipe_book.Views.Pages;

/// <summary>
/// The home page of the application, displaying the daily schedule and recipe suggestions.
/// </summary>
public partial class HomePage : ContentView
{
    #region Fields
    private readonly MainViewModel _vm;
    #endregion

    #region Constructor & Initialization
    public HomePage(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;

        this.Loaded += OnHomePageLoaded;
    }

    private async void OnHomePageLoaded(object sender, EventArgs e)
    {
        await _vm.LoadRecipesCommand.ExecuteAsync(null);

        await Task.Delay(150);

        // Scroll to the end of the horizontal meals list initially (RTL adjustment)
        double fullWidth = MealsScrollView.ContentSize.Width;
        await MealsScrollView.ScrollToAsync(fullWidth, 0, animated: false);
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Handles hardware back button presses specifically for the home page.
    /// </summary>
    /// <returns>True if the back press was handled (e.g., closing search), false otherwise.</returns>
    public bool HandleBackPressed()
    {
        if (MainSearchBar.IsSearchOpen)
        {
            MainSearchBar.CloseSearch();
            return true;
        }
        return false;
    }
    #endregion
}