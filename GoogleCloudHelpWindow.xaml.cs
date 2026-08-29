using System.Diagnostics;
using System.Windows;

namespace ChatOutLoud;

public partial class GoogleCloudHelpWindow : Window
{
    public GoogleCloudHelpWindow()
    {
        InitializeComponent();
    }

    private void OpenGoogleCloudConsoleButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Process.Start(
            new ProcessStartInfo
            {
                FileName =
                    "https://console.cloud.google.com/",

                UseShellExecute =
                    true
            });
    }
}