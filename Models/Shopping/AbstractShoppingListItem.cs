using SQLite;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Recipe_book.Models.Shopping;

/// <summary>
/// Represents an item within an abstract shopping list template.
/// </summary>
public partial class AbstractShoppingListItem : ObservableObject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int ListId { get; set; }

    public string Name { get; set; }
    public string Category { get; set; }
    public double Quantity { get; set; }
    public string Unit { get; set; }

    private string displayText;
    public string DisplayText
    {
        get => displayText;
        set => SetProperty(ref displayText, value);
    }

    public void UpdateDisplayText()
    {
        string displayUnit = string.IsNullOrWhiteSpace(Unit) || Unit.Trim() == "יחידות" ? "" : Unit.Trim();
        DisplayText = string.IsNullOrWhiteSpace(displayUnit) ?
            $"{Quantity} {Name?.Trim()}" :
            $"{Quantity} {displayUnit} {Name?.Trim()}";
    }
}