using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Recipe_book.Models.Organization;
using Recipe_book.Models.Recipes;
using Recipe_book.Services;
using System.Collections.ObjectModel;
using Recipe_book.Views.Items.bars;

namespace Recipe_book.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly RecipesDatabase _database;

    //--------------
    #region Properties
    //--------------

    public ObservableCollection<TabItem> LibraryTabs { get; } = new();
    public ObservableCollection<RecipeFolder> Folders { get; } = new();
    public ObservableCollection<Recipe> FolderRecipes { get; } = new();

    [ObservableProperty]
    private string searchQuery;

    [ObservableProperty]
    private string currentTab = "Folders"; // "Folders", "AllRecipes", "Favorites", "Uncategorized"

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    [NotifyPropertyChangedFor(nameof(IsInnerFolder))]
    [NotifyPropertyChangedFor(nameof(IsRootFolder))]
    private RecipeFolder currentFolder;

    public bool IsRootFolder => CurrentFolder == null;
    public bool IsInnerFolder => CurrentFolder != null;

    public string PageTitle => CurrentFolder == null ? "המתכונים שלי" : CurrentFolder.Name;

    [ObservableProperty]
    private int gridSpan = 2;

    [ObservableProperty]
    private bool isLoading = false;

    private string defaultLoadingText = "טוען, נא להמתין";

    [ObservableProperty]
    private string loadingText = string.Empty;

    #endregion
    //--------------

    public LibraryViewModel(RecipesDatabase database)
    {
        _database = database;
        InitializeVariables();

        LibraryTabs.Add(new TabItem { Id = "Folders", Title = "תיקיות" });
        LibraryTabs.Add(new TabItem { Id = "AllRecipes", Title = "כל המתכונים" });
        LibraryTabs.Add(new TabItem { Id = "Favorites", Title = "מועדפים" });
        LibraryTabs.Add(new TabItem { Id = "Uncategorized", Title = "ללא תיקייה" });


        WeakReferenceMessenger.Default.Register<string>(this, async (r, m) =>
        {
            if (m == "RefreshRecipes" || m == "FoldersChanged" || m == "RecipesChanged")
                await LoadFoldersCommand.ExecuteAsync(null);
        });
    }

    private void InitializeVariables()
    {
        LoadingText = defaultLoadingText;
    }

    //--------------
    #region Logic & Navigation
    //--------------

    partial void OnSearchQueryChanged(string value)
    {
        _ = LoadFoldersAsync();
    }

    [RelayCommand]
    public async Task SelectTabAsync(string tabName)
    {
        if (CurrentTab == tabName) return;

        CurrentTab = tabName;
        CurrentFolder = null; // איפוס ניווט פנימי כשמחליפים טאב
        SearchQuery = string.Empty; // איפוס חיפוש

        await LoadFoldersAsync();
    }

    private List<RecipeFolder> GetFolderAndDescendants(RecipeFolder targetFolder, List<RecipeFolder> allFolders)
    {
        var result = new List<RecipeFolder> { targetFolder };
        var children = allFolders.Where(f => f.ParentFolderId == targetFolder.Id).ToList();

        foreach (var child in children)
        {
            result.AddRange(GetFolderAndDescendants(child, allFolders));
        }

        return result;
    }

    private async Task ExecuteDeleteFolderFlowAsync(RecipeFolder folder)
    {
        string deleteOption = await Application.Current.MainPage.DisplayActionSheet(
            $"מחיקת '{folder.Name}'",
            "ביטול",
            null,
            "מחק תיקייה בלבד (המתכונים יישמרו)",
            "מחק תיקייה וגם את המתכונים שבתוכה");

        if (deleteOption == "ביטול" || string.IsNullOrEmpty(deleteOption)) return;

        var allFolders = await _database.GetFoldersAsync();
        var foldersToDelete = GetFolderAndDescendants(folder, allFolders);

        if (deleteOption == "מחק תיקייה בלבד (המתכונים יישמרו)")
        {
            foreach (var fToDelete in foldersToDelete)
            {
                await _database.RemoveAllRecipesFromFolderAsync(fToDelete.Id);
                await _database.DeleteFolderAsync(fToDelete);
            }
            WeakReferenceMessenger.Default.Send("FoldersChanged");
        }
        else if (deleteOption == "מחק תיקייה וגם את המתכונים שבתוכה")
        {
            foreach (var fToDelete in foldersToDelete)
            {
                var recipesToDelete = await _database.GetRecipesInFolderAsync(fToDelete.Id);
                foreach (var recipe in recipesToDelete)
                {
                    await _database.DeleteRecipeAsync(recipe);
                }

                await _database.RemoveAllRecipesFromFolderAsync(fToDelete.Id);
                await _database.DeleteFolderAsync(fToDelete);
            }
            WeakReferenceMessenger.Default.Send("FoldersChanged");
            WeakReferenceMessenger.Default.Send("RecipesChanged");
        }
    }

    #endregion
    //--------------

    //--------------
    #region Commands
    //--------------

    [RelayCommand]
    public async Task LoadFoldersAsync()
    {
        IsLoading = true;
        try
        {
            Folders.Clear();
            FolderRecipes.Clear();

            var allRecipes = await _database.GetRecipesAsync();

            if (CurrentTab == "Folders")
            {
                var allFolders = await _database.GetFoldersAsync();

                if (string.IsNullOrWhiteSpace(SearchQuery))
                {
                    int? targetParentId = CurrentFolder?.Id;

                    foreach (var folder in allFolders.Where(f => f.ParentFolderId == targetParentId))
                    {
                        Folders.Add(folder);
                    }

                    List<Recipe> recipesToShow;
                    if (targetParentId == null || targetParentId == 0)
                        recipesToShow = await _database.GetRecipesWithoutFolderAsync();
                    else
                        recipesToShow = await _database.GetRecipesInFolderAsync(targetParentId.Value);

                    foreach (var recipe in recipesToShow) FolderRecipes.Add(recipe);
                }
                else
                {
                    var filteredFolders = allFolders.Where(f => f.Name != null && f.Name.Contains(SearchQuery)).ToList();
                    foreach (var folder in filteredFolders) Folders.Add(folder);

                    var filteredRecipes = allRecipes.Where(r => r.Title != null && r.Title.Contains(SearchQuery)).ToList();
                    foreach (var recipe in filteredRecipes) FolderRecipes.Add(recipe);
                }
            }
            else if (CurrentTab == "AllRecipes")
            {
                var recipesToShow = string.IsNullOrWhiteSpace(SearchQuery)
                    ? allRecipes
                    : allRecipes.Where(r => r.Title != null && r.Title.Contains(SearchQuery)).ToList();

                foreach (var recipe in recipesToShow) FolderRecipes.Add(recipe);
            }
            else if (CurrentTab == "Favorites")
            {
                var favoriteRecipes = await _database.GetFavoriteRecipesAsync();

                var recipesToShow = string.IsNullOrWhiteSpace(SearchQuery)
                    ? favoriteRecipes
                    : favoriteRecipes.Where(r => r.Title != null && r.Title.Contains(SearchQuery)).ToList();

                foreach (var recipe in recipesToShow) FolderRecipes.Add(recipe);
            }
            else if (CurrentTab == "Uncategorized")
            {
                var uncategorized = await _database.GetRecipesWithoutFolderAsync();

                var recipesToShow = string.IsNullOrWhiteSpace(SearchQuery)
                    ? uncategorized
                    : uncategorized.Where(r => r.Title != null && r.Title.Contains(SearchQuery)).ToList();

                foreach (var recipe in recipesToShow) FolderRecipes.Add(recipe);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task AddFolderAsync()
    {
        string folderName = await Application.Current.MainPage.DisplayPromptAsync(
            "תיקייה חדשה",
            "הכנס שם לתיקייה החדשה:",
            "צור",
            "ביטול");

        if (!string.IsNullOrWhiteSpace(folderName))
        {
            var newFolder = new RecipeFolder
            {
                Name = folderName,
                ParentFolderId = CurrentFolder?.Id
            };

            await _database.SaveFolderAsync(newFolder);
            WeakReferenceMessenger.Default.Send("FoldersChanged");
        }
    }

    [RelayCommand]
    public void OpenFolder(RecipeFolder folder)
    {
        if (folder == null) return;
        CurrentFolder = folder;
        _ = LoadFoldersAsync();
    }

    [RelayCommand]
    public async Task GoUpAsync()
    {
        if (CurrentFolder == null) return;

        if (CurrentFolder.ParentFolderId == null || CurrentFolder.ParentFolderId == 0)
        {
            CurrentFolder = null;
        }
        else
        {
            var allFolders = await _database.GetFoldersAsync();
            CurrentFolder = allFolders.FirstOrDefault(f => f.Id == CurrentFolder.ParentFolderId);
        }

        await LoadFoldersAsync();
    }

    [RelayCommand]
    public void ToggleView()
    {
        if (GridSpan == 1) GridSpan = 2;
        else if (GridSpan == 2) GridSpan = 3;
        else GridSpan = 1;
    }

    [RelayCommand]
    public async Task RenameFolderAsync(RecipeFolder folder)
    {
        if (folder == null) return;

        string newName = await Application.Current.MainPage.DisplayPromptAsync(
            "שינוי שם תיקייה",
            "הכנס שם חדש:",
            "שמור",
            "ביטול",
            initialValue: folder.Name);

        if (!string.IsNullOrWhiteSpace(newName) && newName != folder.Name)
        {
            folder.Name = newName;
            await _database.SaveFolderAsync(folder);
            WeakReferenceMessenger.Default.Send("FoldersChanged");
        }
    }

    [RelayCommand]
    public async Task FolderOptionsAsync(RecipeFolder folder)
    {
        if (folder == null) return;

        string action = await Application.Current.MainPage.DisplayActionSheet(
            "אפשרויות תיקייה",
            "ביטול",
            null,
            "שנה שם",
            "מחק",
            "שתף תיקייה");

        if (action == "שתף תיקייה")
        {
            await ShareFolderAsync(folder);
        }
        else if (action == "שנה שם")
        {
            await RenameFolderAsync(folder);
        }
        else if (action == "מחק")
        {
            await ExecuteDeleteFolderFlowAsync(folder);
        }
    }

    [RelayCommand]
    public async Task OpenRecipeAsync(Recipe recipe)
    {
        if (recipe == null) return;

        var navParam = new Dictionary<string, object> { { "Recipe", recipe } };
        await Shell.Current.GoToAsync(nameof(Views.SubPages.RecipeViewerPage), navParam);
    }

    [RelayCommand]
    public async Task RecipeOptionsAsync(Recipe recipe)
    {
        if (recipe == null) return;

        string action = await Application.Current.MainPage.DisplayActionSheet(
            $"אפשרויות מתכון",
            "ביטול",
            null,
            "ערוך מתכון",
            "ניהול תיקיות",
            "מחק מתכון",
            "שתף מתכון");

        if (action == "שתף מתכון")
        {
            await ShareRecipeAsync(recipe);
        }
        else if (action == "ערוך מתכון")
        {
            var navParam = new Dictionary<string, object> { { "RecipeToEdit", recipe } };
            await Shell.Current.GoToAsync(nameof(Views.SubPages.RecipeEditorPage), navParam);
        }
        else if (action == "ניהול תיקיות")
        {
            var navParam = new Dictionary<string, object> { { "Recipe", recipe } };
            await Shell.Current.GoToAsync(nameof(Views.SubPages.FolderSelectionPage), navParam);
        }
        else if (action == "מחק מתכון")
        {
            bool answer = await Application.Current.MainPage.DisplayAlert(
                "מחיקת מתכון",
                $"האם אתה בטוח שברצונך למחוק את '{recipe.Title}'?",
                "כן, מחק",
                "ביטול");

            if (answer)
            {
                await _database.DeleteRecipeAsync(recipe);
                WeakReferenceMessenger.Default.Send("RecipesChanged");
            }
        }
    }

    [RelayCommand]
    public async Task AddRecipeAsync()
    {
        await Shell.Current.GoToAsync(nameof(Views.SubPages.RecipeEditorPage));
    }

    #endregion
    //--------------

    //--------------
    #region Sharing & Cloud Logic
    //--------------

    private async Task ShareRecipeAsync(Recipe recipe)
    {
        if (recipe == null) return;

        IsLoading = true;
        LoadingText = "אורז את המתכון, נא להמתין";

        try
        {
            var ingredients = await _database.GetIngredientsAsync(recipe.Id);
            var steps = await _database.GetStepsAsync(recipe.Id);
            recipe.Ingredients = new ObservableCollection<Ingredient>(ingredients);
            recipe.Steps = new ObservableCollection<RecipeStep>(steps);

            var firestoreService = new FirestoreService();
            await firestoreService.SaveRecipeToCloudAsync(recipe);

            await _database.SaveRecipeAsync(recipe);

            if (string.IsNullOrEmpty(recipe.CloudId))
            {
                await Application.Current.MainPage.DisplayAlert("שגיאה", "לא הצלחנו לייצר קישור לענן. אנא בדוק את החיבור לאינטרנט.", "אישור");
                return;
            }

            string shareLink = $"https://recipe-book-d9389.web.app/recipe?id={recipe.CloudId}";

            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = "שתף מתכון",
                Text = $"היי! שמרתי פה מתכון מעולה ל-{recipe.Title}. לחץ על הקישור כדי לשמור אותו אצלך:\n{shareLink}"
            });
        }
        finally
        {
            IsLoading = false;
            LoadingText = defaultLoadingText;
        }
    }

    private async Task ShareFolderAsync(RecipeFolder folder)
    {
        if (folder == null) return;

        IsLoading = true;
        LoadingText = "אורז את התיקייה לענן, זה עשוי לקחת כמה שניות...";

        try
        {
            var sharedFolderTree = await _database.BuildSharedFolderTreeAsync(folder);
            var firestoreService = new FirestoreService();
            string newCloudId = await firestoreService.UploadSharedFolderAsync(sharedFolderTree);

            if (string.IsNullOrEmpty(newCloudId))
            {
                await Application.Current.MainPage.DisplayAlert("שגיאה", "לא הצלחנו לייצר קישור לתיקייה. אנא בדוק את החיבור לאינטרנט.", "אישור");
                return;
            }

            await _database.RegisterSharedFolderForDeletionAsync(newCloudId, sharedFolderTree.ExpiresAt);

            string shareLink = $"https://recipe-book-d9389.web.app/folder?id={newCloudId}";

            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = "שתף תיקיית מתכונים",
                Text = $"היי! הכנתי לך אוסף מתכונים מעולה: '{folder.Name}'. לחץ על הקישור כדי לשמור אותו אצלך:\n{shareLink}"
            });
        }
        finally
        {
            IsLoading = false;
            LoadingText = defaultLoadingText;
        }
    }

    #endregion
    //--------------
}