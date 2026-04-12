using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace Recipe_book.Helpers.Converters;

public class RatingToStarImageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double rating && parameter != null)
        {
            if (int.TryParse(parameter.ToString(), out int starIndex))
            {
                return rating >= starIndex ? "star_fill_icon.svg" : "star_icon.svg";
            }
        }

        return "star_icon.svg";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}