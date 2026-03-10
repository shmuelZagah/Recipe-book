using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;
using System.Collections.ObjectModel;

namespace Recipe_book.Models.Recipes;

/// <summary>
/// The core entity representing a recipe, containing metadata and serving as a parent container for its ingredients and steps.
/// </summary>
public partial class Recipe : ObservableObject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Title { get; set; }
    public string Description { get; set; }
    public string ImagePath { get; set; }

    [ObservableProperty]
    public bool isFavorite = false;

    public DateTime? LastCookedDate { get; set; }

    // Observable collections populated dynamically for the UI. Ignored by the SQLite database.
    [Ignore]
    public ObservableCollection<Ingredient> Ingredients { get; set; } = new ObservableCollection<Ingredient>();

    [Ignore]
    public ObservableCollection<RecipeStep> Steps { get; set; } = new ObservableCollection<RecipeStep>();
}