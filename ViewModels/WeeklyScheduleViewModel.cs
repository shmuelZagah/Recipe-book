using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recipe_book.Models.Recipes;
using Recipe_book.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Recipe_book.ViewModels;

//--------------
#region Helper Classes for UI Binding
//--------------

/// <summary>
/// Represents a single meal scheduled in the UI, combining the schedule record with the full recipe details.
/// </summary>
public partial class ScheduledMealDisplay : ObservableObject
{
    public ScheduledMeal MealRecord { get; set; }
    public Recipe RecipeDetails { get; set; }
}

/// <summary>
/// Represents a category of meals (e.g., Breakfast, Lunch) for a specific day.
/// </summary>
public partial class MealGroup : ObservableObject
{
    public string GroupName { get; set; }
    public DateTime Date { get; set; }
    public ObservableCollection<ScheduledMealDisplay> Meals { get; set; } = new();

    [ObservableProperty]
    private bool isExpanded = false;
}

/// <summary>
/// Represents a single day in the weekly schedule, containing its grouped meals.
/// </summary>
public partial class DailySchedule : ObservableObject
{
    public string DayName { get; set; }
    public DateTime Date { get; set; }
    public string DateString => Date.ToString("dd.MM");

    [ObservableProperty]
    private ObservableCollection<MealGroup> mealGroups = new();
}

#endregion
//--------------

/// <summary>
/// ViewModel for managing and displaying the weekly recipe schedule.
/// </summary>
[QueryProperty(nameof(TargetMealName), "TargetMealName")]
public partial class WeeklyScheduleViewModel : ObservableObject
{
    private readonly RecipesDatabase _database;
    private DateTime _currentWeekStart;

    //--------------
    #region Properties
    //--------------

    private string _targetMealName;
    public string TargetMealName
    {
        get => _targetMealName;
        set
        {
            _targetMealName = value;
            HandleTargetMeal();
        }
    }

    [ObservableProperty]
    private string weekDateRangeText;

    [ObservableProperty]
    private DailySchedule selectedDay;

    [ObservableProperty]
    private ObservableCollection<DailySchedule> weekDays = new();

    [ObservableProperty]
    private DateTime selectedDatePickerDate = DateTime.Today;

    #endregion
    //--------------

    public WeeklyScheduleViewModel(RecipesDatabase database)
    {
        _database = database;
        SetCurrentWeekStart(DateTime.Today);
    }

    //--------------
    #region Lifecycle & Data Loading
    //--------------

    partial void OnSelectedDatePickerDateChanged(DateTime value)
    {
        // If the selected date is outside the currently displayed week, change the week
        if (value.Date < _currentWeekStart.Date || value.Date >= _currentWeekStart.AddDays(7).Date)
        {
            SetCurrentWeekStart(value);
            MainThread.BeginInvokeOnMainThread(async () => await LoadScheduleAsync());
        }
        else
        {
            // Otherwise, just select the specific day in the current week
            SelectedDay = WeekDays.FirstOrDefault(d => d.Date.Date == value.Date) ?? SelectedDay;
        }
    }

    private void SetCurrentWeekStart(DateTime targetDate)
    {
        // Calculate the start of the week (Sunday) explicitly using System.DayOfWeek to avoid Android namespace conflicts
        int diff = (7 + (targetDate.DayOfWeek - System.DayOfWeek.Sunday)) % 7;
        _currentWeekStart = targetDate.AddDays(-1 * diff).Date;

        WeekDateRangeText = $"{_currentWeekStart:dd.MM} - {_currentWeekStart.AddDays(6):dd.MM}";

        // Build a completely new list to avoid UI thread race conditions (Collection was modified exception)
        var newWeekDays = new ObservableCollection<DailySchedule>();
        string[] dayNames = { "ראשון", "שני", "שלישי", "רביעי", "חמישי", "שישי", "שבת" };

        for (int i = 0; i < 7; i++)
        {
            var newDay = new DailySchedule
            {
                DayName = dayNames[i],
                Date = _currentWeekStart.AddDays(i)
            };
            newWeekDays.Add(newDay);
        }

        WeekDays = newWeekDays;
        SelectedDay = WeekDays.FirstOrDefault(d => d.Date.Date == targetDate.Date) ?? WeekDays.First();
    }

    private void HandleTargetMeal()
    {
        if (string.IsNullOrEmpty(_targetMealName) || !WeekDays.Any()) return;

        var targetDay = WeekDays.FirstOrDefault(d => d.Date.Date == DateTime.Today);

        if (targetDay != null && targetDay.MealGroups.Any())
        {
            bool mealFound = false;

            foreach (var group in targetDay.MealGroups)
            {
                if (group.GroupName == _targetMealName)
                {
                    group.IsExpanded = true;
                    mealFound = true;
                }
                else
                {
                    group.IsExpanded = false;
                }
            }

            if (mealFound)
            {
                SelectedDay = targetDay;
                _targetMealName = null;
            }
        }
    }

