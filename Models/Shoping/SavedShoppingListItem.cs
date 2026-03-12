using SQLite;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Recipe_book.Models.Shopping;

/// <summary>
/// Represents a single grocery item within a SavedShoppingList.
/// </summary>
public partial class SavedShoppingListItem : ObservableObject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    // Foreign key linking to SavedShoppingList.Id
    [Indexed]
    public int ListId { get; set; }

    public string Name { get; set; }

    public string Category { get; set; }

    public double Quantity { get; set; }

    public string Unit { get; set; }

    public string DisplayText { get; set; }

    [ObservableProperty]
    private bool isBought;
}