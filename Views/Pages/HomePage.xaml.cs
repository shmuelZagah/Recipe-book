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
        if (_vm.IsSearching || SearchArea == null || MainScroll == null)
            return SwipeAction.MainPageSwipe;

        // Calculate static top area (Search bar + margins)
        double topOffset = SearchArea.Height + 25;

        // 1. Check if swipe is within the Daily Meals carousel bounds
        if (MealsControl != null)
        {
            double mealsRelativeY = GetRelativeY(MealsControl, MainScroll);
            double mealsAbsoluteTop = topOffset + mealsRelativeY - MainScroll.ScrollY;
            double mealsAbsoluteBottom = mealsAbsoluteTop + MealsControl.Height;

            if (startY >= mealsAbsoluteTop - 15 && startY <= mealsAbsoluteBottom + 15)
            {
                return SwipeAction.NativeChildScroll;
            }
        }

        // 2. Check if swipe is within the Suggestions carousel bounds
        if (SuggestionsList != null)
        {
            double suggestionsRelativeY = GetRelativeY(SuggestionsList, MainScroll);
            double suggestionsAbsoluteTop = topOffset + suggestionsRelativeY - MainScroll.ScrollY;
            double suggestionsAbsoluteBottom = suggestionsAbsoluteTop + SuggestionsList.Height;

            if (startY >= suggestionsAbsoluteTop - 15 && startY <= suggestionsAbsoluteBottom + 15)
            {
                return SwipeAction.NativeChildScroll;
            }
        }

        // Default: allow main tab swiping
        return SwipeAction.MainPageSwipe;
    }

    // Helper: Gets absolute Y position relative to the main ScrollView
    private double GetRelativeY(VisualElement element, VisualElement parent)
    {
        double y = 0;
        while (element != null && element != parent)
        {
            y += element.Y;
            element = element.Parent as VisualElement;
        }
        return y;
    }

    public void StartInnerSwipe() { }
    public void RunningInnerSwipe(double deltaX) { }
    public void CompletedInnerSwipe(double deltaX, double screenWidth) { }

    #endregion
}