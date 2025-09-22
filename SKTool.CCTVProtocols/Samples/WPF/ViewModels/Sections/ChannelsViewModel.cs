using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using SKTool.CCTVProtocols.Hikvision;

namespace SKTool.CCTVProtocols.Samples.WPF.ViewModels;

public sealed class ChannelsViewModel : ViewModelBase
{
    private readonly Func<HikvisionClient> _clientFactory;

    public ChannelsViewModel(Func<HikvisionClient> clientFactory)
    {
        _clientFactory = clientFactory;
        LoadCommand = new AsyncRelayCommand(ct => LoadAsync(ct), () => !Busy);
    }

    private bool _busy;
    public bool Busy { get => _busy; set { Set(ref _busy, value); LoadCommand.RaiseCanExecuteChanged(); } }

    public ObservableCollection<ChannelItem> Channels { get; } = new();

    public string RawXml { get => _raw; set => Set(ref _raw, value); }
    private string _raw = "";

    public AsyncRelayCommand LoadCommand { get; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        Busy = true;
        try
        {
            Channels.Clear();
            using var client = _clientFactory();
            var x = await client.GetStreamingChannelsAsync(ct);
            RawXml = x.ToString();

            var ns = x.Root?.GetDefaultNamespace() ?? XNamespace.None;
            foreach (var ch in x.Descendants(ns + "StreamingChannel"))
            {
                var id = ch.Element(ns + "id")?.Value ?? "";
                var name = ch.Element(ns + "channelName")?.Value ?? "";
                Channels.Add(new ChannelItem { Id = id, Name = name });
            }
        }
        finally
        {
            Busy = false;
        }
    }

    public sealed class ChannelItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }
}