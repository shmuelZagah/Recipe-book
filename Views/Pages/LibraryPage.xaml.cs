using Recipe_book.ViewModels;
using Recipe_book.Views.Layouts;

namespace Recipe_book.Views.Pages;

/// <summary>
/// The library page displaying folders and recipes. 
/// Implements ISwipeAwarePage to handle horizontal swipe gestures for internal tab navigation.
/// </summary>
public partial class LibraryPage : ContentView, ISwipeAwarePage
{
    #region Fields
    private readonly LibraryViewModel _vm;
    private bool _isAnimatingTab = false;
    #endregion

    #region Constructor & Initialization
    public LibraryPage(LibraryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;

        this.Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, EventArgs e)
    {
        await _vm.LoadFoldersCommand.ExecuteAsync(null);
        await CheckAndEnableLinksAsync();
    }

    private async Task CheckAndEnableLinksAsync()
    {
        bool hasAskedForLinks = Preferences.Default.Get("HasAskedForAppLinks", false);

        if (!hasAskedForLinks)
        {
            bool answer = await Shell.Current.DisplayAlert(
                "חיבור קישורים למתכונים",
                "כדי שהמתכונים ייפתחו ישירות באפליקציה ולא בדפדפן, יש לאשר פתיחת קישורים בהגדרות המכשיר.\n\nהאם לעבור להגדרות עכשיו?",
                "כן, העבר אותי",
                "לא כרגע");

            if (answer)
            {
                AppInfo.Current.ShowSettingsUI();
            }

            Preferences.Default.Set("HasAskedForAppLinks", true);
        }
    }
    #endregion

    #region Tab Navigation
    private async void OnTabSelected(object sender, string tabId)
    {
        if (BindingContext is not LibraryViewModel vm || _isAnimatingTab) return;
        if (vm.CurrentTab == tabId) return;

        _isAnimatingTab = true;

        int currentIndex = GetTabIndex(vm.CurrentTab);
        int newIndex = GetTabIndex(tabId);

        bool movingToLeftPage = newIndex > currentIndex;
        double screenWidth = this.Width;
        double moveOutOffset = movingToLeftPage ? screenWidth : -screenWidth;

        await ContentContainer.TranslateTo(moveOutOffset, 0, 250, Easing.CubicIn);

        if (vm.SelectTabCommand.CanExecute(tabId))
        {
            vm.SelectTabCommand.Execute(tabId);
        }

        await Task.Delay(50);

        ContentContainer.TranslationX = -moveOutOffset;
        await ContentContainer.TranslateTo(0, 0, 250, Easing.CubicOut);

        _isAnimatingTab = false;
    }

    private void ChangeTabByOffset(int offset)
    {
        if (BindingContext is not LibraryViewModel vm || _isAnimatingTab) return;

        int currentIndex = GetTabIndex(vm.CurrentTab);
        int newIndex = currentIndex + offset;

        if (newIndex >= 0 && newIndex < vm.LibraryTabs.Count)
        {
            MainTabBar?.SelectTab(newIndex);
        }
    }

    private int GetTabIndex(string tabId)
    {
        if (BindingContext is LibraryViewModel vm)
        {
            for (int i = 0; i < vm.LibraryTabs.Count; i++)
            {
                if (vm.LibraryTabs[i].Id == tabId) return i;
            }
        }
        return 0;
    }
    #endregion

    #region ISwipeAwarePage Implementation
    public SwipeAction GetSwipeAction(double totalX, double startX, double startY)
    {
        if (BindingContext is not LibraryViewModel vm)
            return SwipeAction.MainPageSwipe;

        // Check if the touch is within the top header bounds (Search + Tabs)
        if (TopHeader != null && startY <= TopHeader.Height)
        {
            // Check if the touch is specifically on the LiquidTabBar
            if (MainTabBar != null && startY >= MainTabBar.Y)
            {
                // Let the native LiquidTabBar scroll horizontally
                return SwipeAction.NativeChildScroll;
            }

            // Touch is on the SearchBar or empty space above the tabs -> Swipe main pages
            return SwipeAction.MainPageSwipe;
        }

        // Touch is in the content area -> Handle inner tab swiping
        int currentIndex = GetTabIndex(vm.CurrentTab);

        if (totalX > 0 && currentIndex < vm.LibraryTabs.Count - 1) return SwipeAction.ManualInnerSwipe;
        if (totalX < 0 && currentIndex > 0) return SwipeAction.ManualInnerSwipe;

        // Reached the edge of the tabs -> Swipe main pages
        return SwipeAction.MainPageSwipe;
    }

    public void StartInnerSwipe()
    {
        // Break animation lock for fast sequential swipes (Fling)
        _isAnimatingTab = false;
        ContentContainer.CancelAnimations();
    }

    public void RunningInnerSwipe(double deltaX)
    {
        ContentContainer.TranslationX = deltaX;
    }

    public void CompletedInnerSwipe(double deltaX, double screenWidth)
    {
        double swipeThreshold = screenWidth * 0.25;

        if (deltaX > swipeThreshold)
        {
            ChangeTabByOffset(1);
        }
        else if (deltaX < -swipeThreshold)
        {
            ChangeTabByOffset(-1);
        }
        else
        {
            // Revert gesture
            ContentContainer.TranslateTo(0, 0, 250, Easing.CubicOut);
        }
    }
    #endregion
}