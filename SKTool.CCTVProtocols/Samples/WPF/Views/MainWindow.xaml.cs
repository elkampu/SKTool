using System.Threading;
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
                using var cts = new CancellationTokenSource();
                await VM.LoadAllAsync(cts.Token);
            }
            catch (System.Exception ex)
            {
                CameraErrorHandler.Handle(ex, "Apply Password");
            }
        }

        private void CancelAll_Click(object sender, RoutedEventArgs e)
        {
            // best-effort: cancel each async command that supports cancellation
            VM.NetworkVM.LoadCommand.Cancel();
            VM.NetworkVM.ApplyCommand.Cancel();

            VM.TimeNtpVM.LoadCommand.Cancel();
            VM.TimeNtpVM.SetManualNowCommand.Cancel();
            VM.TimeNtpVM.ApplyNtpCommand.Cancel();
            VM.TimeNtpVM.SetSpainTzCommand.Cancel();

            VM.VideoVM.LoadCommand.Cancel();
            VM.VideoVM.ApplyCommand.Cancel();

            VM.DeviceVM.GetDeviceInfoCommand.Cancel();
            VM.DeviceVM.RebootCommand.Cancel();

            VM.SnapshotVM.CaptureCommand.Cancel();
            // Save is synchronous
            VM.ChannelsVM.LoadCommand.Cancel();
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
        private void CancelChannels_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Add logic to handle canceling channel operations if needed
        }
        private void CancelNetwork_Click(object sender, RoutedEventArgs e)
        {
            VM.NetworkVM.LoadCommand.Cancel();
            VM.NetworkVM.ApplyCommand.Cancel();
        }

        private void CancelTime_Click(object sender, RoutedEventArgs e)
        {
            VM.TimeNtpVM.LoadCommand.Cancel();
            VM.TimeNtpVM.SetManualNowCommand.Cancel();
            VM.TimeNtpVM.ApplyNtpCommand.Cancel();
            VM.TimeNtpVM.SetSpainTzCommand.Cancel();
        }

        private void CancelVideo_Click(object sender, RoutedEventArgs e)
        {
            VM.VideoVM.LoadCommand.Cancel();
            VM.VideoVM.ApplyCommand.Cancel();
        }

        private void CancelDevice_Click(object sender, RoutedEventArgs e)
        {
            VM.DeviceVM.GetDeviceInfoCommand.Cancel();
            VM.DeviceVM.RebootCommand.Cancel();
        }

        private void CancelSnapshot_Click(object sender, RoutedEventArgs e)
        {
            VM.SnapshotVM.CaptureCommand.Cancel();
        }
    }
}