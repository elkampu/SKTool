using System.Collections.ObjectModel;
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
        LoadCommand = new AsyncRelayCommand(LoadAsync, () => !Busy);
    }

    private bool _busy;
    public bool Busy { get => _busy; set { Set(ref _busy, value); LoadCommand.RaiseCanExecuteChanged(); } }

    public ObservableCollection<ChannelItem> Channels { get; } = new();

    public string RawXml { get => _raw; set => Set(ref _raw, value); }
    private string _raw = "";

    public AsyncRelayCommand LoadCommand { get; }

    public async Task LoadAsync()
    {
        Busy = true;
        try
        {
            Channels.Clear();
            using var client = _clientFactory();
            var x = await client.GetStreamingChannelsAsync();
            RawXml = x.ToString();

            foreach (var ch in x.Descendants("StreamingChannel"))
            {
                var id = ch.Element("id")?.Value ?? "";
                var name = ch.Element("channelName")?.Value ?? "";
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