using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Recipe_book.Models.Organization;
using Recipe_book.Services;
using Recipe_book.Models.Recipes;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Maui.Controls;

namespace Recipe_book.ViewModels;

/// <summary>
/// ViewModel for managing the recipe library, including folder navigation, search, and recipe operations.
/// </summary>
public partial class LibraryViewModel : ObservableObject
{
    private readonly RecipesDatabase _database;

    //--------------
    #region Properties
    //--------------

    public ObservableCollection<RecipeFolder> Folders { get; } = new();
    public ObservableCollection<Recipe> FolderRecipes { get; } = new();

    [ObservableProperty]
    private string searchQuery;

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

    #endregion
    //--------------

    public LibraryViewModel(RecipesDatabase database)
    {
        _database = database;
    }

    //--------------
    #region Logic & Navigation
    //--------------

    partial void OnSearchQueryChanged(string value)
    {
        _ = LoadFoldersAsync();
    }

    /// <summary>
    /// Recursively finds a folder and all its descendant subfolders.
    /// </summary>
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
                Folders.Remove(fToDelete);
            }
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
                Folders.Remove(fToDelete);
            }
        }

        await LoadFoldersAsync();
    }

    #endregion
    //--------------

    //--------------
    #region Commands
    //--------------

    [RelayCommand]
    public async Task LoadFoldersAsync()
    {
        var allFolders = await _database.GetFoldersAsync();

        Folders.Clear();
        FolderRecipes.Clear();

        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            // Normal mode: navigate through the folder hierarchy
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

            foreach (var recipe in recipesToShow)
            {
                FolderRecipes.Add(recipe);
            }
        }
        else
        {
            // Search mode: flatten the view and search across all folders and recipes
            var filteredFolders = allFolders.Where(f => f.Name != null && f.Name.Contains(SearchQuery)).ToList();
            foreach (var folder in filteredFolders)
            {
                Folders.Add(folder);
            }

            var allRecipes = await _database.GetRecipesAsync();
            var filteredRecipes = allRecipes.Where(r => r.Title != null && r.Title.Contains(SearchQuery)).ToList();
            foreach (var recipe in filteredRecipes)
            {
                FolderRecipes.Add(recipe);
            }
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
            Folders.Add(newFolder);
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
            await LoadFoldersAsync();
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
            "מחק");

        if (action == "שנה שם")
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
            "מחק מתכון");

        if (action == "ערוך מתכון")
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
                FolderRecipes.Remove(recipe);
            }
        }
    }

    [RelayCommand]
    public async Task OpenAllRecipesAsync()
    {
        await Shell.Current.GoToAsync(nameof(Views.SubPages.AllRecipesPage));
    }

    #endregion
    //--------------
}