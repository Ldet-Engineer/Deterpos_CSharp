using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluentIcons.Common;

namespace Deterpos.WPF.Controls;

public partial class DialogContextBar : UserControl
{
    public static readonly DependencyProperty SymbolProperty =
        DependencyProperty.Register(
            nameof(Symbol),
            typeof(Symbol),
            typeof(DialogContextBar),
            new PropertyMetadata(Symbol.Info));

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(DialogContextBar),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconBrushProperty =
        DependencyProperty.Register(
            nameof(IconBrush),
            typeof(Brush),
            typeof(DialogContextBar),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ExtraContentProperty =
        DependencyProperty.Register(
            nameof(ExtraContent),
            typeof(object),
            typeof(DialogContextBar),
            new PropertyMetadata(null));

    public Symbol Symbol
    {
        get => (Symbol)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public Brush? IconBrush
    {
        get => (Brush?)GetValue(IconBrushProperty);
        set => SetValue(IconBrushProperty, value);
    }

    public object? ExtraContent
    {
        get => GetValue(ExtraContentProperty);
        set => SetValue(ExtraContentProperty, value);
    }

    public DialogContextBar()
    {
        InitializeComponent();
    }
}
