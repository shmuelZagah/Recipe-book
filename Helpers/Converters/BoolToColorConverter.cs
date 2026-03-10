using System.Globalization;

namespace Recipe_book.Helpers.Converters;

public class BoolToColorConverter : IValueConverter
{

    private readonly Color ActiveColor = Colors.LightGray; 
    private readonly Color InactiveColor = Colors.Gray;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSelected)
        {
            if (parameter is string paramString && paramString == "Invert")
            {
                isSelected = !isSelected;
            }

            return isSelected ? ActiveColor : InactiveColor;
        }

        return InactiveColor; 
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}