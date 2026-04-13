using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Recipe_book.Models.Organization;
using Recipe_book.Models.Recipes;
using Recipe_book.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Recipe_book.ViewModels;

[QueryProperty(nameof(SelectedDate), "SelectedDate")]
[QueryProperty(nameof(MealType), "MealType")]
public partial class SelectRecipeViewModel : ObservableObject
{
    private readonly RecipesDatabase _database;
    private List<Recipe> _allRecipesCache = new();
    private List<RecipeFolder> _allFoldersCache = new();

    #region Properties

    [ObservableProperty]
    private DateTime selectedDate;

    [ObservableProperty]
    private string mealType;

    [ObservableProperty]
    private string searchQuery;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInnerFolder))]
    private RecipeFolder currentFolder;

    // Returns true if the user is inside a subfolder
    public bool IsInnerFolder => CurrentFolder != null;

    public ObservableCollection<RecipeFolder> Folders { get; } = new();
    public ObservableCollection<Recipe> Recipes { get; } = new();

    #endregion

    public SelectRecipeViewModel(RecipesDatabase database)
    {
        _database = database;
    }

    #region Logic

    partial void OnSearchQueryChanged(string value)
    {
        FilterContent();
    }

    // Filters both folders and recipes based on search query or current navigation level
    private void FilterContent()
    {
        Folders.Clear();
        Recipes.Clear();

        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            // Show only items in current folder level
            int? parentId = CurrentFolder?.Id;

            var filteredFolders = _allFoldersCache.Where(f => f.ParentFolderId == parentId);
            foreach (var f in filteredFolders) Folders.Add(f);

            // Fetch recipes for this specific folder level from DB
            _ = LoadCurrentLevelRecipes(parentId);
        }
        else
        {
            // Global search across all folders and recipes
            var filteredFolders = _allFoldersCache.Where(f => f.Name != null && f.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
            foreach (var f in filteredFolders) Folders.Add(f);

            var filteredRecipes = _allRecipesCache.Where(r => r.Title != null && r.Title.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
            foreach (var r in filteredRecipes) Recipes.Add(r);
        }
    }

    private async Task LoadCurrentLevelRecipes(int? folderId)
    {
        List<Recipe> recipes;
        if (folderId == null || folderId == 0)
            recipes = await _database.GetRecipesWithoutFolderAsync();
        else
            recipes = await _database.GetRecipesInFolderAsync(folderId.Value);

        foreach (var r in recipes) Recipes.Add(r);
    }

    #endregion

    #region Commands

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        _allRecipesCache = await _database.GetRecipesAsync();
        _allFoldersCache = await _database.GetFoldersAsync();
        FilterContent();
    }

    [RelayCommand]
    public void OpenFolder(RecipeFolder folder)
    {
        if (folder == null) return;
        CurrentFolder = folder;
        FilterContent();
    }

    [RelayCommand]
    public void GoUp()
    {
        if (CurrentFolder == null) return;

        CurrentFolder = _allFoldersCache.FirstOrDefault(f => f.Id == CurrentFolder.ParentFolderId);
        FilterContent();
    }

    [RelayCommand]
    public async Task SelectRecipeAsync(Recipe selectedRecipe)
    {
        if (selectedRecipe == null) return;

        var meal = new ScheduledMeal
        {
            Date = SelectedDate.Date,
            RecipeId = selectedRecipe.Id,
            MealType = MealType
        };

        await _database.SaveScheduledMealAsync(meal);
        WeakReferenceMessenger.Default.Send("ScheduleChanged");
        await Shell.Current.GoToAsync("..");
    }

    #endregion
}