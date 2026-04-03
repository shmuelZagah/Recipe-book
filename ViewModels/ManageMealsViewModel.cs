using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Recipe_book.Models.Recipes;
using Recipe_book.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Recipe_book.ViewModels;

/// <summary>
/// ViewModel for customizing the daily meal categories (e.g., Breakfast, Lunch) and their order for a specific day.
/// </summary>
[QueryProperty(nameof(TargetDay), "TargetDay")]
public partial class ManageMealsViewModel : ObservableObject
{
    private readonly RecipesDatabase _database;

    //--------------
    #region Properties
    //--------------

    [ObservableProperty]
    private DailySchedule targetDay;

    #endregion
    //--------------

    public ManageMealsViewModel(RecipesDatabase database)
    {
        _database = database;
    }

    //--------------
    #region Commands
    //--------------

    [RelayCommand]
    public void AddMeal()
    {
        if (TargetDay != null)
        {
            TargetDay.MealGroups.Add(new MealGroup { GroupName = "ארוחה חדשה", Date = TargetDay.Date });
        }
    }

    [RelayCommand]
    public async Task RemoveMealAsync(MealGroup group)
    {
        if (group == null || TargetDay == null) return;

        // Display a warning if the user tries to delete a meal category that contains scheduled recipes
        if (group.Meals.Any())
        {
            bool confirm = await Shell.Current.DisplayAlert(
                "אזהרה",
                $"בארוחה '{group.GroupName}' יש מתכונים ששובצו. מחיקת הארוחה תסיר גם אותם מהתצוגה. להמשיך?",
                "כן, מחיקה", "ביטול");

            if (!confirm) return;
        }

        TargetDay.MealGroups.Remove(group);
    }

    /// <summary>
    /// Saves the customized meal categories and updates any existing scheduled recipes 
    /// to reflect potential category name changes made by the user.
    /// </summary>
    [RelayCommand]
    public async Task SaveAndCloseAsync()
    {
        if (TargetDay != null)
        {
            var categoriesToSave = new List<DailyMealCategory>();

            for (int i = 0; i < TargetDay.MealGroups.Count; i++)
            {
                var currentGroup = TargetDay.MealGroups[i];

                categoriesToSave.Add(new DailyMealCategory
                {
                    Date = TargetDay.Date.Date,
                    GroupName = currentGroup.GroupName,
                    DisplayOrder = i // Maintains the drag-and-drop UI order
                });

                // Update scheduled recipes if the category name was changed 
                foreach (var mealDisplay in currentGroup.Meals)
                {
                    if (mealDisplay.MealRecord.MealType != currentGroup.GroupName)
                    {
                        mealDisplay.MealRecord.MealType = currentGroup.GroupName;
                        await _database.SaveScheduledMealAsync(mealDisplay.MealRecord);
                    }
                }
            }

            await _database.SaveMealCategoriesAsync(TargetDay.Date.Date, categoriesToSave);
        }

        WeakReferenceMessenger.Default.Send("ScheduleChanged");
        await Shell.Current.GoToAsync("..");
    }

    #endregion
    //--------------
}