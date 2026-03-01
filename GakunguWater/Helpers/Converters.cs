using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows;

namespace GakunguWater.Helpers;

// Turns a connection status into a colour Brush
public class StatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Active" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC71")),
            "Suspended" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12")),
            "Disconnected" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")),
            _ => Brushes.Gray
        };
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

// Turns invoice Status into a colour
public class InvoiceStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Paid" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC71")),
            "PartiallyPaid" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F39C12")),
            "Unpaid" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E74C3C")),
            _ => Brushes.Gray
        };
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

// Boolean-to-Visibility
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

// Formats a month/year pair into "January 2025"
public class MonthYearConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime dt) return dt.ToString("MMMM yyyy");
        return value?.ToString() ?? "";
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
