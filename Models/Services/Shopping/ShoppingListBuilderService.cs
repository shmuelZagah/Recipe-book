using Recipe_book.Helpers;
using Recipe_book.Models.Recipes;
using Recipe_book.Models.Shopping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Recipe_book.Services.Shopping;

public class ShoppingListBuilderService
{
    private readonly RecipesDatabase _database;

    public ShoppingListBuilderService(RecipesDatabase database)
    {
        _database = database;
    }

    /// <summary>
    /// Harvests ingredients from scheduled meals within a specific range and compiles a flat static snapshot list.
    /// </summary>
    public async Task<List<SavedShoppingListItem>> BuildIngredientsFromScheduleAsync(int listId, DateTime startDate, DateTime endDate)
    {
        var mealsInRange = await _database.GetScheduledMealsAsync(startDate, endDate);
        var conversions = await _database.GetIngredientConversionsAsync();
        conversions = conversions.OrderByDescending(c => c.Keyword.Length).ToList();

        var aggregatedIngredients = await ProcessAndAggregateIngredientsAsync(mealsInRange, conversions);
        var flatList = new List<SavedShoppingListItem>();

        foreach (var item in aggregatedIngredients)
        {
            var keyParts = item.Key.Split('_');
            string name = keyParts[0];
            string unit = keyParts[1];
            double finalQuantity = item.Value.Quantity;
            string category = item.Value.Category;

            // Standardize weight and volume rounding thresholds
            if (unit == "גרם" || unit == "מ״ל")
            {
                finalQuantity = Math.Ceiling(finalQuantity / 10.0) * 10.0;
                if (finalQuantity >= 1000)
                {
                    finalQuantity /= 1000.0;
                    unit = unit == "גרם" ? "ק״ג" : "ליטר";
                }
            }
            else
            {
                finalQuantity = Math.Ceiling(finalQuantity);
            }

            flatList.Add(new SavedShoppingListItem
            {
                ListId = listId,
                Name = name,
                Category = category,
                Quantity = finalQuantity,
                Unit = unit,
                IsBought = false
            });
        }

        return flatList;
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

    private async Task<IngredientConversion> FindOrSuggestConversionAsync(Recipe recipe, Ingredient ingredient, string originalName, List<IngredientConversion> conversions)
    {
        var possibleNames = TextHelpers.GetPossibleSingulars(originalName);
        var conversion = conversions.FirstOrDefault(c => possibleNames.Any(p => c.Keyword == p || originalName.Contains(c.Keyword) || p.Contains(c.Keyword)));

        if (conversion == null)
        {
            int maxAllowedDistance = originalName.Length <= 4 ? 1 : 2;
            var suggestions = conversions
                .Where(c => possibleNames.Any(p => c.Keyword.Contains(p) || p.Contains(c.Keyword) || TextHelpers.ComputeLevenshteinDistance(p, c.Keyword) <= maxAllowedDistance))
                .OrderBy(c => TextHelpers.ComputeLevenshteinDistance(originalName, c.Keyword))
                .Select(c => c.Keyword).Distinct().Take(4).ToList();

            if (suggestions.Any())
            {
                suggestions.Add("משהו אחר (צור חדש)");
                string selectedOption = await Application.Current.MainPage.DisplayActionSheet($"במתכון '{recipe.Title}' כתבת '{originalName}'. למה התכוונת?", "דלג", null, suggestions.ToArray());

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
        string choice = await Application.Current.MainPage.DisplayActionSheet($"לשנות את '{oldName}' ל-'{newName}' רק כאן או בכל המתכונים?", "רק במתכון הזה", null, "בכל המתכונים שלי");

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
        string finalName = await Application.Current.MainPage.DisplayPromptAsync("מצרך חדש", $"במתכון '{recipe.Title}' כתבת '{currentName}'. איך תרצה לקרוא לזה במערכת?", initialValue: currentName);
        if (string.IsNullOrWhiteSpace(finalName)) return null;

        if (finalName != currentName)
        {
            await ApplyCorrectionAsync(ingredient, currentName, finalName);
        }

        string selectedCategory = await Application.Current.MainPage.DisplayActionSheet($"לאיזו מחלקה שייך '{finalName}'?", "דלג", null, Helpers.AppConstants.ShoppingCategories);
        if (selectedCategory == "דלג" || string.IsNullOrEmpty(selectedCategory)) return null;

        double gramsPerCup = 0;
        bool skippedVolume = false;

        if (isConvertibleVolume)
        {
            string result = await Application.Current.MainPage.DisplayPromptAsync("משקל המצרך", $"כמה גרם שוקלת כוס אחת של '{finalName}'?", "שמור ולמד", "דלג", keyboard: Keyboard.Numeric);
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
}