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
    public string PluralName { get; set; }
    public string CustomDisplay { get; set; }
    public string DisplayText
    {
        get => displayText;
        set => SetProperty(ref displayText, value);
    }

    public void UpdateDisplayText()
    {
        // אם למדנו משהו ספציפי עבור המצב הנוכחי (יחיד או רבים) - נשתמש בו וזהו.
        if (!string.IsNullOrWhiteSpace(CustomDisplay))
        {
            DisplayText = $"{Quantity} {CustomDisplay}";
            return;
        }

        // --- מכאן והלאה: מנוע הניחוש החכם של המערכת (Fallback) ---
        string displayUnit = string.IsNullOrWhiteSpace(Unit) || Unit.Trim() == "יחידות" ? "" : Unit.Trim();
        string displayName = Name?.Trim();
        bool hasUnit = !string.IsNullOrWhiteSpace(displayUnit);

        var packagingUnits = new List<string> { "חבילה", "קופסה", "בקבוק", "צנצנת", "פחית", "שקית", "מארז", "קרטון", "ארגז" }; // הוספנו ארגז
        bool isPackagingUnit = packagingUnits.Contains(displayUnit);

        if (Quantity > 1 || (Quantity != 1 && Quantity > 0))
        {
            if (hasUnit && isPackagingUnit)
            {
                // מנחש ישר את הרבים עבור האריזה
                if (displayUnit == "חבילה") displayUnit = "חבילות";
                else if (displayUnit == "קופסה") displayUnit = "קופסאות";
                else if (displayUnit == "בקבוק") displayUnit = "בקבוקים";
                else if (displayUnit == "צנצנת") displayUnit = "צנצנות";
                else if (displayUnit == "פחית") displayUnit = "פחיות";
                else if (displayUnit == "שקית") displayUnit = "שקיות";
                else if (displayUnit == "מארז") displayUnit = "מארזים";
                else if (displayUnit == "קרטון") displayUnit = "קרטונים";
                else if (displayUnit == "ארגז") displayUnit = "ארגזים"; // הוספנו ארגז
            }
            else if (!hasUnit && !string.IsNullOrWhiteSpace(PluralName))
            {
                displayName = PluralName.Trim();
            }
        }

        if (!hasUnit)
        {
            DisplayText = $"{Quantity} {displayName}";
        }
        else if (isPackagingUnit)
        {
            DisplayText = $"{Quantity} {displayUnit} של {displayName}";
        }
        else
        {
            DisplayText = $"{Quantity} {displayUnit} {displayName}";
        }
    }
}