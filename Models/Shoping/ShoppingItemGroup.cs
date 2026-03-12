using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Recipe_book.Models.Shopping;

/// <summary>
/// A collection used to group shopping items by their category for UI presentation.
/// </summary>
public class ShoppingItemGroup : ObservableCollection<SavedShoppingListItem>
{
    public string CategoryName { get; private set; }

    public ShoppingItemGroup(string categoryName, IEnumerable<SavedShoppingListItem> items) : base(items)
    {
        CategoryName = categoryName;
    }
}