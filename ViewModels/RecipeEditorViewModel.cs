using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using Recipe_book.Models.Recipes;
using Recipe_book.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Recipe_book.ViewModels;

/// <summary>
/// ViewModel for creating and editing recipes, managing both ingredients and preparation steps.
/// </summary>
public partial class RecipeEditorViewModel : ObservableObject, IQueryAttributable
{
    private readonly RecipesDatabase _database;
    private int _existingRecipeId = 0;

    //--------------
    #region Properties
    //--------------

    /// <summary>
    /// Toggles the UI between Ingredients mode (true) and Steps mode (false).
    /// </summary>
    [ObservableProperty]
    private bool isIngredientsMode = true;

    [ObservableProperty]
    private string recipeImage;

    [ObservableProperty]
    private string recipeTitle;

    [ObservableProperty]
    private string recipeDescription;

    [ObservableProperty]
    private string prepTime;

    [ObservableProperty]
    private string servings;

    public ObservableCollection<string> AvailableUnits { get; } = new()
    {
        "יחידות", "כפית", "כף", "כוס", "גרם", "ק״ג", "מ״ל", "ליטר"
    };

    public ObservableCollection<Ingredient> IngredientsList { get; } = new();
    public ObservableCollection<RecipeStep> StepsList { get; } = new();

    [ObservableProperty]
    private string pageTitle = "צור מתכון";


    #endregion
    //--------------

    public RecipeEditorViewModel(RecipesDatabase database)
    {
        _database = database;
    }

