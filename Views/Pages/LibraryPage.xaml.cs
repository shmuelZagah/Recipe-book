using Recipe_book.ViewModels;

namespace Recipe_book.Views.Pages;

public partial class LibraryPage : ContentView
{
    private readonly LibraryViewModel _vm;

    public LibraryPage(LibraryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;

        // טעינת הנתונים דרך האירוע Loaded במקום OnAppearing
        this.Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, EventArgs e)
    {
        // Load the folders first
        await _vm.LoadFoldersCommand.ExecuteAsync(null);

        // Run the app links check quietly in the background
        await CheckAndEnableLinksAsync();
    }

    private async Task CheckAndEnableLinksAsync()
    {
        // Check if the user was already prompted before
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
                // Navigate the user to the app's OS settings page
                AppInfo.Current.ShowSettingsUI();
            }

            // Mark as true so the prompt won't appear again on next launch
            Preferences.Default.Set("HasAskedForAppLinks", true);
        }
    }

    private bool _isAnimatingTab = false;

    private async void OnTabSelected(object sender, string tabId)
    {
        if (BindingContext is not LibraryViewModel vm || _isAnimatingTab) return;
        if (vm.CurrentTab == tabId) return;

        _isAnimatingTab = true;

        int currentIndex = GetTabIndex(vm.CurrentTab);
        int newIndex = GetTabIndex(tabId);

        // RTL Logic: Index 0 is Right, Index 1 is Left.
        // Moving to a higher index means we want to show the Left page,
        // so the current page needs to slide out to the Right (+Width).
        bool movingToLeftPage = newIndex > currentIndex;
        double screenWidth = this.Width;
        double moveOutOffset = movingToLeftPage ? screenWidth : -screenWidth;

        // 1. Slide current content out
        await ContentContainer.TranslateTo(moveOutOffset, 0, 250, Easing.CubicIn);

        // 2. Change ViewModel data
        if (vm.SelectTabCommand.CanExecute(tabId))
        {
            vm.SelectTabCommand.Execute(tabId);
        }

        // Wait a split second to ensure bindings update the UI before it slides back
        await Task.Delay(50);

        // 3. Teleport content to the opposite side while invisible
        ContentContainer.TranslationX = -moveOutOffset;

        // 4. Slide new content in
        await ContentContainer.TranslateTo(0, 0, 250, Easing.CubicOut);

        _isAnimatingTab = false;
    }

    private void OnSwipedLeft(object sender, SwipedEventArgs e)
    {
        // Swipe left -> Move deeper into the tabs (Higher index)
        ChangeTabByOffset(1);
    }

    private void OnSwipedRight(object sender, SwipedEventArgs e)
    {
        // Swipe right -> Move back (Lower index)
        ChangeTabByOffset(-1);
    }

    private void ChangeTabByOffset(int offset)
    {
        if (BindingContext is not LibraryViewModel vm || _isAnimatingTab) return;

        int currentIndex = GetTabIndex(vm.CurrentTab);
        int newIndex = currentIndex + offset;

        if (newIndex >= 0 && newIndex < vm.LibraryTabs.Count)
        {
            // Trigger the visual bar. It will automatically call OnTabSelected when done.
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
}
