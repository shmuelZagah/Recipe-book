using CommunityToolkit.Mvvm.ComponentModel;
using SQLite;
using Plugin.Firebase.Firestore;

namespace Recipe_book.Models.Recipes;

/// <summary>
/// Represents a single sequential step in a recipe's preparation instructions.
/// </summary>
public partial class RecipeStep : ObservableObject
{
    [PrimaryKey, AutoIncrement]
    [FirestoreProperty("Id")]
    public int Id { get; set; }

    [Indexed]
    [FirestoreProperty("RecipeId")]
    public int RecipeId { get; set; }

    [ObservableProperty]
    [property: FirestoreProperty("Description")]
    private string description;

    [FirestoreProperty("StepNumber")]
    public int StepNumber { get; set; }

    /// <summary>
    /// Tracks user progress during the cooking process.
    /// </summary>
    [ObservableProperty]
    [property: FirestoreProperty("IsCompleted")]
    private bool isCompleted;

    [ObservableProperty]
    [property: FirestoreProperty("IsOptional")]
    private bool isOptional;
}