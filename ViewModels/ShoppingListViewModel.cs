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

namespace Recipe_book.ViewModels;

/// <summary>
/// ViewModel for generating, displaying, and sharing the aggregated shopping list.
/// </summary>
public partial class ShoppingListViewModel : ObservableObject
{
    private readonly RecipesDatabase _database;

    //--------------
    #region Properties
    //--------------

    [ObservableProperty]
    private DateTime startDate = DateTime.Today;

    [ObservableProperty]
    private DateTime endDate = DateTime.Today.AddDays(7);

    [ObservableProperty]
    private int customNumber = 1;

    [ObservableProperty]
    private string customUnit = "ימים";

    public List<string> AvailableCustomUnits { get; } = new() { "ימים", "שבועות", "חודשים" };

    public ObservableCollection<ShoppingItemGroup> GroupedShoppingItems { get; } = new();

    [ObservableProperty]
    private DateRangeType selectedRangeType = DateRangeType.Week;

    [ObservableProperty]
    private bool isCustomDateVisible = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHeaderClosed))]
    private bool isHeaderOpen = false;

    public bool IsHeaderClosed => !IsHeaderOpen;

    #endregion
    //--------------

    public ShoppingListViewModel(RecipesDatabase database)
    {
        _database = database;
    }

    //--------------
    #region Navigation & Date Logic
    //--------------

    public async Task InitializeAutoLoadAsync()
    {
        string savedRangeStr = Preferences.Default.Get("ShoppingListRange", nameof(DateRangeType.Week));
        if (Enum.TryParse(typeof(DateRangeType), savedRangeStr, out var parsedRange))
        {
            SelectedRangeType = (DateRangeType)parsedRange;
        }

        IsCustomDateVisible = (SelectedRangeType == DateRangeType.Custom);

        if (SelectedRangeType == DateRangeType.Custom)
        {
            CustomNumber = Preferences.Default.Get("ShoppingListCustomNum", 1);
            CustomUnit = Preferences.Default.Get("ShoppingListCustomUnit", "ימים");
        }

        ApplyDateRange(SelectedRangeType);
        await GenerateListAsync();
    }

    [RelayCommand]
    public async Task SetDateRangeAsync(string rangeTypeStr)
    {
        if (Enum.TryParse(typeof(DateRangeType), rangeTypeStr, out var parsedRange))
        {
            DateRangeType rangeType = (DateRangeType)parsedRange;

            SelectedRangeType = rangeType;
            Preferences.Default.Set("ShoppingListRange", rangeType.ToString());

            if (rangeType == DateRangeType.Custom)
            {
                IsCustomDateVisible = true;
            }
            else
            {
                IsCustomDateVisible = false;
                IsHeaderOpen = false;

                ApplyDateRange(rangeType);
                await GenerateListAsync();
            }
        }
    }

    private void ApplyDateRange(DateRangeType rangeType)
    {
        StartDate = DateTime.Today;
        switch (rangeType)
        {
            case DateRangeType.Day:
                EndDate = DateTime.Today;
                break;
            case DateRangeType.Week:
                EndDate = DateTime.Today.AddDays(7);
                break;
            case DateRangeType.TwoWeeks:
                EndDate = DateTime.Today.AddDays(14);
                break;
            case DateRangeType.Month:
                EndDate = DateTime.Today.AddDays(30);
                break;
            case DateRangeType.Custom:
                if (CustomUnit == "ימים") EndDate = DateTime.Today.AddDays(CustomNumber);
                else if (CustomUnit == "שבועות") EndDate = DateTime.Today.AddDays(CustomNumber * 7);
                else if (CustomUnit == "חודשים") EndDate = DateTime.Today.AddMonths(CustomNumber);
                break;
        }
    }

    [RelayCommand]
    public async Task ToggleHeaderAsync()
    {
        if (IsHeaderOpen)
        {
            IsHeaderOpen = false;

            if (SelectedRangeType == DateRangeType.Custom)
            {
                Preferences.Default.Set("ShoppingListCustomNum", CustomNumber);
                Preferences.Default.Set("ShoppingListCustomUnit", CustomUnit);
                ApplyDateRange(DateRangeType.Custom);
            }
            await GenerateListAsync();
        }
        else
        {
            IsHeaderOpen = true;
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
        if (SelectedRangeType == DateRangeType.Custom)
        {
            Preferences.Default.Set("ShoppingListCustomNum", CustomNumber);
            Preferences.Default.Set("ShoppingListCustomUnit", CustomUnit);
            ApplyDateRange(DateRangeType.Custom);
        }

        GroupedShoppingItems.Clear();

        var mealsInRange = await _database.GetScheduledMealsAsync(StartDate.Date, EndDate.Date);
        var conversions = await _database.GetIngredientConversionsAsync();
        conversions = conversions.OrderByDescending(c => c.Keyword.Length).ToList();

        var aggregatedIngredients = await ProcessAndAggregateIngredientsAsync(mealsInRange, conversions);

        await BuildAndGroupShoppingList(aggregatedIngredients);

        IsHeaderOpen = false;
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
        var flatList = new List<ShoppingItem>();

        var boughtItemsRecords = await _database.GetBoughtItemsAsync();
        var boughtNames = boughtItemsRecords.Select(b => b.ItemName).ToList();

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

            var newItem = new ShoppingItem
            {
                Name = name,
                DisplayText = finalDisplayText,
                Category = category,
                IsBought = isAlreadyBought
            };

            newItem.PropertyChanged += async (s, e) =>
            {
                if (e.PropertyName == nameof(ShoppingItem.IsBought))
                {
                    if (newItem.IsBought)
                        await _database.AddBoughtItemAsync(newItem.Name);
                    else
                        await _database.RemoveBoughtItemAsync(newItem.Name);
                }
            };

            flatList.Add(newItem);
        }

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

        var sb = new StringBuilder();
        sb.AppendLine("🛒 *רשימת קניות:*");
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

        await Share.Default.RequestAsync(new ShareTextRequest
        {
            Text = sb.ToString(),
            Title = "שיתוף רשימת קניות"
        });
    }

    #endregion
    //--------------
}