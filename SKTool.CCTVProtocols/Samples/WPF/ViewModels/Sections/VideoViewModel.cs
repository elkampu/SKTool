using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using SKTool.CCTVProtocols.Hikvision;

namespace SKTool.CCTVProtocols.Samples.WPF.ViewModels;

public sealed class VideoViewModel : ViewModelBase
{
    private readonly Func<HikvisionClient> _clientFactory;

    public VideoViewModel(Func<HikvisionClient> clientFactory)
    {
        _clientFactory = clientFactory;
        LoadCommand = new AsyncRelayCommand(ct => LoadAsync(ct), () => !Busy);
        ApplyCommand = new AsyncRelayCommand(ct => ApplyAsync(ct), () => !Busy);
    }

    private bool _busy;
    public bool Busy { get => _busy; set { Set(ref _busy, value); LoadCommand.RaiseCanExecuteChanged(); ApplyCommand.RaiseCanExecuteChanged(); } }

    public int ChannelId { get => _channelId; set => Set(ref _channelId, value); }
    private int _channelId = 101;

    public string VideoCodecType { get => _codec; set => Set(ref _codec, value); }
    private string _codec = "H.264";

    public string BitrateType { get => _btype; set => Set(ref _btype, value); }
    private string _btype = "VBR"; // or CBR

    public int MaxFrameRate { get => _fps; set => Set(ref _fps, value); }
    private int _fps = 25;

    public int ConstantBitRateKbps { get => _cbr; set => Set(ref _cbr, value); }
    private int _cbr = 2048;

    public int VbrUpperCapKbps { get => _vbrCap; set => Set(ref _vbrCap, value); }
    private int _vbrCap = 4096;

    public string RawXml { get => _raw; set => Set(ref _raw, value); }
    private string _raw = "";

    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand ApplyCommand { get; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        Busy = true;
        try
        {
            using var client = _clientFactory();
            var x = await client.GetChannelXmlAsync(ChannelId, ct);
            RawXml = x.ToString();

            var ns = x.Root?.GetDefaultNamespace() ?? XNamespace.None;

            var vid = x.Root?.Element(ns + "Video") ?? x.Descendants(ns + "Video").FirstOrDefault();
            if (vid != null)
            {
                VideoCodecType = vid.Element(ns + "videoCodecType")?.Value ?? VideoCodecType;
                BitrateType = vid.Element(ns + "bitrateType")?.Value ?? BitrateType;

                if (int.TryParse(vid.Element(ns + "maxFrameRate")?.Value, out var fps)) MaxFrameRate = fps;
                if (int.TryParse(vid.Element(ns + "constantBitRate")?.Value, out var cbr)) ConstantBitRateKbps = cbr;
                if (int.TryParse(vid.Element(ns + "vbrUpperCap")?.Value, out var vcap)) VbrUpperCapKbps = vcap;
            }
        }
        finally
        {
            Busy = false;
        }
    }

    public async Task ApplyAsync(CancellationToken ct = default)
    {
        Busy = true;
        try
        {
            XDocument xml = string.IsNullOrWhiteSpace(RawXml)
                ? new XDocument(new XElement("StreamingChannel", new XElement("id", ChannelId), new XElement("Video")))
                : XDocument.Parse(RawXml);

            var ns = xml.Root?.GetDefaultNamespace() ?? XNamespace.None;

            var vid = xml.Root?.Element(ns + "Video") ?? xml.Descendants(ns + "Video").FirstOrDefault();
            if (vid is null)
            {
                vid = new XElement(ns + "Video");
                xml.Root?.Add(vid);
            }

            HikvisionXml.SetOrAdd(vid, ns + "videoCodecType", VideoCodecType);
            HikvisionXml.SetOrAdd(vid, ns + "bitrateType", BitrateType);
            HikvisionXml.SetOrAdd(vid, ns + "maxFrameRate", MaxFrameRate.ToString());

            if (BitrateType.Equals("CBR", StringComparison.OrdinalIgnoreCase))
                HikvisionXml.SetOrAdd(vid, ns + "constantBitRate", ConstantBitRateKbps.ToString());
            else
                HikvisionXml.SetOrAdd(vid, ns + "vbrUpperCap", VbrUpperCapKbps.ToString());

            using var client = _clientFactory();
            var resp = await client.SetChannelXmlAsync(ChannelId, xml, ct);
            RawXml = resp.ToString();
        }
        finally
        {
            Busy = false;
        }
    }
}