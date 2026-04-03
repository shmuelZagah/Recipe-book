using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Recipe_book.Models.Recipes;
using Recipe_book.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Recipe_book.ViewModels;

/// <summary>
/// ViewModel for selecting a recipe to add to a specific scheduled meal in the planner.
/// Receives the target date and meal type as query parameters.
/// </summary>
[QueryProperty(nameof(SelectedDate), "SelectedDate")]
[QueryProperty(nameof(MealType), "MealType")]
public partial class SelectRecipeViewModel : ObservableObject
{
    private readonly RecipesDatabase _database;

    //--------------
    #region Properties
    //--------------

    [ObservableProperty]
    private DateTime selectedDate;

    [ObservableProperty]
    private string mealType;

    public ObservableCollection<Recipe> Recipes { get; } = new();

    #endregion
    //--------------

    public SelectRecipeViewModel(RecipesDatabase database)
    {
        _database = database;
    }

    //--------------
    #region Commands
    //--------------

    [RelayCommand]
    public async Task LoadRecipesAsync()
    {
        var allRecipes = await _database.GetRecipesAsync();

        Recipes.Clear();
        foreach (var recipe in allRecipes)
        {
            Recipes.Add(recipe);
        }
    }

    /// <summary>
    /// Creates a new scheduled meal with the selected recipe and navigates back to the schedule view.
    /// </summary>
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
    //--------------
}