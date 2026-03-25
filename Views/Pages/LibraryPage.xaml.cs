using Recipe_book.ViewModels;

namespace Recipe_book.Views.Pages;

public partial class LibraryPage : ContentPage
{
    private readonly LibraryViewModel _vm;

    public LibraryPage(LibraryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

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
}