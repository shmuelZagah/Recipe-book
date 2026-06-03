using SQLite;
using System;

namespace Recipe_book.Models.Shopping;

/// <summary>
/// Represents a saved shopping list in the database.
/// </summary>
public class SavedShoppingList
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Title { get; set; }

    public DateTime CreatedAt { get; set; }

    public string CloudId { get; set; }
    public bool IsShared { get; set; }

    // Nullable dates mean the list is not bound to a specific timeframe
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }


}