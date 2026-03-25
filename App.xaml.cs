using System.Text.Json;
using System.Text;
using System.Web; 
using Recipe_book.Models.Shopping;
using Recipe_book.Services;
using Recipe_book.ViewModels;

namespace Recipe_book;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        MainPage = new AppShell();
    }

    protected override async void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);

        // Check if the link belongs to our standard web domain
        if (uri.Scheme.ToLower() == "https" && uri.Host.ToLower() == "recipe-book-d9389.web.app")
        {
            try
            {
                var db = IPlatformApplication.Current.Services.GetService<RecipesDatabase>();

                // ==========================================
                // Handle Shared Recipes (Cloud Download)
                // ==========================================
                if (uri.AbsolutePath.ToLower() == "/recipe")
                {
                    // Extract the CloudId from the URL (e.g., ?id=XYZ123)
                    var queryDictionary = HttpUtility.ParseQueryString(uri.Query);
                    string cloudId = queryDictionary["id"];

                    if (!string.IsNullOrEmpty(cloudId))
                    {
                        var firestoreService = new FirestoreService();
                        var importedRecipe = await firestoreService.GetRecipeFromCloudAsync(cloudId);

                        if (importedRecipe != null)
                        {
                            // --- THE CLONE MAGIC ---
                            // 1. Disconnect it from the original cloud document
                            importedRecipe.CloudId = null;
                            importedRecipe.Id = 0; // Force SQLite to treat it as a new record

                            // 2. Clean up personal data
                            importedRecipe.IsFavorite = false;
                            importedRecipe.LastCookedDate = null;
                            importedRecipe.Title = importedRecipe.Title;

                            // 3. Save the base recipe locally
                            await db.SaveRecipeAsync(importedRecipe);

                            // 4. Save Ingredients locally
                            if (importedRecipe.Ingredients != null)
                            {
                                foreach (var ingredient in importedRecipe.Ingredients)
                                {
                                    ingredient.Id = 0; // Reset local ID
                                    ingredient.RecipeId = importedRecipe.Id;
                                    await db.SaveIngredientAsync(ingredient);
                                }
                            }

                            // 5. Save Steps locally
                            if (importedRecipe.Steps != null)
                            {
                                foreach (var step in importedRecipe.Steps)
                                {
                                    step.Id = 0; // Reset local ID
                                    step.RecipeId = importedRecipe.Id;
                                    step.IsCompleted = false; // Ensure it's not marked as done
                                    await db.SaveStepAsync(step);
                                }
                            }

                            await App.Current.MainPage.DisplayAlert("הצלחה!", $"המתכון '{importedRecipe.Title}' קפץ פנימה. בוא נבחר באיזו תיקייה לשמור אותו.", "המשך");

                            var navParam = new Dictionary<string, object>
                            {
                                { "Recipe", importedRecipe },
                                { "IsFromNewRecipe", false }
                            };

                            // Ensure navigation runs on the main UI thread
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                await Shell.Current.GoToAsync(nameof(Views.SubPages.FolderSelectionPage), navParam);
                            });
                        }
                    }
                }

                // ==========================================
                // Handle Shared Folders (Cloud Download)
                // ==========================================
                else if (uri.AbsolutePath.ToLower() == "/folder")
                {
                    var queryDictionary = HttpUtility.ParseQueryString(uri.Query);
                    string cloudId = queryDictionary["id"];

                    if (!string.IsNullOrEmpty(cloudId))
                    {
                        var firestoreService = new FirestoreService();
                        var importedFolder = await firestoreService.GetSharedFolderFromCloudAsync(cloudId);

                        if (importedFolder != null && !string.IsNullOrEmpty(importedFolder.RootFolderJson))
                        {
                            await App.Current.MainPage.DisplayAlert("תיקייה התקבלה!", $"התיקייה '{importedFolder.BookName}' מוכנה. בחר איפה לשמור אותה.", "המשך");

                            // Create the exact parameters our smart FolderSelectionViewModel is waiting for!
                            var navParam = new Dictionary<string, object>
                            {
                                { "IsImportMode", true },
                                { "ImportedFolderJson", importedFolder.RootFolderJson }
                            };

                            // Ensure navigation runs on the main UI thread
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                await Shell.Current.GoToAsync(nameof(Views.SubPages.FolderSelectionPage), navParam);
                            });
                        }
                        else
                        {
                            await App.Current.MainPage.DisplayAlert("שגיאה", "לא הצלחנו למצוא את התיקייה בענן. ייתכן שהקישור שבור או שהתיקייה נמחקה.", "אישור");
                        }
                    }
                }
            }

            catch (Exception ex)
            {
                await App.Current.MainPage.DisplayAlert("שגיאה בייבוא", $"הקישור לא תקין או שגיאת רשת: {ex.Message}", "אישור");
            }
        }
    }
}

