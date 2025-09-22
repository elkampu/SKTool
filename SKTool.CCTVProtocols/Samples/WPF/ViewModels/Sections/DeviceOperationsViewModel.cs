using SKTool.CCTVProtocols.Hikvision;
using System.Threading;
using System.Threading.Tasks;

namespace SKTool.CCTVProtocols.Samples.WPF.ViewModels;

public sealed class DeviceOperationsViewModel : ViewModelBase
{
    private readonly Func<HikvisionClient> _clientFactory;

    public DeviceOperationsViewModel(Func<HikvisionClient> clientFactory)
    {
        _clientFactory = clientFactory;
        GetDeviceInfoCommand = new AsyncRelayCommand(ct => GetDeviceInfoAsync(ct), () => !Busy);
        RebootCommand = new AsyncRelayCommand(ct => RebootAsync(ct), () => !Busy);
    }

    private bool _busy;
    public bool Busy { get => _busy; set { Set(ref _busy, value); GetDeviceInfoCommand.RaiseCanExecuteChanged(); RebootCommand.RaiseCanExecuteChanged(); } }

    public string DeviceInfoXml { get => _deviceInfoXml; set => Set(ref _deviceInfoXml, value); }
    private string _deviceInfoXml = "";

    public AsyncRelayCommand GetDeviceInfoCommand { get; }
    public AsyncRelayCommand RebootCommand { get; }

    public async Task GetDeviceInfoAsync(CancellationToken ct = default)
    {
        Busy = true;
        try
        {
            using var client = _clientFactory();
            var x = await client.GetDeviceInfoAsync(ct);
            DeviceInfoXml = x.ToString();
        }
        finally
        {
            Busy = false;
        }
    }

    public async Task RebootAsync(CancellationToken ct = default)
    {
        Busy = true;
        try
        {
            using var client = _clientFactory();
            await client.RebootAsync(ct);
        }
        finally
        {
            Busy = false;
        }
    }
}