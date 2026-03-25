using SQLite;
using CommunityToolkit.Mvvm.ComponentModel;
using Plugin.Firebase.Firestore;

namespace Recipe_book.Models.Recipes;

/// <summary>
/// Represents a single ingredient associated with a specific recipe.
/// </summary>
public partial class Ingredient : ObservableObject
{
    [PrimaryKey, AutoIncrement]
    [FirestoreProperty("Id")]
    public int Id { get; set; }

    [Indexed]
    [FirestoreProperty("RecipeId")]
    public int RecipeId { get; set; }

    [ObservableProperty]
    [property: FirestoreProperty("Name")]
    private string name;

    [ObservableProperty]
    [property: FirestoreProperty("Quantity")]
    private double? quantity;

    [ObservableProperty]
    [property: FirestoreProperty("Unit")]
    private string unit = "יחידות";

    /// <summary>
    /// Maintains the order in which the ingredient is displayed within the recipe.
    /// </summary>
    [FirestoreProperty("OrderIndex")]
    public int OrderIndex { get; set; }
}