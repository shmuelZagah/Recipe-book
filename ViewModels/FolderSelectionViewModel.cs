using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Recipe_book.Models.Organization;
using Recipe_book.Models.Recipes;
using Recipe_book.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Recipe_book.ViewModels;

//--------------
#region Helper Classes
//--------------

public partial class SelectableFolder : ObservableObject
{
    public RecipeFolder Folder { get; set; }

    [ObservableProperty]
    private bool isSelected;
}

#endregion
//--------------

[QueryProperty(nameof(TargetRecipe), "Recipe")]
[QueryProperty(nameof(IsFromNewRecipe), "IsFromNewRecipe")]
[QueryProperty(nameof(IsImportMode), "IsImportMode")]
[QueryProperty(nameof(ImportedFolderJson), "ImportedFolderJson")]
public partial class FolderSelectionViewModel : ObservableObject
{
    private readonly RecipesDatabase _database;
    private List<RecipeFolder> _allFolders = new();

    //--------------
    #region Properties
    //--------------

    [ObservableProperty]
    private Recipe targetRecipe;

    [ObservableProperty]
    private bool isFromNewRecipe;

    // --- NEW: Import Mode Properties ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRecipeMode))]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    [NotifyPropertyChangedFor(nameof(SaveButtonText))]
    private bool isImportMode;

    public bool IsRecipeMode => !IsImportMode; // Simple flag for the XAML to hide/show things
    public string PageTitle => IsImportMode ? "בחר מיקום לתיקייה" : "שמירה בתיקיות";
    public string SaveButtonText => IsImportMode ? "שמור כאן" : "שמור";

    [ObservableProperty]
    private string importedFolderJson;

    // --- NEW: Loading Overlay Properties ---
    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string loadingText = "טוען...";

    [ObservableProperty]
    private string searchQuery;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInnerFolder))]
    private RecipeFolder currentFolder;

    public bool IsInnerFolder => CurrentFolder != null;

    public ObservableCollection<SelectableFolder> DisplayedFolders { get; } = new();
    public ObservableCollection<RecipeFolder> SelectedFolders { get; } = new();

    #endregion
    //--------------

    public FolderSelectionViewModel(RecipesDatabase database)
    {
        _database = database;
    }

    //--------------
    #region Logic & Initialization
    //--------------

    partial void OnTargetRecipeChanged(Recipe value)
    {
        if (value != null && IsRecipeMode) _ = InitializeAsync();
    }

    partial void OnIsImportModeChanged(bool value)
    {
        if (value) _ = InitializeAsync(); // Trigger init if we opened in Import mode (since TargetRecipe will be null)
    }

    partial void OnSearchQueryChanged(string value)
    {
        LoadCurrentLevelFolders();
    }

    private async Task InitializeAsync()
    {
        _allFolders = await _database.GetFoldersAsync();

        if (IsRecipeMode && TargetRecipe != null)
        {
            var currentMappings = await _database.GetFoldersForRecipeAsync(TargetRecipe.Id);
            SelectedFolders.Clear();
            foreach (var folder in currentMappings)
            {
                SelectedFolders.Add(folder);
            }
        }

        LoadCurrentLevelFolders();
    }

    private void LoadCurrentLevelFolders()
    {
        if (_allFolders == null) return;

        DisplayedFolders.Clear();
        List<RecipeFolder> foldersToShow;

        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            int? targetParentId = CurrentFolder?.Id;
            foldersToShow = _allFolders.Where(f => f.ParentFolderId == targetParentId).ToList();
        }
        else
        {
            foldersToShow = _allFolders.Where(f => f.Name != null && f.Name.Contains(SearchQuery)).ToList();
        }

        foreach (var folder in foldersToShow)
        {
            bool isAlreadySelected = SelectedFolders.Any(s => s.Id == folder.Id);

            DisplayedFolders.Add(new SelectableFolder
            {
                Folder = folder,
                IsSelected = isAlreadySelected
            });
        }
    }

    private async void CloseScreen()
    {
        if (IsFromNewRecipe || IsImportMode)
        {
            await Shell.Current.GoToAsync("../..");
        }
        else
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    #endregion
    //--------------

    //--------------
    #region Commands
    //--------------

    [RelayCommand]
    public void ToggleFolderSelection(SelectableFolder selectableFolder)
    {
        if (selectableFolder == null || IsImportMode) return; // Disable checkboxes in import mode

        selectableFolder.IsSelected = !selectableFolder.IsSelected;
        var existingInSelected = SelectedFolders.FirstOrDefault(f => f.Id == selectableFolder.Folder.Id);

        if (selectableFolder.IsSelected && existingInSelected == null)
            SelectedFolders.Add(selectableFolder.Folder);
        else if (!selectableFolder.IsSelected && existingInSelected != null)
            SelectedFolders.Remove(existingInSelected);
    }

    [RelayCommand]
    public void RemoveFromSelected(RecipeFolder folderToRemove)
    {
        if (folderToRemove == null) return;
        SelectedFolders.Remove(folderToRemove);

        var displayedItem = DisplayedFolders.FirstOrDefault(d => d.Folder.Id == folderToRemove.Id);
        if (displayedItem != null) displayedItem.IsSelected = false;
    }

    [RelayCommand]
    public void OpenFolder(RecipeFolder folder)
    {
        CurrentFolder = folder;
        if (!string.IsNullOrEmpty(SearchQuery))
            SearchQuery = string.Empty;
        else
            LoadCurrentLevelFolders();
    }

    [RelayCommand]
    public void GoUp()
    {
        if (CurrentFolder == null) return;

        if (CurrentFolder.ParentFolderId == null || CurrentFolder.ParentFolderId == 0)
            CurrentFolder = null;
        else
            CurrentFolder = _allFolders.FirstOrDefault(f => f.Id == CurrentFolder.ParentFolderId);

        LoadCurrentLevelFolders();
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (IsImportMode)
        {
            // --- THE IMPORT ENGINE TRIGGER ---
            IsLoading = true;
            LoadingText = "מוריד מתכונים ובונה את התיקייה...";

            try
            {
                int? targetParentId = CurrentFolder?.Id;
                await _database.ImportSharedFolderAsync(ImportedFolderJson, targetParentId);
            }
            finally
            {
                IsLoading = false;
                CloseScreen();
            }
        }
        else
        {
            // Existing recipe mapping logic
            await _database.UpdateRecipeFoldersAsync(TargetRecipe.Id, SelectedFolders.Select(f => f.Id).ToList());
            CloseScreen();
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        CloseScreen();
    }

    #endregion
    //--------------
}