    [RelayCommand]
    public async Task LoadScheduleAsync()
    {
        // Keep a local reference to avoid race conditions if the user navigates weeks quickly
        var currentDays = WeekDays.ToList();

        DateTime endDate = _currentWeekStart.AddDays(6);
        var mealsThisWeek = await _database.GetScheduledMealsAsync(_currentWeekStart, endDate);

        foreach (var day in currentDays)
        {
            // Create a fresh collection for the day's groups
            var newGroups = new ObservableCollection<MealGroup>();

            // 1. Fetch custom meal categories for the specific day
            var customCategories = await _database.GetMealCategoriesAsync(day.Date.Date);

            if (customCategories.Any())
            {
                foreach (var cat in customCategories)
                    newGroups.Add(new MealGroup { GroupName = cat.GroupName, Date = day.Date.Date });
            }
            else
            {
                // Fallback to default categories
                newGroups.Add(new MealGroup { GroupName = "בוקר", Date = day.Date.Date });
                newGroups.Add(new MealGroup { GroupName = "צהריים", Date = day.Date.Date });
                newGroups.Add(new MealGroup { GroupName = "ערב", Date = day.Date.Date });
            }

            // 2. Populate the groups with the scheduled recipes
            var mealsForThisDay = mealsThisWeek.Where(m => m.Date.Date == day.Date.Date).ToList();
            foreach (var scheduledMeal in mealsForThisDay)
            {
                var fullRecipe = await _database.GetRecipeAsync(scheduledMeal.RecipeId);

                if (fullRecipe != null)
                {
                    var targetGroup = newGroups.FirstOrDefault(g => g.GroupName == scheduledMeal.MealType);

                    if (targetGroup == null)
                    {
                        targetGroup = new MealGroup { GroupName = scheduledMeal.MealType, Date = day.Date };
                        newGroups.Add(targetGroup);
                    }

                    targetGroup.Meals.Add(new ScheduledMealDisplay
                    {
                        MealRecord = scheduledMeal,
                        RecipeDetails = fullRecipe
                    });
                }
            }

            // Apply the new groups to the UI safely
            day.MealGroups = newGroups;
        }

        HandleTargetMeal();
    }

    #endregion
    //--------------

    //--------------
    #region Commands
    //--------------

    [RelayCommand]
    public void NextWeek()
    {
        DateTime nextTarget = SelectedDay != null ? SelectedDay.Date.AddDays(7) : _currentWeekStart.AddDays(7);
        SelectedDatePickerDate = nextTarget;
    }

    [RelayCommand]
    public void PreviousWeek()
    {
        DateTime prevTarget = SelectedDay != null ? SelectedDay.Date.AddDays(-7) : _currentWeekStart.AddDays(-7);
        SelectedDatePickerDate = prevTarget;
    }

    [RelayCommand]
    public async Task RemoveRecipeAsync(ScheduledMealDisplay mealToRemove)
    {
        if (mealToRemove == null) return;

        bool isConfirmed = await Shell.Current.DisplayAlert("הסרת מתכון", $"האם להסיר את '{mealToRemove.RecipeDetails.Title}' מהלו\"ז?", "כן, הסר", "ביטול");

        if (!isConfirmed) return;

        await _database.DeleteScheduledMealAsync(mealToRemove.MealRecord);

        foreach (var group in SelectedDay.MealGroups)
        {
            if (group.Meals.Contains(mealToRemove))
            {
                group.Meals.Remove(mealToRemove);
                break;
            }
        }
    }

    [RelayCommand]
    public async Task AddRecipeToMealAsync(MealGroup targetGroup)
    {
        if (targetGroup == null) return;

        var navigationParameter = new Dictionary<string, object>
        {
            { "SelectedDate", targetGroup.Date },
            { "MealType", targetGroup.GroupName }
        };

        await Shell.Current.GoToAsync("SelectRecipePage", navigationParameter);
    }

    [RelayCommand]
    public async Task ViewRecipeAsync(Recipe selectedRecipe)
    {
        if (selectedRecipe == null) return;

        var navigationParameter = new Dictionary<string, object>
        {
            { "Recipe", selectedRecipe }
        };

        await Shell.Current.GoToAsync("RecipeViewerPage", navigationParameter);
    }

    [RelayCommand]
    public async Task EditDayMealsAsync()
    {
        if (SelectedDay == null) return;

        var navigationParameter = new Dictionary<string, object>
        {
            { "TargetDay", SelectedDay }
        };

        await Shell.Current.GoToAsync("ManageMealsPage", navigationParameter);
    }

    [RelayCommand]
    public void ToggleMealGroup(MealGroup group)
    {
        if (group != null)
        {
            group.IsExpanded = !group.IsExpanded;
        }
    }

    [RelayCommand]
    public void ToggleAllMeals()
    {
        if (SelectedDay == null || !SelectedDay.MealGroups.Any()) return;

        bool anyExpanded = SelectedDay.MealGroups.Any(m => m.IsExpanded);

        foreach (var group in SelectedDay.MealGroups)
        {
            group.IsExpanded = !anyExpanded;
        }
    }

    #endregion
    //--------------
}