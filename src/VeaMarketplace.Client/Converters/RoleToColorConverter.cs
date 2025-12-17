using System;
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
        return System.Windows.Data.Binding.DoNothing;
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
                UserRank.Legend => "Legend",
                UserRank.Elite => "Elite",
                UserRank.Diamond => "Diamond",
                UserRank.Platinum => "Platinum",
                UserRank.Gold => "Gold",
                UserRank.Silver => "Silver",
                UserRank.Bronze => "Bronze",
                _ => "Newcomer"
            };
        }

        return "Newcomer";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

