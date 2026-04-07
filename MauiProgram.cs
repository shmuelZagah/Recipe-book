using Microsoft.Extensions.Logging;
using Recipe_book.Services;
using Recipe_book.Views.Pages;
using Recipe_book.Views.SubPages;

// --- 1. Correct Firebase usings for v3 ---
using Microsoft.Maui.LifecycleEvents;
using Plugin.Firebase.Bundled.Shared;
using Recipe_book.Views.Layouts;


#if ANDROID
using Plugin.Firebase.Bundled.Platforms.Android;
#endif
// -----------------------------------------

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
                    fonts.AddFont("Rubik-Regular.ttf", "RubikRegular");
                    fonts.AddFont("Rubik-Bold.ttf", "RubikBold");
                })

                .ConfigureMauiHandlers(handlers =>
                {
                    // Your existing entry handler (stays exactly as is)
                    Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
                    {
#if ANDROID
                        handler.PlatformView.BackgroundTintList =
                            Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#endif
                    });

                    // SwipeInterceptView handler — registered separately here, NOT inside the mapper above
                    handlers.AddHandler(typeof(SwipeInterceptView),
#if ANDROID
                        typeof(Recipe_book.Platforms.Android.SwipeInterceptViewHandler)
#elif IOS
            typeof(Recipe_book.Platforms.iOS.SwipeInterceptViewHandler)
#endif
                    );
                });

            //Content pages
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddSingleton<RecipesDatabase>();
            builder.Services.AddTransient<RecipeEditorPage>();
            builder.Services.AddTransient<RecipeViewerPage>();
            builder.Services.AddTransient<FolderSelectionPage>();
            builder.Services.AddTransient<AllRecipesPage>();
            builder.Services.AddTransient<SelectRecipePage>();
            builder.Services.AddTransient<ManageMealsPage>();

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

            //Content views
            builder.Services.AddTransient<HomePage>();
            builder.Services.AddTransient<SchedulePage>();
            builder.Services.AddTransient<LibraryPage>();
            builder.Services.AddTransient<ShoppingListPage>();


            return builder.Build();
        }

        // --- 3. Firebase initialization method ---
        private static MauiAppBuilder RegisterFirebaseServices(this MauiAppBuilder builder)
        {
            builder.ConfigureLifecycleEvents(events =>
            {
#if ANDROID
                events.AddAndroid(android => android.OnCreate((activity, state) =>
                    CrossFirebase.Initialize(activity, new CrossFirebaseSettings(
                        isFirestoreEnabled: true,
                        isAnalyticsEnabled: false,
                        isCrashlyticsEnabled: false)))); //need to be true in the futur 
#endif
            });

            return builder;
        }
        // -----------------------------------------
    }
}