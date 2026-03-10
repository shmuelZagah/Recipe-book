using System.Collections.ObjectModel;

namespace Recipe_book.Models.Recipes;

/// <summary>
/// A collection used to group shopping items by their category (e.g., Dairy, Vegetables) for UI presentation.
/// </summary>
public class ShoppingItemGroup : ObservableCollection<ShoppingItem>
{
    public string CategoryName { get; private set; }

    public ShoppingItemGroup(string categoryName, IEnumerable<ShoppingItem> items) : base(items)
    {
        CategoryName = categoryName;
    }
}