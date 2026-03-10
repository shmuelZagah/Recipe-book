using SQLite;
using System;

namespace Recipe_book.Models.Recipes;

/// <summary>
/// Represents a recipe scheduled for a specific date and meal category in the user's meal planner.
/// </summary>
public class ScheduledMeal
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public DateTime Date { get; set; }

    public string MealType { get; set; }

    public int RecipeId { get; set; }
}