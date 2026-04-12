using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace Recipe_book.Helpers.Converters;

public class ScreenRatioConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double screenWidth && screenWidth > 0 && parameter != null)
        {
            if (double.TryParse(parameter.ToString(), out double ratio))
            {
                double calculatedWidth = screenWidth * ratio;
                return Math.Clamp(calculatedWidth, 250 , 450);
            }
        }

        return 330; 
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}