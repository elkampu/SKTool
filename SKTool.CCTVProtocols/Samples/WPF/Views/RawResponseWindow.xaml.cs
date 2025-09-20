using System.Windows;

namespace SKTool.CCTVProtocols.Samples.WPF.Views
{
    public partial class RawResponseWindow : Window
    {
        public RawResponseWindow(string title, string content)
        {
            InitializeComponent();
            Title = title;
            ContentBox.Text = content ?? string.Empty;
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(ContentBox.Text);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}