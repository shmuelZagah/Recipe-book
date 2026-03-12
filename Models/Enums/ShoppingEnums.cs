namespace Recipe_book.Models.Enums;

/// <summary>
/// Represents the selected date range for generating the shopping list.
/// </summary>
public enum DateRangeType
{
    Day,
    Week,
    TwoWeeks,
    Month,
    NextWeek,      
    CustomRolling,  
    SpecificDates   
}