using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;

namespace Recipe_book.Models.Recipes;

/// <summary>
/// Represents a single sequential step in a recipe's preparation instructions.
/// </summary>
public partial class RecipeStep : ObservableObject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int RecipeId { get; set; }

    [ObservableProperty]
    private string description;

    public int StepNumber { get; set; }

    /// <summary>
    /// Tracks user progress during the cooking process.
    /// </summary>
    [ObservableProperty]
    private bool isCompleted;

    [ObservableProperty]
    private bool isOptional;
}