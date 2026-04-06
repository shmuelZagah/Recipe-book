using Recipe_book.ViewModels;
using Recipe_book.Views.Layouts;

namespace Recipe_book.Views.Pages;

/// <summary>
/// The home page of the application, displaying the daily schedule and recipe suggestions.
/// Implements ISwipeAwarePage to allow horizontal scrolling on the daily meals list.
/// </summary>
public partial class HomePage : ContentView, ISwipeAwarePage
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

    #region ISwipeAwarePage Implementation
    public SwipeAction GetSwipeAction(double totalX, double startX, double startY)
    {
        // If searching or UI elements are not fully loaded, allow main page swipe
        if (_vm.IsSearching || SearchArea == null || MainScroll == null || MealsScrollView == null)
            return SwipeAction.MainPageSwipe;

        // Calculate the absolute position of the meals section on the screen:
        // Top search area height + its vertical margins (Top 15 + Bottom 10 = 25)
        double topOffset = SearchArea.Height + 25;

        // The Y position of the horizontal list relative to the vertical ScrollView
        double relativeY = (MealsContainer?.Y ?? 0) + MealsScrollView.Y;

        // Exact physical Y position on the screen (accounting for current vertical scroll)
        double absoluteTop = topOffset + relativeY - MainScroll.ScrollY;
        double absoluteBottom = absoluteTop + MealsScrollView.Height;

        // Added a 15px safe zone padding to make the horizontal scroll easier to catch
        if (startY >= absoluteTop - 15 && startY <= absoluteBottom + 15)
        {
            // Touch is on the daily meals horizontal list -> let native scroll handle it
            return SwipeAction.NativeChildScroll;
        }

        // Touch is outside the meals list -> trigger main page navigation
        return SwipeAction.MainPageSwipe;
    }

    public void StartInnerSwipe() { }
    public void RunningInnerSwipe(double deltaX) { }
    public void CompletedInnerSwipe(double deltaX, double screenWidth) { }
    #endregion
}