using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Debug;
using SKTool.CCTVProtocols.Hikvision;
using SKTool.CCTVProtocols.Logging;
using SKTool.CCTVProtocols.Samples.WPF.Models;
using SKTool.CCTVProtocols.Samples.WPF.Services;

namespace SKTool.CCTVProtocols.Samples.WPF.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private static readonly ILoggerFactory LoggerFactory =
        Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Information)
                .AddDebug()
                .AddProvider(new JsonFileLoggerProvider("Logs/sktool.log", maxBytes: 2 * 1024 * 1024, rollCount: 3));
        });

    private readonly DeviceModel _device = new();

    public string Host { get => _device.Host; set { _device.Host = value; OnChanged(); } }
    public int Port { get => _device.Port; set { _device.Port = value; OnChanged(); } }
    public bool UseHttps { get => _device.UseHttps; set { _device.UseHttps = value; OnChanged(); } }
    public string Username { get => _device.Username; set { _device.Username = value; OnChanged(); } }
    public string Password { get => _device.Password; set { _device.Password = value; OnChanged(); } }
    public bool PreferDigest { get => _device.PreferDigest; set { _device.PreferDigest = value; OnChanged(); } }

    private string _status = "Idle";
    public string Status { get => _status; set => Set(ref _status, value); }

    // Section VMs
    public NetworkViewModel NetworkVM { get; }
    public TimeNtpViewModel TimeNtpVM { get; }
    public VideoViewModel VideoVM { get; }
    public SnapshotViewModel SnapshotVM { get; }
    public ChannelsViewModel ChannelsVM { get; }
    public DeviceOperationsViewModel DeviceVM { get; }

    public MainViewModel()
    {
        NetworkVM = new NetworkViewModel(CreateClient);
        TimeNtpVM = new TimeNtpViewModel(CreateClient);
        VideoVM = new VideoViewModel(CreateClient);
        SnapshotVM = new SnapshotViewModel(CreateClient);
        ChannelsVM = new ChannelsViewModel(CreateClient);
        DeviceVM = new DeviceOperationsViewModel(CreateClient);
    }

    private HikvisionClient CreateClient()
    {
        try
        {
            var dev = _device.ToDevice();
            var logger = LoggerFactory.CreateLogger<HikvisionClient>();
            return new HikvisionClient(
                dev,
                _device.PreferDigest,
                timeout: TimeSpan.FromSeconds(20),
                allowSelfSigned: true,
                connectTimeout: TimeSpan.FromSeconds(60),
                logger: logger);
        }
        catch (Exception ex)
        {
            CameraErrorHandler.Handle(ex, "Create Client");
            throw;
        }
    }

    public async Task LoadAllAsync(CancellationToken ct = default)
    {
        try { await NetworkVM.LoadAsync(ct); }
        catch (Exception ex) when (Handle(ex, "Load Network")) { }

        try { await TimeNtpVM.LoadAsync(ct); }
        catch (Exception ex) when (Handle(ex, "Load Time/NTP")) { }

        try { await VideoVM.LoadAsync(ct); }
        catch (Exception ex) when (Handle(ex, "Load Video")) { }

        try { await ChannelsVM.LoadAsync(ct); }
        catch (Exception ex) when (Handle(ex, "Load Channels")) { }
    }

    private static bool Handle(Exception ex, string ctx)
    {
        CameraErrorHandler.Handle(ex, ctx);
        return true;
    }

    private void OnChanged() { }
}