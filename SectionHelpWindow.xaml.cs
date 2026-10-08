using System.Collections.Generic;
using System.Windows;

namespace ChatOutLoud;

public sealed record SectionHelpItem(
    string Heading,
    string Body);

public partial class SectionHelpWindow : Window
{
    public SectionHelpWindow(
        string sectionTitle,
        string sectionDescription,
        IEnumerable<SectionHelpItem> helpItems)
    {
        InitializeComponent();

        Title =
            $"Chat Out Loud - {sectionTitle} Help";

        WindowSectionTitleText.Text =
            $"{sectionTitle} Help";

        SectionTitleText.Text =
            sectionTitle.ToUpperInvariant();

        SectionDescriptionText.Text =
            sectionDescription;

        HelpItemsControl.ItemsSource =
            helpItems;
    }

    private void CloseButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }
}