    //--------------
    #region Initialization & Navigation
    //--------------

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.ContainsKey("RecipeToEdit"))
        {
            if (query["RecipeToEdit"] is Recipe recipe)
            {
                LoadRecipeForEditing(recipe);
            }
        }
        else
        {
            // Default empty state for a new recipe
            if (IngredientsList.Count == 0) AddIngredient();
            if (StepsList.Count == 0) AddStep();
        }
    }

    private async void LoadRecipeForEditing(Recipe recipe)
    {
        _existingRecipeId = recipe.Id;
        PageTitle = "עריכת מתכון";
        RecipeTitle = recipe.Title;
        RecipeDescription = recipe.Description;
        RecipeImage = recipe.DisplayImage;
        PrepTime = recipe.PrepTime; 
        Servings = recipe.Servings;

        var ingredients = await _database.GetIngredientsAsync(recipe.Id);
        IngredientsList.Clear();
        foreach (var item in ingredients.OrderBy(i => i.OrderIndex))
            IngredientsList.Add(item);

        var steps = await _database.GetStepsAsync(recipe.Id);
        StepsList.Clear();
        foreach (var item in steps.OrderBy(s => s.StepNumber))
            StepsList.Add(item);
    }

    #endregion
    //--------------

    //--------------
    #region General Commands
    //--------------

    [RelayCommand]
    public void ShowIngredients() => IsIngredientsMode = true;

    [RelayCommand]
    public void ShowSteps() => IsIngredientsMode = false;


    [RelayCommand]
    public async Task ChangeImageAsync()
    {
        try
        {
            // Using FilePicker delegates file access to the OS, 
            // avoiding the need for explicit storage permissions.
            var photo = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "בחר תמונה",
                FileTypes = FilePickerFileType.Images
            });

            if (photo != null)
            {
                // Save the selected image to the application's local data directory
                var newFile = Path.Combine(FileSystem.AppDataDirectory, photo.FileName);

                using (var stream = await photo.OpenReadAsync())
                using (var newStream = File.OpenWrite(newFile))
                {
                    await stream.CopyToAsync(newStream);
                }

                RecipeImage = newFile;
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("שגיאה", "לא הצלחנו לטעון את התמונה: " + ex.Message, "אישור");
        }
    }

    [RelayCommand]
    public async Task SaveRecipeAsync()
    {
        if (string.IsNullOrWhiteSpace(RecipeTitle))
        {
            await Application.Current.MainPage.DisplayAlert("שגיאה", "חייב לתת שם למתכון!", "אישור");
            return;
        }

        bool isNewRecipe = _existingRecipeId == 0;
        Recipe recipeToSave;

        // Fetch existing recipe to prevent overwriting fields like IsFavorite
        if (!isNewRecipe)
        {
            recipeToSave = await _database.GetRecipeAsync(_existingRecipeId);
            if (recipeToSave == null)
            {
                recipeToSave = new Recipe();
            }
        }
        else
        {
            recipeToSave = new Recipe();
        }

        recipeToSave.Id = _existingRecipeId;
        recipeToSave.Title = RecipeTitle;

        if (!string.IsNullOrEmpty(RecipeImage) && RecipeImage.StartsWith("http"))
        {
            recipeToSave.CloudImagePath = RecipeImage;
            recipeToSave.LocalImagePath = null;
        }
        else
        {
            recipeToSave.LocalImagePath = RecipeImage;
        }

        recipeToSave.Description = RecipeDescription;
        recipeToSave.PrepTime = PrepTime; 
        recipeToSave.Servings = Servings;

        await _database.SaveRecipeAsync(recipeToSave);

        // Sync deletions of ingredients and steps
        if (!isNewRecipe)
        {
            var existingIngredients = await _database.GetIngredientsAsync(_existingRecipeId);
            var currentIngredientIds = IngredientsList.Select(i => i.Id).ToList();
            foreach (var dbIng in existingIngredients)
            {
                if (!currentIngredientIds.Contains(dbIng.Id))
                    await _database.DeleteIngredientAsync(dbIng);
            }

            var existingSteps = await _database.GetStepsAsync(_existingRecipeId);
            var currentStepIds = StepsList.Select(s => s.Id).ToList();
            foreach (var dbStep in existingSteps)
            {
                if (!currentStepIds.Contains(dbStep.Id))
                    await _database.DeleteStepAsync(dbStep);
            }
        }

        // Save current ingredients
        for (int i = 0; i < IngredientsList.Count; i++)
        {
            var ingredient = IngredientsList[i];
            if (!string.IsNullOrWhiteSpace(ingredient.Name))
            {
                ingredient.RecipeId = recipeToSave.Id;
                ingredient.OrderIndex = i;
                await _database.SaveIngredientAsync(ingredient);
            }
        }

        // Save current steps
        for (int i = 0; i < StepsList.Count; i++)
        {
            var step = StepsList[i];
            if (!string.IsNullOrWhiteSpace(step.Description))
            {
                step.RecipeId = recipeToSave.Id;
                step.StepNumber = i + 1;
                await _database.SaveStepAsync(step);
            }
        }


        if (isNewRecipe)
        {
            var navigationParameter = new Dictionary<string, object>
            {
                { "Recipe", recipeToSave },
                { "IsFromNewRecipe", true }
            };

            WeakReferenceMessenger.Default.Send("RefreshRecipes");
            await Shell.Current.GoToAsync(nameof(Views.SubPages.FolderSelectionPage), navigationParameter);
        }
        else
        {
            WeakReferenceMessenger.Default.Send("RefreshRecipes");
            await Shell.Current.GoToAsync("..");
        }
    }

    [RelayCommand]
    public async Task DeleteRecipeAsync()
    {
        if (_existingRecipeId == 0)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        bool answer = await Application.Current.MainPage.DisplayAlert(
            "מחיקת מתכון",
            "האם אתה בטוח שברצונך למחוק את המתכון?",
            "כן, מחק",
            "ביטול");

        if (answer)
        {
            var recipeToDelete = await _database.GetRecipeAsync(_existingRecipeId);
            if (recipeToDelete != null)
            {
                await _database.DeleteRecipeAsync(recipeToDelete);
            }

            await Shell.Current.GoToAsync("../..");
        }
    }

    #endregion
    //--------------

    //--------------
    #region Ingredients Management
    //--------------

    [RelayCommand]
    public void AddIngredient()
    {
        var emptyItem = new Ingredient
        {
            Name = "",
            Quantity = null,
            Unit = "יחידות"
        };

        IngredientsList.Add(emptyItem);
    }

    [RelayCommand]
    public void RemoveIngredient(Ingredient ingredient)
    {
        if (IngredientsList.Contains(ingredient))
        {
            IngredientsList.Remove(ingredient);
        }
    }

    #endregion
    //--------------

    //--------------
    #region Steps Management
    //--------------

    [RelayCommand]
    public void AddStep()
    {
        var newStep = new RecipeStep
        {
            Description = "",
            StepNumber = StepsList.Count + 1,
            IsOptional = false,
            IsCompleted = false
        };

        StepsList.Add(newStep);
    }

    [RelayCommand]
    public void RemoveStep(RecipeStep step)
    {
        if (StepsList.Contains(step))
        {
            StepsList.Remove(step);
            UpdateStepNumbering();
        }
    }

    private void UpdateStepNumbering()
    {
        for (int i = 0; i < StepsList.Count; i++)
            StepsList[i].StepNumber = i + 1;
    }

    [RelayCommand]
    public void ToggleOptionalStep(RecipeStep step)
    {
        if (step != null)
            step.IsOptional = !step.IsOptional;
    }

    #endregion
    //--------------
}