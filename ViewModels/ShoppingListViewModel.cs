using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Recipe_book.Models.Recipes;
using Recipe_book.Services;
using Recipe_book.Helpers;
using Recipe_book.Models.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Recipe_book.Models.Shopping;
using System.Text.Json;

namespace Recipe_book.ViewModels;

public partial class ShoppingListViewModel : ObservableObject
{
    private readonly RecipesDatabase _database;

    //--------------
    #region Properties
    //--------------

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

    public bool IsHeaderClosed => !IsHeaderOpen;

    public ObservableCollection<ShoppingItemGroup> GroupedShoppingItems { get; } = new();

    // Properties for the Multi-Select Merge Overlay
    [ObservableProperty]
    private bool isMergeModeActive = false;

    public ObservableCollection<SelectableListDto> MergeableLists { get; } = new();

    #endregion
    //--------------

    public ShoppingListViewModel(RecipesDatabase database)
    {
        _database = database;
    }

    //--------------
    #region List Management Logic
    //--------------

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

        // Load configuration tied to the specific list ID to maintain independent schedules
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

            // Refresh main UI if the active list was renamed
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
            IsStatic = false, // Indicates the list is dynamic and depends on the user's date range
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(7),
            CreatedAt = DateTime.Now
        };

        await _database.SaveShoppingListAsync(newList);
        await LoadAllListsAsync();

        // Automatically focus on the newly created list
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

        IsMergeModeActive = false; // Hide the overlay UI

        // 1. Initialize the new aggregated static list
        var newList = new SavedShoppingList
        {
            Title = newListName,
            IsStatic = true, // Merged lists become static snapshots
            CreatedAt = DateTime.Now
        };

        await _database.SaveShoppingListAsync(newList);

        // 2. Fetch and aggregate elements across all selected lists
        var aggregatedItems = new Dictionary<string, SavedShoppingListItem>();

        foreach (var list in selectedLists)
        {
            var items = await _database.GetItemsForShoppingListAsync(list.Id);
            foreach (var item in items)
            {
                // Composite key ensures accurate quantity incrementation based on matching parameters
                string key = $"{item.Name}_{item.Unit}_{item.Category}";

                if (aggregatedItems.ContainsKey(key))
                {
                    aggregatedItems[key].Quantity += item.Quantity;

                    // Re-calculate display format after aggregation
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

        // 3. Persist the aggregated data models
        await _database.SyncShoppingListItemsAsync(newList.Id, aggregatedItems.Values.ToList());

        // 4. Update the UI state
        await LoadAllListsAsync();
        await SwitchListAsync(newList);
        await Application.Current.MainPage.DisplayAlert("הצלחה! 🎉", "הרשימות מוזגו בהצלחה לרשימה אחת מאוחדת.", "מעולה");
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
                var fallbackList = SavedLists.FirstOrDefault(l => !l.IsStatic);
                await SwitchListAsync(fallbackList);
            }
        }
    }

    [RelayCommand]
    public async Task ImportFromClipboardAsync()
    {
        try
        {
            // 1. Ensure payload exists in the system clipboard
            if (!Clipboard.Default.HasText)
            {
                await Application.Current.MainPage.DisplayAlert("שגיאה", "אין טקסט מועתק בלוח.", "אישור");
                return;
            }

            string clipboardText = await Clipboard.Default.GetTextAsync();
            if (string.IsNullOrWhiteSpace(clipboardText)) return;

            // 2. Identify the application-specific URI scheme payload boundary
            string searchKey = "recipebook://sharelist?data=";
            int startIndex = clipboardText.IndexOf(searchKey);

            if (startIndex == -1)
            {
                await Application.Current.MainPage.DisplayAlert("לא נמצא קישור", "לא זיהינו קישור של רשימת קניות בטקסט שהעתקת. ודא שהעתקת את ההודעה המלאה מוואטסאפ.", "אישור");
                return;
            }

            // 3. Extract and sanitize the Base64 payload
            string base64Data = clipboardText.Substring(startIndex + searchKey.Length).Trim();
            base64Data = base64Data.Split('\n', '\r', ' ')[0];

            // Normalize Base64 padding structure
            int mod4 = base64Data.Length % 4;
            if (mod4 > 0) base64Data += new string('=', 4 - mod4);

            // Decode serialized payload
            string decodedData = Uri.UnescapeDataString(base64Data);
            byte[] bytes = Convert.FromBase64String(decodedData);
            string json = Encoding.UTF8.GetString(bytes);

            var sharedDto = JsonSerializer.Deserialize<SharedListDto>(json);

            if (sharedDto != null)
            {
                // 4. Register the imported list as a static data set
                var newList = new SavedShoppingList
                {
                    Title = sharedDto.T + " (מיובא)",
                    IsStatic = true,
                    CreatedAt = DateTime.Now
                };

                await _database.SaveShoppingListAsync(newList);

                // 5. Transform DTOs back into native entity models
                var flatList = new List<SavedShoppingListItem>();
                foreach (var item in sharedDto.I)
                {
                    string displayUnit = item.U == "יחידות" ? "" : item.U;
                    string displayTxt = string.IsNullOrWhiteSpace(displayUnit) ? $"{item.Q} {item.N}" : $"{item.Q} {displayUnit} {item.N}";

                    flatList.Add(new SavedShoppingListItem
                    {
                        ListId = newList.Id,
                        Name = item.N,
                        Quantity = item.Q,
                        Unit = displayUnit,
                        Category = item.C,
                        DisplayText = displayTxt,
                        IsBought = false
                    });
                }

                await _database.SyncShoppingListItemsAsync(newList.Id, flatList);

                // 6. Refresh interface context
                await LoadAllListsAsync();
                await SwitchListAsync(newList);

                await Application.Current.MainPage.DisplayAlert("הצלחה! 🎉", "הרשימה יובאה בהצלחה והיא פתוחה עכשיו.", "מעולה");
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("שגיאה בייבוא", "הטקסט שהעתקת אינו תקין או שהקישור פגום.", "אישור");
        }
    }

    #endregion
    //--------------

    //--------------
    #region Navigation & Date Logic
    //--------------

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

        if (CurrentShoppingList == null)
        {
            CurrentShoppingList = SavedLists.FirstOrDefault(l => !l.IsStatic)
                                  ?? new SavedShoppingList { Title = "קניות השבוע", IsStatic = false, CreatedAt = DateTime.Now };
            if (CurrentShoppingList.Id == 0)
            {
                await _database.SaveShoppingListAsync(CurrentShoppingList);
                await LoadAllListsAsync();
            }
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
            StatusText = $"📌 רשימה סטטית (אינה מתעדכנת מזמנים)";
            return;
        }

        string formattedStart = StartDate.ToString("dd.MM");
        string formattedEnd = EndDate.ToString("dd.MM");

        switch (SelectedRangeType)
        {
            case DateRangeType.Day:
                StatusText = $"💡 עדכנית להיום ({formattedStart})";
                break;
            case DateRangeType.Week:
                StatusText = $"💡 עדכנית לשבוע הקרוב ({formattedStart} - {formattedEnd})";
                break;
            case DateRangeType.TwoWeeks:
                StatusText = $"💡 עדכנית לשבועיים הקרובים ({formattedStart} - {formattedEnd})";
                break;
            case DateRangeType.Month:
                StatusText = $"💡 עדכנית לחודש הקרוב ({formattedStart} - {formattedEnd})";
                break;
            case DateRangeType.NextWeek:
                StatusText = $"💡 עדכנית לשבוע הבא ({formattedStart} - {formattedEnd})";
                break;
            case DateRangeType.CustomRolling:
                StatusText = $"💡 עדכנית ל-{CustomDurationDays} ימים ({formattedStart} - {formattedEnd})";
                break;
            case DateRangeType.SpecificDates:
                StatusText = $"💡 רשימה לתאריכים ({formattedStart} - {formattedEnd})";
                break;
        }
    }

    #endregion
    //--------------

    //--------------
    #region Core Generation Logic
    //--------------

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
    //--------------

    //--------------
    #region UI & Sharing
    //--------------

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
            sb.AppendLine($"🛒 *{CurrentShoppingList?.Title ?? "רשימת קניות"}:*");
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
            var sharedDto = new SharedListDto
            {
                T = CurrentShoppingList?.Title ?? "רשימה משותפת"
            };

            foreach (var group in GroupedShoppingItems)
            {
                foreach (var item in group)
                {
                    sharedDto.I.Add(new SharedItemDto
                    {
                        N = item.Name,
                        Q = item.Quantity,
                        U = item.Unit,
                        C = item.Category
                    });
                }
            }

            // Serialize and encode payload
            string json = JsonSerializer.Serialize(sharedDto);
            string base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

            // Generate custom scheme URI link
            string deepLink = $"recipebook://sharelist?data={Uri.EscapeDataString(base64Data)}";

            var sbLink = new StringBuilder();
            sbLink.AppendLine($"🛒 *{CurrentShoppingList?.Title ?? "רשימת קניות"}*");
            sbLink.AppendLine("שלחתי לך רשימת קניות לאפליקציה!");
            sbLink.AppendLine("📌 *איך לייבא?* פשוט העתק את כל ההודעה הזו, כנס לאפליקציה ולחץ על כפתור הייבוא.");
            sbLink.AppendLine();
            sbLink.AppendLine(deepLink);

            textToShare = sbLink.ToString();
        }

        // Invoke native OS share functionality
        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Text = textToShare,
            Title = "שיתוף רשימת קניות"
        });
    }
    #endregion
    //--------------
}

// -------------------------------------------------------------------------
// Support classes placed outside the main ViewModel
// -------------------------------------------------------------------------

// Helper class to bind checkboxes to the merge overlay interface
public partial class SelectableListDto : ObservableObject
{
    public SavedShoppingList List { get; set; }

    [ObservableProperty]
    private bool isSelected;
}

// Data Transfer Objects (DTOs) for Deep Linking payload structuring
public class SharedListDto
{
    public string T { get; set; } // List Title
    public List<SharedItemDto> I { get; set; } = new(); // Aggregated Items
}

public class SharedItemDto
{
    public string N { get; set; } // Item Name
    public double Q { get; set; } // Computed Quantity
    public string U { get; set; } // Metric Unit
    public string C { get; set; } // General Category
}