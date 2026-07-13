using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DynamicIslandBar;

public partial class CapsuleConfirmationDialog : Window
{
    private CapsuleConfirmationDialog(
        CapsuleThemePreset themePreset,
        string title,
        string message,
        string confirmText)
    {
        InitializeComponent();
        Title = title;
        DialogTitleText.Text = title;
        DialogMessageText.Text = message;
        ConfirmButton.Content = confirmText;
        ApplyTheme(themePreset);
    }

    public static bool ShowConfirmation(
        Window owner,
        CapsuleThemePreset themePreset,
        string title,
        string message,
        string confirmText)
    {
        var dialog = new CapsuleConfirmationDialog(themePreset, title, message, confirmText)
        {
            Owner = owner
        };
        return dialog.ShowDialog() == true;
    }

    private void ApplyTheme(CapsuleThemePreset themePreset)
    {
        var isWhite = themePreset == CapsuleThemePreset.TransparentWhite;
        var foreground = ColorFrom("#FFF4FAFF");
        var muted = ColorFrom("#FFC5D8EA");
        var accent = ColorFrom(isWhite ? "#FFFFFFFF" : "#FF46E0FF");

        DialogSurface.Background = Brushes.Transparent;
        DialogSurface.BorderBrush = new SolidColorBrush(
            ColorFrom(isWhite ? "#EFFFFFFF" : "#B846E0FF"));
        Foreground = new SolidColorBrush(foreground);
        DialogMessageText.Foreground = new SolidColorBrush(muted);
        CloseButton.Foreground = new SolidColorBrush(muted);

        DialogIconSurface.Background = Brushes.Transparent;
        DialogIconSurface.BorderBrush = new SolidColorBrush(Color.FromArgb(150, accent.R, accent.G, accent.B));
        DialogIconGlyph.Foreground = new SolidColorBrush(accent);

        StyleButton(CancelButton, foreground, Color.FromArgb(18, 255, 255, 255), Color.FromArgb(95, 255, 255, 255));
        StyleButton(ConfirmButton, Colors.White, ColorFrom("#FFE5667C"), ColorFrom("#FFFFB2BF"));
        DialogShadow.Color = Colors.Black;
    }

    private static void StyleButton(
        System.Windows.Controls.Button button,
        Color foreground,
        Color background,
        Color border)
    {
        button.Foreground = new SolidColorBrush(foreground);
        button.Background = new SolidColorBrush(background);
        button.BorderBrush = new SolidColorBrush(border);
    }

    private static Color ColorFrom(string value) => (Color)ColorConverter.ConvertFromString(value);

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
        else if (e.Key == Key.Enter)
        {
            DialogResult = true;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
