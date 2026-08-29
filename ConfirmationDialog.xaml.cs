using System.Windows;

namespace ChatOutLoud;

public partial class ConfirmationDialog : Window
{
    public ConfirmationDialog(
        string title,
        string message)
    {
        InitializeComponent();

        DialogTitleText.Text =
            title;

        DialogMessageText.Text =
            message;
    }

    private void YesButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult =
            true;
    }

    private void NoButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult =
            false;
    }
}