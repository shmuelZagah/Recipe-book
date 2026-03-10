using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Recipe_book.Models.Recipes;
using Recipe_book.Services;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Recipe_book.ViewModels;

/// <summary>
/// ViewModel for displaying all available recipes in a simple alphabetical list format.
/// </summary>
public partial class AllRecipesViewModel : ObservableObject
{
    private readonly RecipesDatabase _database;

    //--------------
    #region Properties
    //--------------

    public ObservableCollection<Recipe> Recipes { get; } = new();

    #endregion
    //--------------

    public AllRecipesViewModel(RecipesDatabase database)
    {
        _database = database;
    }

    //--------------
    #region Commands
    //--------------

    /// <summary>
    /// Loads all recipes from the database and orders them alphabetically.
    /// </summary>
    [RelayCommand]
    public async Task LoadRecipesAsync()
    {
        var all = await _database.GetRecipesAsync();
        Recipes.Clear();

        foreach (var recipe in all.OrderBy(r => r.Title))
        {
            Recipes.Add(recipe);
        }
    }

    /// <summary>
    /// Navigates to the recipe viewer for the selected recipe.
    /// </summary>
    [RelayCommand]
    public async Task OpenRecipeAsync(Recipe recipe)
    {
        if (recipe == null) return;

        var navParam = new Dictionary<string, object> { { "Recipe", recipe } };
        await Shell.Current.GoToAsync(nameof(Views.SubPages.RecipeViewerPage), navParam);
    }

    #endregion
    //--------------
}