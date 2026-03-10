using SQLite;

namespace Recipe_book.Models.Recipes;

/// <summary>
/// Persists the state of checked-off shopping items across app sessions.
/// </summary>
public class BoughtItemRecord
{
    [PrimaryKey]
    public string ItemName { get; set; }
}