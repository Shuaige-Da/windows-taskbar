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
        var foreground = ColorFrom(isWhite ? "#FF243B58" : "#FFF4FAFF");
        var muted = ColorFrom(isWhite ? "#FF617895" : "#FFC5D8EA");
        var accent = ColorFrom(isWhite ? "#FF4D8CFF" : "#FF46E0FF");

        DialogSurface.Background = new LinearGradientBrush(
            ColorFrom(isWhite ? "#E8F7FBFF" : "#E5182634"),
            ColorFrom(isWhite ? "#C8E8F1FA" : "#E50A1422"),
            45);
        DialogSurface.BorderBrush = new SolidColorBrush(
            ColorFrom(isWhite ? "#EFFFFFFF" : "#B846E0FF"));
        Foreground = new SolidColorBrush(foreground);
        DialogMessageText.Foreground = new SolidColorBrush(muted);
        CloseButton.Foreground = new SolidColorBrush(muted);

        DialogIconSurface.Background = new SolidColorBrush(Color.FromArgb(30, accent.R, accent.G, accent.B));
        DialogIconSurface.BorderBrush = new SolidColorBrush(Color.FromArgb(150, accent.R, accent.G, accent.B));
        DialogIconGlyph.Foreground = new SolidColorBrush(accent);

        StyleButton(CancelButton, foreground, Color.FromArgb(18, 255, 255, 255), Color.FromArgb(95, 255, 255, 255));
        StyleButton(ConfirmButton, Colors.White, ColorFrom("#FFE5667C"), ColorFrom("#FFFFB2BF"));
        DialogShadow.Color = isWhite ? ColorFrom("#700C2948") : Colors.Black;
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
