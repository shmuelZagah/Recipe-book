using Microsoft.Extensions.Logging;
using Recipe_book.Services;
using Recipe_book.Views.Pages;
using Recipe_book.Views.SubPages;


namespace Recipe_book
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
            {
#if ANDROID
                handler.PlatformView.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#endif
            });


            //pages
            builder.Services.AddSingleton<RecipesDatabase>(); 
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddTransient<RecipeEditorPage>();
            builder.Services.AddTransient<RecipeViewerPage>();
            builder.Services.AddTransient<LibraryPage>();
            builder.Services.AddTransient<FolderSelectionPage>();
            builder.Services.AddTransient<AllRecipesPage>();
            builder.Services.AddTransient<SchedulePage>();
            builder.Services.AddTransient<SelectRecipePage>();
            builder.Services.AddTransient<ManageMealsPage>();
            builder.Services.AddTransient<ShoppingListPage>();

            //ViewModels
            builder.Services.AddTransient<Recipe_book.ViewModels.WeeklyScheduleViewModel>();
            builder.Services.AddTransient<Recipe_book.ViewModels.AllRecipesViewModel>();
            builder.Services.AddTransient<Recipe_book.ViewModels.LibraryViewModel>();
            builder.Services.AddTransient<Recipe_book.ViewModels.FolderSelectionViewModel>();
            builder.Services.AddTransient<Recipe_book.ViewModels.RecipeViewerViewModel>();
            builder.Services.AddTransient<Recipe_book.ViewModels.RecipeEditorViewModel>();
            builder.Services.AddTransient<Recipe_book.ViewModels.SelectRecipeViewModel>();
            builder.Services.AddTransient<Recipe_book.ViewModels.ManageMealsViewModel>();
            builder.Services.AddTransient<Recipe_book.ViewModels.ShoppingListViewModel>();

            builder.Services.AddSingleton<ViewModels.MainViewModel>();

         


            return builder.Build();
        }
    }
}
