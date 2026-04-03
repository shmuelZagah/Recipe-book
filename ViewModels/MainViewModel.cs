using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls;
using Recipe_book.Models.Recipes;
using Recipe_book.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;


namespace Recipe_book.ViewModels;

/// <summary>
/// ViewModel for the main dashboard, displaying favorites, unused recipes, and today's schedule.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly RecipesDatabase _database;
    private List<Recipe> _allRecipesCache = new();

    //--------------
    #region Properties
    //--------------

    [ObservableProperty]
    private string searchQuery;

    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchQuery);

    public ObservableCollection<Recipe> Recipes { get; } = new();
    public ObservableCollection<Recipe> FavoriteRecipes { get; } = new();
    public ObservableCollection<Recipe> DidntUsedForLongTime { get; } = new();
    public ObservableCollection<MealGroup> TodayMeals { get; } = new();

    #endregion
    //--------------

    public MainViewModel(RecipesDatabase database)
    {
        _database = database;

        WeakReferenceMessenger.Default.Register<string>(this, async (r, m) =>
        {
            if (m == "RecipesChanged")
            {
                await LoadRecipesCommand.ExecuteAsync(null);
            }
            else if (m == "ScheduleChanged")
            {
                await LoadTodayScheduleAsync();
            }
        });
    }

    //--------------
    #region Logic & Initialization
    //--------------

    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(IsSearching));
        FilterRecipes();
    }

    private void FilterRecipes()
    {
        Recipes.Clear();

        var filtered = string.IsNullOrWhiteSpace(SearchQuery)
            ? _allRecipesCache
            : _allRecipesCache.Where(r => r.Title.Contains(SearchQuery)).ToList();

        foreach (var recipe in filtered)
        {
            Recipes.Add(recipe);
        }
    }

    private async Task LoadTodayScheduleAsync()
    {
        TodayMeals.Clear();
        DateTime today = DateTime.Today;

        var customCategories = await _database.GetMealCategoriesAsync(today);
        var mealsToday = await _database.GetScheduledMealsAsync(today, today.AddDays(1));

        List<MealGroup> groups = new();

        if (customCategories.Any())
        {
            foreach (var cat in customCategories)
            {
                groups.Add(new MealGroup { GroupName = cat.GroupName, Date = today, IsExpanded = true });
            }
        }
        else
        {
            groups.Add(new MealGroup { GroupName = "בוקר", Date = today, IsExpanded = true });
            groups.Add(new MealGroup { GroupName = "צהריים", Date = today, IsExpanded = true });
            groups.Add(new MealGroup { GroupName = "ערב", Date = today, IsExpanded = true });
        }

        foreach (var scheduledMeal in mealsToday.Where(m => m.Date.Date == today))
        {
            var fullRecipe = await _database.GetRecipeAsync(scheduledMeal.RecipeId);
            if (fullRecipe != null)
            {
                var targetGroup = groups.FirstOrDefault(g => g.GroupName == scheduledMeal.MealType);
                if (targetGroup == null)
                {
                    targetGroup = new MealGroup { GroupName = scheduledMeal.MealType, Date = today, IsExpanded = true };
                    groups.Add(targetGroup);
                }

                targetGroup.Meals.Add(new ScheduledMealDisplay
                {
                    MealRecord = scheduledMeal,
                    RecipeDetails = fullRecipe
                });
            }
        }

        foreach (var g in groups)
        {
            TodayMeals.Add(g);
        }
    }

    #endregion
    //--------------

    //--------------
    #region Commands
    //--------------

    [RelayCommand]
    public async Task LoadRecipesAsync()
    {
        _allRecipesCache = await _database.GetRecipesAsync();

        FavoriteRecipes.Clear();
        DidntUsedForLongTime.Clear();

        var allRecipesByUsed = from r in _allRecipesCache
                               orderby r.LastCookedDate ascending
                               select r;

        int i = 0, n = 5;
        foreach (var recipe in allRecipesByUsed)
        {
            if (recipe.IsFavorite)
                FavoriteRecipes.Add(recipe);

            if (i < n)
            {
                DidntUsedForLongTime.Add(recipe);
                i++;
            }
        }

        FilterRecipes();
        await LoadTodayScheduleAsync();
    }

    [RelayCommand]
    public async Task DeleteRecipeAsync(Recipe recipe)
    {
        if (recipe == null) return;

        await _database.DeleteRecipeAsync(recipe);
        _allRecipesCache.Remove(recipe);

        FavoriteRecipes.Remove(recipe);
        DidntUsedForLongTime.Remove(recipe);

        FilterRecipes();
    }

    [RelayCommand]
    public async Task AddRecipeAsync()
    {
        await Shell.Current.GoToAsync(nameof(Views.SubPages.RecipeEditorPage));
    }

    [RelayCommand]
    public async Task OpenRecipeAsync(Recipe recipe)
    {
        if (recipe == null) return;

        var navigationParameter = new Dictionary<string, object>
        {
            { "Recipe", recipe }
        };

        await Shell.Current.GoToAsync(nameof(Views.SubPages.RecipeViewerPage), navigationParameter);
    }

    [RelayCommand]
    public void GoToSchedule(MealGroup selectedMeal)
    {
        if (selectedMeal != null)
        {
            WeeklyScheduleViewModel.PendingTargetMeal = selectedMeal.GroupName;
        }

        Recipe_book.MainPage.SwitchTabAction?.Invoke(2);

        WeeklyScheduleViewModel.OpenPendingMealAction?.Invoke();
    }

    [RelayCommand]
    public async Task GoToShoppingListAsync()
    {
        await Shell.Current.GoToAsync("ShoppingListPage");
    }

    #endregion
    //--------------
}