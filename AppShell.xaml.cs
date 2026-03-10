using Recipe_book.Views.Pages;
using Recipe_book.Views.SubPages;

namespace Recipe_book
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(RecipeEditorPage), typeof(RecipeEditorPage));
            Routing.RegisterRoute(nameof(RecipeViewerPage), typeof(RecipeViewerPage));
            Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
            Routing.RegisterRoute(nameof(FolderSelectionPage), typeof(FolderSelectionPage));
            Routing.RegisterRoute(nameof(AllRecipesPage), typeof(AllRecipesPage));
            Routing.RegisterRoute(nameof(SchedulePage), typeof(SchedulePage));
            Routing.RegisterRoute(nameof(SelectRecipePage), typeof(SelectRecipePage));
            Routing.RegisterRoute(nameof(ManageMealsPage), typeof(ManageMealsPage));
            Routing.RegisterRoute(nameof(ShoppingListPage), typeof(ShoppingListPage));
        }

        private async void OnSettingsTapped(object sender, EventArgs e)
        {
            Current.FlyoutIsPresented = false;
            await Current.GoToAsync(nameof(SettingsPage));
        }
    }
}
