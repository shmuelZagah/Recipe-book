using CommunityToolkit.Mvvm.Messaging;
using Recipe_book.Models.Cloud;
using Recipe_book.Models.Recipes;
using Recipe_book.Models.Shopping;
using Recipe_book.Services;
using Recipe_book.ViewModels;
using System.Text;
using System.Text.Json;
using System.Web;

namespace Recipe_book;

public partial class App : Application
{
    public static string PendingDeepLinkUrl { get; set; }
    public App()
    {
        InitializeComponent();

        MainPage = new AppShell();
    }

    protected override void OnStart()
    {
        base.OnStart();

        _ = InitializeUserAsync();

        // Check if a deep link is waiting in the room
        if (!string.IsNullOrEmpty(PendingDeepLinkUrl))
        {
            string urlToProcess = PendingDeepLinkUrl;
            PendingDeepLinkUrl = null; // Clear the room

            // UI is strictly ready now, safe to process!
            MainThread.BeginInvokeOnMainThread(() =>
            {
                this.SendOnAppLinkRequestReceived(new Uri(urlToProcess));
            });
        }
    }

    private async Task InitializeUserAsync()
    {
        try
        {
            var authService = IPlatformApplication.Current.Services.GetService<IFirebaseAuthService>();

            if (authService != null)
            {
                string uid = await authService.SignInAnonymouslyAsync();
                System.Diagnostics.Debug.WriteLine($"[AUTH SUCCESS]: User initialized with UID: {uid}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AUTH ERROR]: Failed to initialize user - {ex.Message}");
        }
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
                    var queryDictionary = HttpUtility.ParseQueryString(uri.Query);
                    string cloudId = queryDictionary["id"];

                    if (!string.IsNullOrEmpty(cloudId))
                    {
                        var firestoreService = new FirestoreService();
                        var importedRecipe = await firestoreService.GetRecipeFromCloudAsync(cloudId);

                        if (importedRecipe != null)
                        {
                            importedRecipe.CloudId = null;
                            importedRecipe.Id = 0;
                            importedRecipe.LastCookedDate = null;
                            importedRecipe.Title = importedRecipe.Title;
                            importedRecipe.Rating = 0;

                            await db.SaveRecipeAsync(importedRecipe);

                            if (importedRecipe.Ingredients != null)
                            {
                                foreach (var ingredient in importedRecipe.Ingredients)
                                {
                                    ingredient.Id = 0;
                                    ingredient.RecipeId = importedRecipe.Id;
                                    await db.SaveIngredientAsync(ingredient);
                                }
                            }

                            if (importedRecipe.Steps != null)
                            {
                                foreach (var step in importedRecipe.Steps)
                                {
                                    step.Id = 0;
                                    step.RecipeId = importedRecipe.Id;
                                    step.IsCompleted = false;
                                    await db.SaveStepAsync(step);
                                }
                            }

                            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send("RecipesChanged");

                            var navParam = new Dictionary<string, object>
                            {
                                { "Recipe", importedRecipe },
                                { "IsFromNewRecipe", false }
                            };

                            // --- FIX: Wrap UI Alert and Navigation in MainThread ---
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                await App.Current.MainPage.DisplayAlert("הצלחה!", $"המתכון '{importedRecipe.Title}' קפץ פנימה. בוא נבחר באיזו תיקייה לשמור אותו.", "המשך");
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
                            var navParam = new Dictionary<string, object>
                            {
                                { "IsImportMode", true },
                                { "ImportedFolderJson", importedFolder.RootFolderJson }
                            };

                            // --- FIX: Wrap UI Alert and Navigation in MainThread ---
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                await App.Current.MainPage.DisplayAlert("תיקייה התקבלה!", $"התיקייה '{importedFolder.BookName}' מוכנה. בחר איפה לשמור אותה.", "המשך");
                                await Shell.Current.GoToAsync(nameof(Views.SubPages.FolderSelectionPage), navParam);
                            });
                        }
                        else
                        {
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                await App.Current.MainPage.DisplayAlert("שגיאה", "לא הצלחנו למצוא את התיקייה בענן. ייתכן שהקישור שבור או שהתיקייה נמחקה.", "אישור");
                            });
                        }
                    }
                }

                // ==========================================
                // Handle Shared Shopping Lists (Cloud Download)
                // ==========================================
                else if (uri.AbsolutePath.ToLower() == "/sharelist")
                {
                    var queryDictionary = HttpUtility.ParseQueryString(uri.Query);
                    string cloudId = queryDictionary["id"];

                    if (!string.IsNullOrEmpty(cloudId))
                    {
                        var firestoreService = new FirestoreService();
                        var importedList = await firestoreService.GetSharedListFromCloudAsync(cloudId);

                        if (importedList != null && !string.IsNullOrEmpty(importedList.PayloadJson))
                        {
                            var sharedDto = JsonSerializer.Deserialize<SharedListDto>(importedList.PayloadJson);

                            if (sharedDto != null)
                            {
                                var existingConversions = await db.GetIngredientConversionsAsync();
                                var existingKeywords = existingConversions.Select(c => c.Keyword).ToHashSet();

                                foreach (var conv in sharedDto.C)
                                {
                                    if (!existingKeywords.Contains(conv.K))
                                    {
                                        await db.AddIngredientConversionAsync(new IngredientConversion
                                        {
                                            Keyword = conv.K,
                                            BaseUnit = conv.B,
                                            AmountPerCup = conv.A,
                                            Category = conv.C
                                        });
                                    }
                                }

                                var newList = new SavedShoppingList
                                {
                                    Title = sharedDto.T + " (מיובא)",
                                    IsStatic = true,
                                    CreatedAt = DateTime.Now
                                };
                                await db.SaveShoppingListAsync(newList);

                                var flatList = new List<SavedShoppingListItem>();
                                foreach (var item in sharedDto.I)
                                {
                                    string displayUnit = item.U == "יחידות" ? "" : item.U;
                                    string displayTxt = string.IsNullOrWhiteSpace(displayUnit) ? $"{item.Q} {item.N}" : $"{item.Q} {displayUnit} {item.N}";

                                    flatList.Add(new SavedShoppingListItem
                                    {
                                        ListId = newList.Id,
                                        Name = item.N,
                                        Quantity = item.Q,
                                        Unit = displayUnit,
                                        Category = item.C,
                                        DisplayText = displayTxt,
                                        IsBought = false
                                    });
                                }

                                await db.SyncShoppingListItemsAsync(newList.Id, flatList);

                                // --- FIX: Safe Navigation Without Route Parameters ---
                                MainThread.BeginInvokeOnMainThread(async () =>
                                {
                                    await App.Current.MainPage.DisplayAlert("הצלחה! 🎉", $"הרשימה '{sharedDto.T}' יובאה בהצלחה יחד עם המצרכים. תוכל למצוא אותה בתפריט הרשימות במסך הקניות.", "מעולה");

                                    ShoppingListViewModel.PendingImportId = newList.Id;

                                    Recipe_book.MainPage.SwitchTabAction?.Invoke(3);

                                    ShoppingListViewModel.RefreshActivePage?.Invoke();
                                });
                            }
                        }
                        else
                        {
                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                await App.Current.MainPage.DisplayAlert("שגיאה", "לא הצלחנו למצוא את הרשימה בענן. ייתכן שהקישור שבור, נמחק או פג תוקף.", "אישור");
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await App.Current.MainPage.DisplayAlert("שגיאה בייבוא", $"הקישור לא תקין או שגיאת רשת: {ex.Message}", "אישור");
                });
            }
        }
    }
}