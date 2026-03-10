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

/// <summary>
/// A wrapper class for a RecipeFolder that adds a selectable state for the UI.
/// </summary>
public partial class SelectableFolder : ObservableObject
{
    public RecipeFolder Folder { get; set; }

    [ObservableProperty]
    private bool isSelected;
}

#endregion
//--------------

/// <summary>
/// ViewModel for selecting and managing which folders a specific recipe belongs to.
/// </summary>
[QueryProperty(nameof(TargetRecipe), "Recipe")]
[QueryProperty(nameof(IsFromNewRecipe), "IsFromNewRecipe")]
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

    [ObservableProperty]
    private string searchQuery;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInnerFolder))]
    private RecipeFolder currentFolder;

    public bool IsInnerFolder => CurrentFolder != null;

    public ObservableCollection<SelectableFolder> DisplayedFolders { get; } = new();

    // The collection of chips displayed at the bottom for currently selected folders
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
        if (value != null)
        {
            _ = InitializeAsync();
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        LoadCurrentLevelFolders();
    }

    private async Task InitializeAsync()
    {
        _allFolders = await _database.GetFoldersAsync();

        // Load previously selected folders for this recipe
        var currentMappings = await _database.GetFoldersForRecipeAsync(TargetRecipe.Id);

        SelectedFolders.Clear();
        foreach (var folder in currentMappings)
        {
            SelectedFolders.Add(folder);
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
        if (IsFromNewRecipe)
        {
            // Closes both the modal and the underlying editor, returning to the main screen
            await Shell.Current.GoToAsync("../..");
        }
        else
        {
            // Closes only the modal, returning to the recipe viewer
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
        if (selectableFolder == null) return;

        selectableFolder.IsSelected = !selectableFolder.IsSelected;

        var existingInSelected = SelectedFolders.FirstOrDefault(f => f.Id == selectableFolder.Folder.Id);

        if (selectableFolder.IsSelected && existingInSelected == null)
        {
            SelectedFolders.Add(selectableFolder.Folder);
        }
        else if (!selectableFolder.IsSelected && existingInSelected != null)
        {
            SelectedFolders.Remove(existingInSelected);
        }
    }

    [RelayCommand]
    public void RemoveFromSelected(RecipeFolder folderToRemove)
    {
        if (folderToRemove == null) return;

        SelectedFolders.Remove(folderToRemove);

        // Update the UI if the removed folder is currently displayed on screen
        var displayedItem = DisplayedFolders.FirstOrDefault(d => d.Folder.Id == folderToRemove.Id);
        if (displayedItem != null)
        {
            displayedItem.IsSelected = false;
        }
    }

    [RelayCommand]
    public void OpenFolder(RecipeFolder folder)
    {
        CurrentFolder = folder;

        if (!string.IsNullOrEmpty(SearchQuery))
        {
            SearchQuery = string.Empty; // Triggers OnSearchQueryChanged automatically
        }
        else
        {
            LoadCurrentLevelFolders();
        }
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
        await _database.UpdateRecipeFoldersAsync(TargetRecipe.Id, SelectedFolders.Select(f => f.Id).ToList());
        CloseScreen();
    }

    [RelayCommand]
    public void Cancel()
    {
        CloseScreen();
    }

    #endregion
    //--------------
}