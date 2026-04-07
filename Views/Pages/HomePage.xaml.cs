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
        await MealsControl.ScrollToStartAsync(); 
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
        if (_vm.IsSearching || SearchArea == null || MainScroll == null || MealsControl == null)
            return SwipeAction.MainPageSwipe;

        // Top search area + its vertical margins (15 + 10)
        double topOffset = SearchArea.Height + 25;

        // Calculate the absolute Y position of the horizontal control
        double relativeY = MealsControl.Y;
        double absoluteTop = topOffset + relativeY - MainScroll.ScrollY;
        double absoluteBottom = absoluteTop + MealsControl.Height;

        // Allow native horizontal scroll within bounds (+15px safe zone padding)
        if (startY >= absoluteTop - 15 && startY <= absoluteBottom + 15)
        {
            return SwipeAction.NativeChildScroll;
        }

        return SwipeAction.MainPageSwipe;
    }
    public void StartInnerSwipe() { }
    public void RunningInnerSwipe(double deltaX) { }
    public void CompletedInnerSwipe(double deltaX, double screenWidth) { }
    #endregion
}