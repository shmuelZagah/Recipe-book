using SQLite;
using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Recipe_book.Models.Shopping;

public partial class SavedShoppingListItem : ObservableObject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int ListId { get; set; }

    public string Name { get; set; }
    public string Category { get; set; }
    public double Quantity { get; set; }
    public string Unit { get; set; }

    public DateTime? LastCheckedDate { get; set; }

    private string displayText;
    public string DisplayText
    {
        get => displayText;
        set => SetProperty(ref displayText, value);
    }

    public double ManualQuantity { get; set; }
    public double AutoQuantity { get; set; }
    public bool IsLocked { get; set; }

    public DateTime? CoveredUntil { get; set; }
    public double CoveredQuantity { get; set; }

    [Ignore]
    public DateTime? CurrentRequiredBy { get; set; }

    [ObservableProperty]
    private bool isBought;

    partial void OnIsBoughtChanged(bool value)
    {
        if (value)
        {
            if (!CoveredUntil.HasValue)
            {
                CoveredUntil = CurrentRequiredBy ?? DateTime.Today.AddDays(7);
            }
            CoveredQuantity = Quantity;
            LastCheckedDate = DateTime.Today;
        }
        else
        {
            // Reset coverage only if it is a pure manual uncheck of a fully satisfied item
            if (Quantity <= CoveredQuantity)
            {
                CoveredUntil = null;
                CoveredQuantity = 0;
            }
        }
        UpdateDisplayText();
    }

    public void UpdateDisplayText()
    {
        double displayQty = Quantity;

        // Show only the missing delta if we have partial coverage
        if (!IsBought && CoveredQuantity > 0 && Quantity > CoveredQuantity)
        {
            displayQty = Quantity - CoveredQuantity;
        }

        string displayUnit = (string.IsNullOrWhiteSpace(Unit) || Unit.Trim() == "יחידות") ? "" : Unit.Trim();
        DisplayText = string.IsNullOrWhiteSpace(displayUnit) ?
            $"{displayQty} {Name?.Trim()}" :
            $"{displayQty} {displayUnit} {Name?.Trim()}";
    }
}