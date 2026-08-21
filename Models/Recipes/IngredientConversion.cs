using SQLite;

namespace Recipe_book.Models.Recipes;

/// <summary>
/// Defines conversion rules for ingredients to standardize units (e.g., converting cups to grams) 
/// when generating the unified shopping list.
/// </summary>
public class IngredientConversion
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Keyword { get; set; }
    public string BaseUnit { get; set; }

    /// <summary>
    /// The weight in grams for one cup of this specific ingredient.
    /// </summary>
    public double AmountPerCup { get; set; }

    public string Category { get; set; }

    public string PluralKeyword { get; set; }
}