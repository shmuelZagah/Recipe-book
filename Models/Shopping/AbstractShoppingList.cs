using SQLite;
using System;

namespace Recipe_book.Models.Shopping;

/// <summary>
/// Represents a template/abstract shopping list used as a base for creating actual lists.
/// </summary>
public class AbstractShoppingList
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Title { get; set; }

    public DateTime CreatedAt { get; set; }
}