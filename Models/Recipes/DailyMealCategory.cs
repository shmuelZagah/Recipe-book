using SQLite;

namespace Recipe_book.Models.Recipes;

/// <summary>
/// Defines dynamic meal categories (e.g., Breakfast, Lunch) for a specific date in the meal planner.
/// </summary>
public class DailyMealCategory
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public DateTime Date { get; set; }

    public string GroupName { get; set; }

    /// <summary>
    /// Determines the display order of the meal category in the UI.
    /// </summary>
    public int DisplayOrder { get; set; }
}