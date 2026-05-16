using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Recipe_book.Helpers;
using Recipe_book.Models.Cloud;
using Recipe_book.Models.Enums;
using Recipe_book.Models.Recipes;
using Recipe_book.Models.Shopping;
using Recipe_book.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Recipe_book.ViewModels;

public partial class ShoppingListViewModel : ObservableObject
{
    private readonly RecipesDatabase _database;

    #region Properties

    public static int? PendingImportId { get; set; }
    public static Action RefreshActivePage;

    [ObservableProperty]
    private string emptyViewText = "אין מצרכים מתוכננים בטווח הנבחר";

    [ObservableProperty]
    private SavedShoppingList currentShoppingList;

    public ObservableCollection<SavedShoppingList> SavedLists { get; } = new();

    [ObservableProperty]
    private bool isListsMenuOpen = false;

    [ObservableProperty]
    private DateTime startDate = DateTime.Today;

    [ObservableProperty]
    private DateTime endDate = DateTime.Today.AddDays(7);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHeaderClosed))]
    private bool isHeaderOpen = false;

    [ObservableProperty]
    private bool isLoading = false;

    [ObservableProperty]
    private string loadingText = String.Empty;

    private string defaultLoadingText = "טוען, נא להמתין";

    public bool IsHeaderClosed => !IsHeaderOpen;

    public ObservableCollection<ShoppingItemGroup> GroupedShoppingItems { get; } = new();

    [ObservableProperty]
    private bool isMergeModeActive = false;

    public ObservableCollection<SelectableListDto> MergeableLists { get; } = new();

    [ObservableProperty]
    private bool hasValidList = true;

    private int? importedListId;
    public int? ImportedListId
    {
        get => importedListId;
        set
        {
            importedListId = value;
            if (value.HasValue)
            {
                MainThread.BeginInvokeOnMainThread(async () => await HandleImportedListAsync(value.Value));
            }
        }
    }

    #endregion


    public ShoppingListViewModel(RecipesDatabase database)
    {
        _database = database;

        RefreshActivePage = () =>
        {
            MainThread.BeginInvokeOnMainThread(async () => await InitializeAutoLoadAsync());
        };

        // Added "RefreshRecipes" to catch edits from the RecipeEditor
        WeakReferenceMessenger.Default.Register<string>(this, async (r, m) =>
        {
            if (m == "ScheduleChanged" || m == "RecipesChanged" || m == "RefreshRecipes")
            {
                await InitializeAutoLoadAsync();
            }
        });
    }

    private async Task HandleImportedListAsync(int id)
    {
        await LoadAllListsAsync();
        var list = SavedLists.FirstOrDefault(l => l.Id == id);
        if (list != null)
        {
            await SwitchListAsync(list);
        }
        importedListId = null;
    }

    #region List Management Logic

    public async Task LoadAllListsAsync()
    {
        var lists = await _database.GetSavedShoppingListsAsync();
        SavedLists.Clear();
        foreach (var list in lists)
        {
            SavedLists.Add(list);
        }
    }

    [RelayCommand]
    public void ToggleListsMenu()
    {
        IsListsMenuOpen = !IsListsMenuOpen;
        IsHeaderOpen = false;
    }

    [RelayCommand]
    public async Task SwitchListAsync(SavedShoppingList selectedList)
    {
        if (selectedList == null) return;

        CurrentShoppingList = selectedList;
        IsListsMenuOpen = false;
        HasValidList = true;

        LoadPreferences();
        UpdateVisibility();
        CalculateActualDates();
        UpdateStatusText();

        await GenerateListAsync();
    }

    [RelayCommand]
    public async Task RenameListAsync(SavedShoppingList listToRename)
    {
        if (listToRename == null) return;

        string newName = await Application.Current.MainPage.DisplayPromptAsync(
            "שינוי שם רשימה",
            "הזן שם חדש לרשימה:",
            "שמור",
            "ביטול",
            listToRename.Title,
            maxLength: 40,
            keyboard: Keyboard.Text);

        if (!string.IsNullOrWhiteSpace(newName) && newName != listToRename.Title)
        {
            listToRename.Title = newName.Trim();

            await _database.SaveShoppingListAsync(listToRename);

            if (CurrentShoppingList?.Id == listToRename.Id)
            {
                OnPropertyChanged(nameof(CurrentShoppingList));
            }

            await LoadAllListsAsync();
        }
    }

    [RelayCommand]
    public async Task CreateNewListAsync()
    {
        string listName = await Application.Current.MainPage.DisplayPromptAsync(
            "רשימה חדשה", "איך תרצה לקרוא לרשימה?", "צור", "ביטול");

        if (string.IsNullOrWhiteSpace(listName)) return;

        var newList = new SavedShoppingList
        {
            Title = listName,
            IsStatic = false,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(7),
            CreatedAt = DateTime.Now
        };

        await _database.SaveShoppingListAsync(newList);
        await LoadAllListsAsync();

        await SwitchListAsync(newList);
    }

    [RelayCommand]
    public void OpenMergeMode()
    {
        MergeableLists.Clear();
        foreach (var list in SavedLists)
        {
            MergeableLists.Add(new SelectableListDto { List = list, IsSelected = false });
        }

        IsListsMenuOpen = false;
        IsMergeModeActive = true;
    }

    [RelayCommand]
    public void CancelMerge()
    {
        IsMergeModeActive = false;
    }

    [RelayCommand]
    public async Task ConfirmMergeAsync()
    {
        var selectedLists = MergeableLists.Where(l => l.IsSelected).Select(l => l.List).ToList();

        if (selectedLists.Count < 2)
        {
            await Application.Current.MainPage.DisplayAlert("שגיאה", "יש לבחור לפחות 2 רשימות למיזוג.", "אישור");
            return;
        }

        string newListName = await Application.Current.MainPage.DisplayPromptAsync(
            "רשימה ממוזגת", "איך לקרוא לרשימה הממוזגת החדשה?", "המשך", "ביטול");

        if (string.IsNullOrWhiteSpace(newListName)) return;

        IsMergeModeActive = false;

        var newList = new SavedShoppingList
        {
            Title = newListName,
            IsStatic = true,
            CreatedAt = DateTime.Now
        };

        await _database.SaveShoppingListAsync(newList);

        var aggregatedItems = new Dictionary<string, SavedShoppingListItem>();

        foreach (var list in selectedLists)
        {
            var items = await _database.GetItemsForShoppingListAsync(list.Id);
            foreach (var item in items)
            {
                string key = $"{item.Name}_{item.Unit}_{item.Category}";

                if (aggregatedItems.ContainsKey(key))
                {
                    aggregatedItems[key].Quantity += item.Quantity;

                    string displayUnit = item.Unit == "יחידות" ? "" : item.Unit;
                    aggregatedItems[key].DisplayText = string.IsNullOrWhiteSpace(displayUnit) ?
                        $"{aggregatedItems[key].Quantity} {item.Name}" :
                        $"{aggregatedItems[key].Quantity} {displayUnit} {item.Name}";
                }
                else
                {
                    aggregatedItems[key] = new SavedShoppingListItem
                    {
                        ListId = newList.Id,
                        Name = item.Name,
                        Quantity = item.Quantity,
                        Unit = item.Unit,
                        Category = item.Category,
                        DisplayText = item.DisplayText,
                        IsBought = false
                    };
                }
            }
        }

        await _database.SyncShoppingListItemsAsync(newList.Id, aggregatedItems.Values.ToList());

        await LoadAllListsAsync();
        await SwitchListAsync(newList);
        WeakReferenceMessenger.Default.Send("ShoppingChanged");
        await Application.Current.MainPage.DisplayAlert("הצלחה!", "הרשימות מוזגו בהצלחה לרשימה אחת מאוחדת.", "מעולה");
    }

    [RelayCommand]
    public async Task DeleteListAsync(SavedShoppingList listToDelete)
    {
        if (listToDelete == null) return;

        bool confirm = await Application.Current.MainPage.DisplayAlert(
            "מחיקת רשימה", $"האם אתה בטוח שברצונך למחוק את '{listToDelete.Title}'?", "כן, מחק", "ביטול");

        if (confirm)
        {
            await _database.DeleteShoppingListAsync(listToDelete);
            await LoadAllListsAsync();

            if (CurrentShoppingList?.Id == listToDelete.Id)
            {
                if (SavedLists.Any())
                {
                    var fallbackList = SavedLists.FirstOrDefault(l => !l.IsStatic) ?? SavedLists.First();
                    await SwitchListAsync(fallbackList);
                }
                else
                {
                    CurrentShoppingList = new SavedShoppingList { Title = "אין רשימות כרגע", Id = -1, IsStatic = true };
                    HasValidList = false;
                    EmptyViewText = "";
                    GroupedShoppingItems.Clear();
                    StatusText = "לא קיימות רשימות. לחץ על התפריט ליצירת רשימה חדשה.";
                }
            }
        }
    }

    #endregion

    #region Navigation & Date Logic

    [ObservableProperty] private string statusText;
    [ObservableProperty] private DateRangeType selectedRangeType = DateRangeType.Week;
    [ObservableProperty] private bool isCustomRollingVisible;
    [ObservableProperty] private bool isSpecificDatesVisible;

    [ObservableProperty] private int customOffsetDays = 0;
    [ObservableProperty] private int customDurationDays = 7;

    [ObservableProperty] private DateTime specificStartDate = DateTime.Today;
    [ObservableProperty] private DateTime specificEndDate = DateTime.Today.AddDays(7);

    public async Task InitializeAutoLoadAsync()
    {
        await LoadAllListsAsync();

        if (SavedLists.Count == 0)
        {
            CurrentShoppingList = new SavedShoppingList { Title = "אין רשימות כרגע", Id = -1, IsStatic = true };
            HasValidList = false;
            EmptyViewText = "";
            GroupedShoppingItems.Clear();
            StatusText = "לא קיימות רשימות. לחץ על התפריט ליצירת רשימה חדשה.";
            return;
        }

        HasValidList = true;
        EmptyViewText = "אין מצרכים מתוכננים בטווח הנבחר";

        if (PendingImportId.HasValue)
        {
            var targetList = SavedLists.FirstOrDefault(l => l.Id == PendingImportId.Value);
            PendingImportId = null;
            if (targetList != null)
            {
                await SwitchListAsync(targetList);
                return;
            }
        }

        if (CurrentShoppingList == null || CurrentShoppingList.Id == -1)
        {
            CurrentShoppingList = SavedLists.FirstOrDefault(l => !l.IsStatic) ?? SavedLists.First();
        }

        LoadPreferences();
        UpdateVisibility();
        CalculateActualDates();
        UpdateStatusText();
        await GenerateListAsync();
    }

    private void LoadPreferences()
    {
        string listIdStr = CurrentShoppingList?.Id.ToString() ?? "0";

        string savedRangeStr = Preferences.Default.Get($"ShoppingListRange_{listIdStr}", nameof(DateRangeType.Week));
        if (Enum.TryParse(typeof(DateRangeType), savedRangeStr, out var parsedRange))
            SelectedRangeType = (DateRangeType)parsedRange;

        CustomOffsetDays = Preferences.Default.Get($"ShoppingListOffset_{listIdStr}", 0);
        CustomDurationDays = Preferences.Default.Get($"ShoppingListDuration_{listIdStr}", 7);
        SpecificStartDate = Preferences.Default.Get($"ShoppingListSpecStart_{listIdStr}", DateTime.Today);
        SpecificEndDate = Preferences.Default.Get($"ShoppingListSpecEnd_{listIdStr}", DateTime.Today.AddDays(7));
    }

    private void SavePreferences()
    {
        string listIdStr = CurrentShoppingList?.Id.ToString() ?? "0";

        Preferences.Default.Set($"ShoppingListRange_{listIdStr}", SelectedRangeType.ToString());
        Preferences.Default.Set($"ShoppingListOffset_{listIdStr}", CustomOffsetDays);
        Preferences.Default.Set($"ShoppingListDuration_{listIdStr}", CustomDurationDays);
        Preferences.Default.Set($"ShoppingListSpecStart_{listIdStr}", SpecificStartDate);
        Preferences.Default.Set($"ShoppingListSpecEnd_{listIdStr}", SpecificEndDate);
    }

    [RelayCommand]
    public void SelectRangeType(string rangeTypeStr)
    {
        if (Enum.TryParse(typeof(DateRangeType), rangeTypeStr, out var parsedRange))
        {
            SelectedRangeType = (DateRangeType)parsedRange;
            UpdateVisibility();
        }
    }

    private void UpdateVisibility()
    {
        IsCustomRollingVisible = SelectedRangeType == DateRangeType.CustomRolling;
        IsSpecificDatesVisible = SelectedRangeType == DateRangeType.SpecificDates;
    }

    [RelayCommand]
    public void OpenHeader()
    {
        if (CurrentShoppingList != null && CurrentShoppingList.IsStatic)
        {
            Application.Current.MainPage.DisplayAlert("רשימה סגורה", "זוהי רשימה שהתקבלה ממישהו אחר ולכן אינה מתעדכנת לפי תאריכים.", "אישור");
            return;
        }

        IsListsMenuOpen = false;
        LoadPreferences();
        UpdateVisibility();
        IsHeaderOpen = true;
    }

    [RelayCommand]
    public void CancelSelection()
    {
        IsHeaderOpen = false;
        LoadPreferences();
        UpdateVisibility();
    }

    [RelayCommand]
    public async Task ApplyAndCloseAsync()
    {
        if (SelectedRangeType == DateRangeType.SpecificDates && SpecificStartDate > SpecificEndDate)
        {
            await Application.Current.MainPage.DisplayAlert("שגיאה", "תאריך ההתחלה חייב להיות לפני תאריך הסיום.", "אישור");
            return;
        }

        SavePreferences();
        CalculateActualDates();
        UpdateStatusText();

        IsHeaderOpen = false;
        await GenerateListAsync();
    }

    private void CalculateActualDates()
    {
        switch (SelectedRangeType)
        {
            case DateRangeType.Day:
                StartDate = DateTime.Today;
                EndDate = DateTime.Today;
                break;
            case DateRangeType.Week:
                StartDate = DateTime.Today;
                EndDate = DateTime.Today.AddDays(7);
                break;
            case DateRangeType.TwoWeeks:
                StartDate = DateTime.Today;
                EndDate = DateTime.Today.AddDays(14);
                break;
            case DateRangeType.Month:
                StartDate = DateTime.Today;
                EndDate = DateTime.Today.AddDays(30);
                break;
            case DateRangeType.NextWeek:
                StartDate = DateTime.Today.AddDays(7);
                EndDate = DateTime.Today.AddDays(14);
                break;
            case DateRangeType.CustomRolling:
                StartDate = DateTime.Today.AddDays(CustomOffsetDays);
                EndDate = StartDate.AddDays(CustomDurationDays);
                break;
            case DateRangeType.SpecificDates:
                StartDate = SpecificStartDate.Date;
                EndDate = SpecificEndDate.Date;
                break;
        }
    }

    private void UpdateStatusText()
    {
        if (CurrentShoppingList != null && CurrentShoppingList.IsStatic)
        {
            StatusText = $"רשימה סטטית (אינה מתעדכנת מזמנים)";
            return;
        }

        string formattedStart = StartDate.ToString("dd.MM");
        string formattedEnd = EndDate.ToString("dd.MM");

        switch (SelectedRangeType)
        {
            case DateRangeType.Day:
                StatusText = $"להיום ({formattedStart})";
                break;
            case DateRangeType.Week:
                StatusText = $"לשבוע הקרוב ({formattedStart} - {formattedEnd})";
                break;
            case DateRangeType.TwoWeeks:
                StatusText = $"לשבועיים הקרובים ({formattedStart} - {formattedEnd})";
                break;
            case DateRangeType.Month:
                StatusText = $"לחודש הקרוב ({formattedStart} - {formattedEnd})";
                break;
            case DateRangeType.NextWeek:
                StatusText = $"לשבוע הבא ({formattedStart} - {formattedEnd})";
                break;
            case DateRangeType.CustomRolling:
                StatusText = $"ל-{CustomDurationDays} ימים ({formattedStart} - {formattedEnd})";
                break;
            case DateRangeType.SpecificDates:
                StatusText = $"לתאריכים ({formattedStart} - {formattedEnd})";
                break;
        }
    }

    #endregion

    #region Core Generation Logic

    [RelayCommand]
    public async Task GenerateListAsync()
    {
        GroupedShoppingItems.Clear();

        if (CurrentShoppingList != null && CurrentShoppingList.IsStatic)
        {
            await LoadStaticListItemsAsync();
            return;
        }

        var mealsInRange = await _database.GetScheduledMealsAsync(StartDate.Date, EndDate.Date);
        var conversions = await _database.GetIngredientConversionsAsync();
        conversions = conversions.OrderByDescending(c => c.Keyword.Length).ToList();

        var aggregatedIngredients = await ProcessAndAggregateIngredientsAsync(mealsInRange, conversions);

        await BuildAndGroupShoppingList(aggregatedIngredients);
    }

    private async Task LoadStaticListItemsAsync()
    {
        var items = await _database.GetItemsForShoppingListAsync(CurrentShoppingList.Id);

        var grouped = items.GroupBy(x => x.Category)
                           .Select(g => new ShoppingItemGroup(g.Key, g))
                           .OrderBy(g => g.CategoryName);

        foreach (var group in grouped)
        {
            foreach (var item in group)
            {
                item.PropertyChanged += async (s, e) =>
                {
                    if (e.PropertyName == nameof(SavedShoppingListItem.IsBought))
                    {
                        await _database.SaveShoppingListItemAsync(item);
                    }
                };
            }
            GroupedShoppingItems.Add(group);
        }
    }

    private async Task<Dictionary<string, (double Quantity, string Category)>> ProcessAndAggregateIngredientsAsync(
        List<ScheduledMeal> meals, List<IngredientConversion> conversions)
    {
        var aggregatedIngredients = new Dictionary<string, (double Quantity, string Category)>();

        foreach (var meal in meals)
        {
            var recipe = await _database.GetRecipeAsync(meal.RecipeId);
            if (recipe == null) continue;

            var recipeIngredients = await _database.GetIngredientsAsync(meal.RecipeId);
            if (recipeIngredients == null || !recipeIngredients.Any()) continue;

            foreach (var ingredient in recipeIngredients)
            {
                if (string.IsNullOrWhiteSpace(ingredient.Name) || ingredient.Quantity == null) continue;

                await ProcessSingleIngredientAsync(recipe, ingredient, conversions, aggregatedIngredients);
            }
        }

        return aggregatedIngredients;
    }

    private async Task ProcessSingleIngredientAsync(Recipe recipe, Ingredient ingredient, List<IngredientConversion> conversions, Dictionary<string, (double Quantity, string Category)> aggregatedIngredients)
    {
        double quantity = ingredient.Quantity.Value;
        string unit = ingredient.Unit?.Trim() ?? "יחידות";
        string name = ingredient.Name.Trim();

        if (unit == "ק״ג") { quantity *= 1000; unit = "גרם"; }
        else if (unit == "ליטר") { quantity *= 1000; unit = "מ״ל"; }

        var conversion = await FindOrSuggestConversionAsync(recipe, ingredient, name, conversions);

        name = ingredient.Name.Trim();
        bool isConvertibleVolume = unit == "כוס" || unit == "כף" || unit == "כפית";

        if (conversion == null)
        {
            conversion = await LearnNewIngredientAsync(recipe, ingredient, name, unit, isConvertibleVolume, conversions);
            if (conversion == null) return;
            name = conversion.Keyword;
        }

        AggregateFinalIngredient(quantity, unit, name, conversion, aggregatedIngredients);
    }

    private async Task<IngredientConversion> FindOrSuggestConversionAsync(Recipe recipe, Ingredient ingredient, string originalName, List<IngredientConversion> conversions)
    {
        var possibleNames = TextHelpers.GetPossibleSingulars(originalName);

        var conversion = conversions.FirstOrDefault(c => possibleNames.Any(p => c.Keyword == p || originalName.Contains(c.Keyword) || p.Contains(c.Keyword)));

        if (conversion == null)
        {
            int maxAllowedDistance = originalName.Length <= 4 ? 1 : 2;

            var suggestions = conversions
                .Where(c => possibleNames.Any(p =>
                    c.Keyword.Contains(p) || p.Contains(c.Keyword) ||
                    TextHelpers.ComputeLevenshteinDistance(p, c.Keyword) <= maxAllowedDistance))
                .OrderBy(c => TextHelpers.ComputeLevenshteinDistance(originalName, c.Keyword))
                .Select(c => c.Keyword)
                .Distinct()
                .Take(4)
                .ToList();

            if (suggestions.Any())
            {
                suggestions.Add("משהו אחר (צור חדש)");

                string selectedOption = await Application.Current.MainPage.DisplayActionSheet(
                    $"במתכון '{recipe.Title}' כתבת '{originalName}'. למה התכוונת?", "דלג", null, suggestions.ToArray());

                if (selectedOption != "דלג" && !string.IsNullOrEmpty(selectedOption) && selectedOption != "משהו אחר (צור חדש)")
                {
                    conversion = conversions.First(c => c.Keyword == selectedOption);
                    await ApplyCorrectionAsync(ingredient, originalName, selectedOption);
                }
            }
        }
        return conversion;
    }

    private async Task ApplyCorrectionAsync(Ingredient currentIngredient, string oldName, string newName)
    {
        string choice = await Application.Current.MainPage.DisplayActionSheet(
            $"לשנות את '{oldName}' ל-'{newName}' רק כאן או בכל המתכונים?",
            "רק במתכון הזה", null, "בכל המתכונים שלי");

        if (choice == "בכל המתכונים שלי")
        {
            var allIngredients = await _database.GetIngredientsByNameAsync(oldName);

            foreach (var ing in allIngredients)
            {
                ing.Name = newName;
                await _database.SaveIngredientAsync(ing);
            }
            currentIngredient.Name = newName;
        }
        else
        {
            currentIngredient.Name = newName;
            await _database.SaveIngredientAsync(currentIngredient);
        }
    }

    private async Task<IngredientConversion> LearnNewIngredientAsync(Recipe recipe, Ingredient ingredient, string currentName, string unit, bool isConvertibleVolume, List<IngredientConversion> conversions)
    {
        string finalName = await Application.Current.MainPage.DisplayPromptAsync(
            "מצרך חדש", $"במתכון '{recipe.Title}' כתבת '{currentName}'. איך תרצה לקרוא לזה במערכת?", initialValue: currentName);

        if (string.IsNullOrWhiteSpace(finalName)) return null;

        if (finalName != currentName)
        {
            await ApplyCorrectionAsync(ingredient, currentName, finalName);
        }

        string selectedCategory = await Application.Current.MainPage.DisplayActionSheet(
            $"לאיזו מחלקה שייך '{finalName}'?",
            "דלג", null,
            "ירקות ופירות", "בשרים ודגים", "מוצרי אפייה בסיסיים", "קטניות", "פחמימות יבשות ודגנים",
            "תבלינים ועשבי תיבול", "ממרחים ומתוקים", "שומנים ושמנים", "מוצרי חלב, ביצים ותחליפים",
            "סוכרים וממתיקים", "אגוזים וזרעים", "רוטבים ומרינדות", "משקאות ומיצים", "קפואים", "שימורים");

        if (selectedCategory == "דלג" || string.IsNullOrEmpty(selectedCategory)) return null;

        double gramsPerCup = 0;
        bool skippedVolume = false;

        if (isConvertibleVolume)
        {
            string result = await Application.Current.MainPage.DisplayPromptAsync(
                "משקל המצרך", $"כמה גרם שוקלת כוס אחת של '{finalName}'?", "שמור ולמד", "דלג", keyboard: Keyboard.Numeric);

            if (string.IsNullOrWhiteSpace(result)) skippedVolume = true;
            else
            {
                double.TryParse(result, out gramsPerCup);
                if (gramsPerCup <= 0) skippedVolume = true;
            }
        }

        string savedBaseUnit = "יחידות";
        if (isConvertibleVolume && !skippedVolume) savedBaseUnit = "גרם";
        else if (unit == "גרם" || unit == "מ״ל") savedBaseUnit = unit;

        var newConversion = new IngredientConversion
        {
            Keyword = finalName,
            BaseUnit = savedBaseUnit,
            AmountPerCup = gramsPerCup,
            Category = selectedCategory
        };

        await _database.AddIngredientConversionAsync(newConversion);
        conversions.Add(newConversion);

        return newConversion;
    }

    private void AggregateFinalIngredient(double quantity, string unit, string name, IngredientConversion conversion, Dictionary<string, (double Quantity, string Category)> aggregatedIngredients)
    {
        string finalUnit = unit;
        string aggregatedKeyName = name;
        string itemCategory = "כללי";

        if (conversion != null)
        {
            itemCategory = string.IsNullOrWhiteSpace(conversion.Category) ? "כללי" : conversion.Category;

            if (name.Contains("שימורים") || unit.Contains("שימורים")) itemCategory = "שימורים";

            bool isVolumeOrWeight = unit == "כוס" || unit == "כף" || unit == "כפית" || unit == "גרם" || unit == "מ״ל";

            var possibleSingulars = TextHelpers.GetPossibleSingulars(name);
            bool isPluralOfKeyword = possibleSingulars.Contains(conversion.Keyword);

            if (isVolumeOrWeight || isPluralOfKeyword || name == conversion.Keyword)
            {
                aggregatedKeyName = conversion.Keyword;
            }

            if (isVolumeOrWeight)
            {
                if ((unit == "כוס" || unit == "כף" || unit == "כפית") && conversion.AmountPerCup > 0)
                {
                    if (unit == "כוס") quantity *= conversion.AmountPerCup;
                    else if (unit == "כף") quantity *= (conversion.AmountPerCup / 16.0);
                    else if (unit == "כפית") quantity *= (conversion.AmountPerCup / 48.0);

                    finalUnit = conversion.BaseUnit;
                }
                else if (unit == "גרם" || unit == "מ״ל")
                {
                    finalUnit = unit;
                }
            }
        }

        string dictionaryKey = $"{aggregatedKeyName}_{finalUnit}";

        if (aggregatedIngredients.ContainsKey(dictionaryKey))
        {
            var existing = aggregatedIngredients[dictionaryKey];
            aggregatedIngredients[dictionaryKey] = (existing.Quantity + quantity, existing.Category);
        }
        else
        {
            aggregatedIngredients.Add(dictionaryKey, (quantity, itemCategory));
        }
    }

    #endregion

    #region UI & Sharing

    private async Task BuildAndGroupShoppingList(Dictionary<string, (double Quantity, string Category)> aggregatedIngredients)
    {
        CurrentShoppingList.StartDate = StartDate;
        CurrentShoppingList.EndDate = EndDate;
        await _database.SaveShoppingListAsync(CurrentShoppingList);

        var existingItems = await _database.GetItemsForShoppingListAsync(CurrentShoppingList.Id);
        var boughtNames = existingItems.Where(i => i.IsBought).Select(i => i.Name).ToList();

        var flatList = new List<SavedShoppingListItem>();

        foreach (var item in aggregatedIngredients)
        {
            var keyParts = item.Key.Split('_');
            string name = keyParts[0];
            string unit = keyParts[1];
            double finalQuantity = item.Value.Quantity;
            string category = item.Value.Category;

            if (unit == "גרם")
            {
                finalQuantity = Math.Ceiling(finalQuantity / 10.0) * 10.0;
                if (finalQuantity >= 1000) { finalQuantity /= 1000.0; unit = "ק״ג"; }
            }
            else if (unit == "מ״ל")
            {
                finalQuantity = Math.Ceiling(finalQuantity / 10.0) * 10.0;
                if (finalQuantity >= 1000) { finalQuantity /= 1000.0; unit = "ליטר"; }
            }
            else
            {
                finalQuantity = Math.Ceiling(finalQuantity);
            }

            string displayUnit = (unit == "יחידות") ? "" : unit;
            string finalDisplayText = string.IsNullOrWhiteSpace(displayUnit) ? $"{finalQuantity} {name}" : $"{finalQuantity} {displayUnit} {name}";

            bool isAlreadyBought = boughtNames.Contains(name);

            var newItem = new SavedShoppingListItem
            {
                ListId = CurrentShoppingList.Id,
                Name = name,
                DisplayText = finalDisplayText,
                Category = category,
                Quantity = finalQuantity,
                Unit = displayUnit,
                IsBought = isAlreadyBought
            };

            newItem.PropertyChanged += async (s, e) =>
            {
                if (e.PropertyName == nameof(SavedShoppingListItem.IsBought))
                {
                    await _database.SaveShoppingListItemAsync(newItem);
                }
            };

            flatList.Add(newItem);
        }

        await _database.SyncShoppingListItemsAsync(CurrentShoppingList.Id, flatList);

        GroupedShoppingItems.Clear();
        var grouped = flatList.GroupBy(x => x.Category)
                              .Select(g => new ShoppingItemGroup(g.Key, g))
                              .OrderBy(g => g.CategoryName);

        foreach (var group in grouped)
        {
            GroupedShoppingItems.Add(group);
        }
    }

    [RelayCommand]
    public async Task ClearBoughtItemsAsync()
    {
        bool answer = await Application.Current.MainPage.DisplayAlert(
            "איפוס קניות",
            "האם אתה בטוח שברצונך לנקות את כל הסימונים מהרשימה?",
            "כן, נקה הכל",
            "ביטול");

        if (answer)
        {
            foreach (var group in GroupedShoppingItems)
            {
                foreach (var item in group)
                {
                    if (item.IsBought)
                    {
                        item.IsBought = false;
                    }
                }
            }
        }
    }

    [RelayCommand]
    public async Task ShareListAsync()
    {
        if (GroupedShoppingItems == null || !GroupedShoppingItems.Any())
        {
            await Application.Current.MainPage.DisplayAlert("רשימה ריקה", "אין מצרכים ברשימה לשתף.", "אישור");
            return;
        }

        string shareOption = await Application.Current.MainPage.DisplayActionSheet(
            "איך תרצה לשתף את הרשימה?",
            "ביטול",
            null,
            "טקסט רגיל",
            "קישור לאפליקציה");

        if (shareOption == "ביטול" || string.IsNullOrEmpty(shareOption))
        {
            return;
        }

        string textToShare = "";

        if (shareOption == "טקסט רגיל")
        {
            var sb = new StringBuilder();
            sb.AppendLine($"*{CurrentShoppingList?.Title ?? "רשימת קניות"}:*");
            sb.AppendLine();

            foreach (var group in GroupedShoppingItems)
            {
                sb.AppendLine($"--- *{group.CategoryName}* ---");
                foreach (var item in group)
                {
                    string checkIcon = item.IsBought ? "✅" : "🔲";
                    sb.AppendLine($"{checkIcon} {item.DisplayText}");
                }
                sb.AppendLine();
            }
            textToShare = sb.ToString();
        }
        else if (shareOption == "קישור לאפליקציה")
        {
            IsLoading = true;
            LoadingText = "מייצר קישור לשיתוף רשימת הקניות, נא להמתין";
            try
            {
                var sharedDto = new SharedListDto
                {
                    T = CurrentShoppingList?.Title ?? "רשימה משותפת"
                };

                var allItemNames = new HashSet<string>();

                foreach (var group in GroupedShoppingItems)
                {
                    foreach (var item in group)
                    {
                        allItemNames.Add(item.Name);
                        sharedDto.I.Add(new SharedItemDto
                        {
                            N = item.Name,
                            Q = item.Quantity,
                            U = item.Unit,
                            C = item.Category
                        });
                    }
                }

                var allConversions = await _database.GetIngredientConversionsAsync();
                var relevantConversions = allConversions.Where(c => allItemNames.Contains(c.Keyword)).ToList();

                foreach (var conv in relevantConversions)
                {
                    sharedDto.C.Add(new SharedConversionDto
                    {
                        K = conv.Keyword,
                        B = conv.BaseUnit,
                        A = conv.AmountPerCup,
                        C = conv.Category
                    });
                }

                string jsonPayload = JsonSerializer.Serialize(sharedDto);

                var cloudModel = new SharedShoppingListCloudModel
                {
                    ListName = sharedDto.T,
                    PayloadJson = jsonPayload
                };

                var firestoreService = new FirestoreService();
                string newCloudId = await firestoreService.UploadSharedListAsync(cloudModel);

                if (string.IsNullOrEmpty(newCloudId))
                {
                    await Application.Current.MainPage.DisplayAlert("שגיאה", "לא הצלחנו לייצר קישור לענן. אנא בדוק את החיבור לאינטרנט.", "אישור");
                    return;
                }

                await _database.RegisterSharedListForDeletionAsync(newCloudId, cloudModel.ExpiresAt);

                string deepLink = $"https://recipe-book-d9389.web.app/sharelist?id={newCloudId}";

                var sbLink = new StringBuilder();
                sbLink.AppendLine($"*{cloudModel.ListName}*");
                sbLink.AppendLine("שלחתי לך רשימת קניות מרוכזת באפליקציה!");
                sbLink.AppendLine("לחץ על הקישור כדי לייבא אותה (כולל המרות מצרכים):");
                sbLink.AppendLine();
                sbLink.AppendLine(deepLink);

                textToShare = sbLink.ToString();
            }
            finally
            {
                LoadingText = defaultLoadingText;
                IsLoading = false;
            }
        }

        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Text = textToShare,
            Title = "שיתוף רשימת קניות"
        });
    }
    #endregion
}

public partial class SelectableListDto : ObservableObject
{
    public SavedShoppingList List { get; set; }

    [ObservableProperty]
    private bool isSelected;
}

public class SharedListDto
{
    public string T { get; set; }
    public List<SharedItemDto> I { get; set; } = new();
    public List<SharedConversionDto> C { get; set; } = new();
}

public class SharedItemDto
{
    public string N { get; set; }
    public double Q { get; set; }
    public string U { get; set; }
    public string C { get; set; }
}

public class SharedConversionDto
{
    public string K { get; set; }
    public string B { get; set; }
    public double A { get; set; }
    public string C { get; set; }
}