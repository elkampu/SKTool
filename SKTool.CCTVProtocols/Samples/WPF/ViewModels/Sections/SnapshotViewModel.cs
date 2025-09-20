using Microsoft.Win32;
using SKTool.CCTVProtocols.Hikvision;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace SKTool.CCTVProtocols.Samples.WPF.ViewModels;

public sealed class SnapshotViewModel : ViewModelBase
{
    private readonly Func<HikvisionClient> _clientFactory;

    public SnapshotViewModel(Func<HikvisionClient> clientFactory)
    {
        _clientFactory = clientFactory;
        CaptureCommand = new AsyncRelayCommand(CaptureAsync, () => !Busy);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => Snapshot != null && !Busy);
    }

    private bool _busy;
    public bool Busy { get => _busy; set { Set(ref _busy, value); CaptureCommand.RaiseCanExecuteChanged(); SaveCommand.RaiseCanExecuteChanged(); } }

    public int ChannelId { get => _channelId; set => Set(ref _channelId, value); }
    private int _channelId = 101;

    public BitmapImage? Snapshot { get => _img; set { Set(ref _img, value); SaveCommand.RaiseCanExecuteChanged(); } }
    private BitmapImage? _img;

    private byte[]? _lastBytes;

    public AsyncRelayCommand CaptureCommand { get; }
    public AsyncRelayCommand SaveCommand { get; }

    public async Task CaptureAsync()
    {
        Busy = true;
        try
        {
            using var client = _clientFactory();
            _lastBytes = await client.GetSnapshotAsync(ChannelId);
            Snapshot = LoadBitmap(_lastBytes);
        }
        finally
        {
            Busy = false;
        }
    }

    public Task SaveAsync()
    {
        if (_lastBytes is null) return Task.CompletedTask;
        var dlg = new SaveFileDialog
        {
            Filter = "JPEG Image (*.jpg)|*.jpg",
            FileName = $"snapshot_{ChannelId}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg"
        };
        if (dlg.ShowDialog() == true)
        {
            File.WriteAllBytes(dlg.FileName, _lastBytes);
        }
        return Task.CompletedTask;
    }

    private static BitmapImage LoadBitmap(byte[] bytes)
    {
        var bmp = new BitmapImage();
        using var ms = new MemoryStream(bytes);
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }
}