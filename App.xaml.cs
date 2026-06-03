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

        // Wait for auth to complete BEFORE processing any deep link
        _ = InitializeAndHandleDeepLinkAsync();
    }

    private async Task InitializeAndHandleDeepLinkAsync()
    {
        // Step 1: Make sure the user is authenticated first
        await InitializeUserAsync();

        // Step 2: Only NOW it's safe to process the deep link
        if (!string.IsNullOrEmpty(PendingDeepLinkUrl))
        {
            string urlToProcess = PendingDeepLinkUrl;
            PendingDeepLinkUrl = null;

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
                // Handle Shared Folders 
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
                // Handle Shared Shopping Lists (online)
                // ==========================================
                else if (uri.AbsolutePath.ToLower() == "/sharelist")
                {
                    var queryDictionary = HttpUtility.ParseQueryString(uri.Query);
                    string cloudId = queryDictionary["id"];

                    if (!string.IsNullOrEmpty(cloudId))
                    {
                        var allLocalLists = await db.GetSavedShoppingListsAsync();
                        var existingList = allLocalLists.FirstOrDefault(l => l.CloudId == cloudId);

                        if (existingList != null)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                ShoppingListViewModel.PendingImportId = existingList.Id;
                                Recipe_book.MainPage.SwitchTabAction?.Invoke(3);
                                ShoppingListViewModel.RefreshActivePage?.Invoke();
                            });

                            return;
                        }

                        var firestoreService = new FirestoreService();
                        var importedList = await firestoreService.GetSharedListFromCloudAsync(cloudId);

                        if (importedList != null && !string.IsNullOrEmpty(importedList.ItemsJson))
                        {
                            var authService = IPlatformApplication.Current.Services.GetService<IFirebaseAuthService>();
                            string currentUid = authService?.GetCurrentUserId();

                            if (!string.IsNullOrEmpty(currentUid) && !importedList.PartnerUids.Contains(currentUid))
                            {
                                importedList.PartnerUids.Add(currentUid);
                                await firestoreService.UpdateSharedListAsync(importedList);
                            }

                            var existingConversions = await db.GetIngredientConversionsAsync();
                            var existingKeywords = existingConversions.Select(c => c.Keyword).ToHashSet();

                            if (!string.IsNullOrEmpty(importedList.ConversionsJson))
                            {
                                var convsDto = System.Text.Json.JsonSerializer.Deserialize<List<SharedCloudConversionDto>>(importedList.ConversionsJson);
                                if (convsDto != null)
                                {
                                    foreach (var conv in convsDto)
                                    {
                                        string k = conv.K;
                                        if (!string.IsNullOrEmpty(k) && !existingKeywords.Contains(k))
                                        {
                                            await db.AddIngredientConversionAsync(new IngredientConversion
                                            {
                                                Keyword = k,
                                                BaseUnit = string.IsNullOrWhiteSpace(conv.B) ? "יחידות" : conv.B,
                                                AmountPerCup = conv.A,
                                                Category = string.IsNullOrWhiteSpace(conv.C) ? "כללי" : conv.C
                                            });
                                        }
                                    }
                                }
                            }

                            var newList = new SavedShoppingList
                            {
                                Title = importedList.ListName,
                                CreatedAt = DateTime.Now,
                                CloudId = cloudId,
                                IsShared = true
                            };
                            await db.SaveShoppingListAsync(newList);

                            var flatList = new List<SavedShoppingListItem>();
                            var itemsDto = System.Text.Json.JsonSerializer.Deserialize<List<SharedCloudItemDto>>(importedList.ItemsJson);

                            if (itemsDto != null)
                            {
                                foreach (var cloudItem in itemsDto)
                                {
                                    string n = cloudItem.N ?? "";
                                    string u = string.IsNullOrWhiteSpace(cloudItem.U) ? "יחידות" : cloudItem.U;
                                    string c = string.IsNullOrWhiteSpace(cloudItem.C) ? "כללי" : cloudItem.C;
                                    double q = cloudItem.Q;
                                    bool isBought = cloudItem.IsBought;

                                    string displayUnit = (u == "יחידות") ? "" : u;
                                    string displayTxt = string.IsNullOrWhiteSpace(displayUnit) ? $"{q} {n}" : $"{q} {displayUnit} {n}";

                                    if (!string.IsNullOrWhiteSpace(n))
                                    {
                                        flatList.Add(new SavedShoppingListItem
                                        {
                                            ListId = newList.Id,
                                            Name = n,
                                            Quantity = q,
                                            Unit = displayUnit,
                                            Category = c,
                                            DisplayText = displayTxt,
                                            IsBought = isBought
                                        });
                                    }
                                }
                            }

                            await db.SaveStaticShoppingListItemsAsync(newList.Id, flatList);

                            MainThread.BeginInvokeOnMainThread(async () =>
                            {
                                await App.Current.MainPage.DisplayAlert("הצלחה! 🎉", $"הרשימה '{importedList.ListName}' יובאה בהצלחה.", "מעולה");
                                ShoppingListViewModel.PendingImportId = newList.Id;
                                Recipe_book.MainPage.SwitchTabAction?.Invoke(3);
                                ShoppingListViewModel.RefreshActivePage?.Invoke();
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