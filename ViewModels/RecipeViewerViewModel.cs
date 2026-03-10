using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Recipe_book.Models.Recipes;
using Recipe_book.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace Recipe_book.ViewModels;

//--------------
#region Helper Classes
//--------------

public class DateShortcut
{
    public string Name { get; set; }
    public DateTime Date { get; set; }
}

#endregion
//--------------

/// <summary>
/// ViewModel for displaying a recipe's details, steps, and allowing the user to schedule it.
/// </summary>
public partial class RecipeViewerViewModel : ObservableObject, IQueryAttributable
{
    private readonly RecipesDatabase _database;

    //--------------
    #region Properties
    //--------------

    [ObservableProperty]
    private Recipe currentRecipe;

    [ObservableProperty]
    private bool isIngredientsMode = true;

    public ObservableCollection<Ingredient> IngredientsList { get; } = new();
    public ObservableCollection<RecipeStep> StepsList { get; } = new();

    #endregion
    //--------------

    public RecipeViewerViewModel(RecipesDatabase database)
    {
        _database = database;
    }

    //--------------
    #region Initialization
    //--------------

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey("Recipe"))
        {
            CurrentRecipe = query["Recipe"] as Recipe;
        }
    }

    private async void LoadRecipeDetails()
    {
        if (CurrentRecipe == null) return;

        var ingredients = await _database.GetIngredientsAsync(CurrentRecipe.Id);
        IngredientsList.Clear();
        foreach (var item in ingredients.OrderBy(i => i.OrderIndex))
            IngredientsList.Add(item);

        var steps = await _database.GetStepsAsync(CurrentRecipe.Id);
        StepsList.Clear();
        foreach (var item in steps.OrderBy(s => s.StepNumber))
            StepsList.Add(item);
    }

    public async Task RefreshRecipeAsync()
    {
        if (CurrentRecipe != null && CurrentRecipe.Id != 0)
        {
            var updatedRecipe = await _database.GetRecipeAsync(CurrentRecipe.Id);
            if (updatedRecipe != null)
            {
                CurrentRecipe = updatedRecipe;

                LoadRecipeDetails();
            }
        }
    }

    #endregion
    //--------------

    //--------------
    #region Schedule Overlay Logic
    //--------------

    [ObservableProperty]
    private bool isScheduleOverlayVisible = false;

    [ObservableProperty]
    private DateTime selectedScheduleDate = DateTime.Today;

    [ObservableProperty]
    private string selectedMealType;

    public ObservableCollection<string> AvailableMeals { get; } = new();
    public ObservableCollection<DateShortcut> DateShortcuts { get; } = new();

    [RelayCommand]
    public async Task OpenScheduleOverlayAsync()
    {
        GenerateShortcuts();
        SelectedScheduleDate = DateTime.Today;
        await LoadMealsForDateAsync(SelectedScheduleDate);
        IsScheduleOverlayVisible = true;
    }

    [RelayCommand]
    public void CloseScheduleOverlay() => IsScheduleOverlayVisible = false;

    partial void OnSelectedScheduleDateChanged(DateTime value)
    {
        _ = LoadMealsForDateAsync(value);
    }

    private async Task LoadMealsForDateAsync(DateTime date)
    {
        var categories = await _database.GetMealCategoriesAsync(date);
        var mealNames = categories.Select(c => c.GroupName).ToList();

        AvailableMeals.Clear();
        if (mealNames.Any())
        {
            foreach (var meal in mealNames) AvailableMeals.Add(meal);
        }
        else
        {
            AvailableMeals.Add("בוקר");
            AvailableMeals.Add("צהריים");
            AvailableMeals.Add("ערב");
        }

        SelectedMealType = AvailableMeals.FirstOrDefault();
    }

    private void GenerateShortcuts()
    {
        DateShortcuts.Clear();
        DateShortcuts.Add(new DateShortcut { Name = "היום", Date = DateTime.Today });
        DateShortcuts.Add(new DateShortcut { Name = "מחר", Date = DateTime.Today.AddDays(1) });

        string[] dayNames = { "ראשון", "שני", "שלישי", "רביעי", "חמישי", "שישי", "שבת" };
        for (int i = 2; i <= 6; i++)
        {
            var d = DateTime.Today.AddDays(i);
            DateShortcuts.Add(new DateShortcut { Name = dayNames[(int)d.DayOfWeek], Date = d });
        }
    }

    [RelayCommand]
    public void ApplyShortcut(DateTime date)
    {
        SelectedScheduleDate = date;
    }

    [RelayCommand]
    public async Task SaveScheduleAsync()
    {
        if (string.IsNullOrEmpty(SelectedMealType)) return;

        var newMeal = new ScheduledMeal
        {
            Date = SelectedScheduleDate,
            MealType = SelectedMealType,
            RecipeId = CurrentRecipe.Id
        };

        await _database.SaveScheduledMealAsync(newMeal);
        IsScheduleOverlayVisible = false;
        await Application.Current.MainPage.DisplayAlert("מעולה!", "המתכון נוסף ללו\"ז בהצלחה.", "אישור");
    }

    #endregion
    //--------------

    //--------------
    #region Commands
    //--------------

    [RelayCommand]
    public void ShowIngredients() => IsIngredientsMode = true;

    [RelayCommand]
    public void ShowSteps() => IsIngredientsMode = false;

    [RelayCommand]
    public async Task ToggleFavoriteAsync()
    {
        if (CurrentRecipe == null) return;

        CurrentRecipe.IsFavorite = !CurrentRecipe.IsFavorite;
        await _database.SaveRecipeAsync(CurrentRecipe);
    }

    [RelayCommand]
    public async Task ToggleStepCompleteAsync(RecipeStep step)
    {
        if (step != null)
            step.IsCompleted = !step.IsCompleted;

        if (step.IsCompleted && CurrentRecipe != null)
        {
            CurrentRecipe.LastCookedDate = DateTime.Now;
            await _database.SaveRecipeAsync(CurrentRecipe);
        }
    }

    [RelayCommand]
    public async Task EditRecipeAsync()
    {
        if (CurrentRecipe == null) return;

        var navigationParameter = new Dictionary<string, object>
        {
            { "RecipeToEdit", CurrentRecipe }
        };

        await Shell.Current.GoToAsync(nameof(Views.SubPages.RecipeEditorPage), navigationParameter);
    }

    [RelayCommand]
    public async Task OpenFolderSelectionAsync()
    {
        if (CurrentRecipe == null) return;

        var navigationParameter = new Dictionary<string, object>
        {
            { "Recipe", CurrentRecipe }
        };

        await Shell.Current.GoToAsync(nameof(Views.SubPages.FolderSelectionPage), navigationParameter);
    }

    [RelayCommand]
    public async Task DeleteRecipeAsync()
    {
        bool answer = await Application.Current.MainPage.DisplayAlert(
            "מחיקת מתכון",
            "האם אתה בטוח שברצונך למחוק את המתכון?",
            "כן, מחק",
            "ביטול");

        if (answer)
        {
            var recipeToDelete = await _database.GetRecipeAsync(CurrentRecipe.Id);
            if (recipeToDelete != null)
            {
                await _database.DeleteRecipeAsync(recipeToDelete);
            }

            await Shell.Current.GoToAsync("..");
        }
    }

    #endregion
    //--------------
}