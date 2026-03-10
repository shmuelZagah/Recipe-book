using SQLite;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Recipe_book.Models.Recipes;

/// <summary>
/// Represents a single ingredient associated with a specific recipe.
/// </summary>
public partial class Ingredient : ObservableObject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int RecipeId { get; set; }

    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private double? quantity;

    [ObservableProperty]
    private string unit = "יחידות";

    /// <summary>
    /// Maintains the order in which the ingredient is displayed within the recipe.
    /// </summary>
    public int OrderIndex { get; set; }
}