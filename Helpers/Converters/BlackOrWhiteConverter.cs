using System.Globalization;

namespace Recipe_book.Helpers.Converters;

public class BlackOrWhiteConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Color bgColor)
        {
            // אם הצבע שקוף או בהיר מאוד (לחות גבוהה) - נחזיר טקסט שחור/אפור כהה
            if (bgColor.Alpha < 0.1 || bgColor.GetLuminosity() > 0.7)
            {
                return Color.FromArgb("#333333"); // שחור-אפור אלגנטי
            }

            // אם הצבע כהה (היום נבחר) - נחזיר טקסט לבן
            return Colors.White;
        }

        return Colors.Black; // ברירת מחדל
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}