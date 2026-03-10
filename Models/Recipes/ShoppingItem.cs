using CommunityToolkit.Mvvm.ComponentModel;

namespace Recipe_book.Models.Recipes;

/// <summary>
/// Represents a consolidated item in the generated shopping list.
/// </summary>
public partial class ShoppingItem : ObservableObject
{
    public string Name { get; set; }

    /// <summary>
    /// The formatted text displayed in the UI.
    /// </summary>
    public string DisplayText { get; set; }

    public string Category { get; set; }

    /// <summary>
    /// Tracks whether the user has checked this item off the shopping list.
    /// </summary>
    [ObservableProperty]
    private bool isBought;
}