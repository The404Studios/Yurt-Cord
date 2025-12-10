using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using VeaMarketplace.Shared.Enums;

namespace VeaMarketplace.Client.Converters;

public class RoleToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is UserRole role)
        {
            var color = role switch
            {
                UserRole.Owner => Color.FromRgb(255, 215, 0),
                UserRole.Admin => Color.FromRgb(231, 76, 60),
                UserRole.Moderator => Color.FromRgb(155, 89, 182),
                UserRole.VIP => Color.FromRgb(0, 255, 136),
                UserRole.Verified => Color.FromRgb(52, 152, 219),
                _ => Color.FromRgb(185, 187, 190)
            };

            return new SolidColorBrush(color);
        }

        return new SolidColorBrush(Color.FromRgb(185, 187, 190));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class RankToBadgeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is UserRank rank)
        {
            return rank switch
            {
                UserRank.Legend => "👑 Legend",
                UserRank.Elite => "🔥 Elite",
                UserRank.Diamond => "💎 Diamond",
                UserRank.Platinum => "✨ Platinum",
                UserRank.Gold => "🥇 Gold",
                UserRank.Silver => "🥈 Silver",
                UserRank.Bronze => "🥉 Bronze",
                _ => "🌟 Newcomer"
            };
        }

        return "🌟 Newcomer";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class TimestampConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime timestamp)
        {
            var now = DateTime.Now;
            var local = timestamp.ToLocalTime();

            if (local.Date == now.Date)
                return $"Today at {local:h:mm tt}";

            if (local.Date == now.Date.AddDays(-1))
                return $"Yesterday at {local:h:mm tt}";

            return local.ToString("MM/dd/yyyy h:mm tt");
        }

        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
