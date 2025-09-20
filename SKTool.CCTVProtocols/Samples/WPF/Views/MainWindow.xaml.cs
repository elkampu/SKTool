using System.Threading.Tasks;
using System.Windows;
using SKTool.CCTVProtocols.Samples.WPF.Services;
using SKTool.CCTVProtocols.Samples.WPF.ViewModels;

namespace SKTool.CCTVProtocols.Samples.WPF.Views
{
    public partial class MainWindow : Window
    {
        public MainViewModel VM { get; } = new();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = VM;
        }

        private async void ApplyPassword_Click(object sender, RoutedEventArgs e)
        {
            VM.Password = PwdBox.Password;

            try
            {
                await VM.LoadAllAsync();
            }
            catch (System.Exception ex)
            {
                CameraErrorHandler.Handle(ex, "Apply Password");
            }
        }

        private void ViewNetworkResponse_Click(object sender, RoutedEventArgs e)
        {
            var w = new RawResponseWindow("Network Response", VM.NetworkVM.RawXml);
            w.Owner = this;
            w.ShowDialog();
        }

        private void ViewTimeResponse_Click(object sender, RoutedEventArgs e)
        {
            var w = new RawResponseWindow("Time Response", VM.TimeNtpVM.RawTimeXml);
            w.Owner = this;
            w.ShowDialog();
        }

        private void ViewNtpResponse_Click(object sender, RoutedEventArgs e)
        {
            var w = new RawResponseWindow("NTP Response", VM.TimeNtpVM.RawNtpXml);
            w.Owner = this;
            w.ShowDialog();
        }

        private void ViewVideoResponse_Click(object sender, RoutedEventArgs e)
        {
            var w = new RawResponseWindow("Video Response", VM.VideoVM.RawXml);
            w.Owner = this;
            w.ShowDialog();
        }

        private void ViewDeviceInfoResponse_Click(object sender, RoutedEventArgs e)
        {
            var w = new RawResponseWindow("Device Info Response", VM.DeviceVM.DeviceInfoXml);
            w.Owner = this;
            w.ShowDialog();
        }

        private void ViewChannelsResponse_Click(object sender, RoutedEventArgs e)
        {
            var w = new RawResponseWindow("Channels Response", VM.ChannelsVM.RawXml);
            w.Owner = this;
            w.ShowDialog();
        }
    }
}