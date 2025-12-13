using System.Windows;
using System.Windows.Controls;

namespace VeaMarketplace.Client.Controls;

public partial class LoadingSkeleton : UserControl
{
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(LoadingSkeleton),
            new PropertyMetadata(new CornerRadius(4)));

    public static readonly DependencyProperty SkeletonTypeProperty =
        DependencyProperty.Register(nameof(SkeletonType), typeof(SkeletonType), typeof(LoadingSkeleton),
            new PropertyMetadata(SkeletonType.Rectangle, OnSkeletonTypeChanged));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public SkeletonType SkeletonType
    {
        get => (SkeletonType)GetValue(SkeletonTypeProperty);
        set => SetValue(SkeletonTypeProperty, value);
    }

    public LoadingSkeleton()
    {
        InitializeComponent();
    }

    private static void OnSkeletonTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingSkeleton skeleton)
        {
            skeleton.ApplySkeletonType((SkeletonType)e.NewValue);
        }
    }

    private void ApplySkeletonType(SkeletonType type)
    {
        CornerRadius = type switch
        {
            SkeletonType.Circle => new CornerRadius(9999),
            SkeletonType.Text => new CornerRadius(3),
            SkeletonType.Card => new CornerRadius(8),
            SkeletonType.Avatar => new CornerRadius(9999),
            SkeletonType.Button => new CornerRadius(6),
            _ => new CornerRadius(4)
        };
    }
}

public enum SkeletonType
{
    Rectangle,
    Circle,
    Text,
    Card,
    Avatar,
    Button
}
