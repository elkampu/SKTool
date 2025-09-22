using SKTool.CCTVProtocols.Hikvision;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SKTool.CCTVProtocols.Samples.WPF.ViewModels;

public sealed class NetworkViewModel : ViewModelBase
{
    private readonly Func<HikvisionClient> _clientFactory;

    public NetworkViewModel(Func<HikvisionClient> clientFactory)
    {
        _clientFactory = clientFactory;
        LoadCommand = new AsyncRelayCommand(ct => LoadAsync(ct), () => !Busy);
        ApplyCommand = new AsyncRelayCommand(ct => ApplyAsync(ct), () => !Busy);
    }

    private bool _busy;
    public bool Busy { get => _busy; set { Set(ref _busy, value); LoadCommand.RaiseCanExecuteChanged(); ApplyCommand.RaiseCanExecuteChanged(); } }

    public int InterfaceId { get => _interfaceId; set => Set(ref _interfaceId, value); }
    private int _interfaceId = 1;

    public bool UseDhcp { get => _useDhcp; set => Set(ref _useDhcp, value); }
    private bool _useDhcp;

    public string IPv4Address { get => _ip; set => Set(ref _ip, value); }
    private string _ip = "";

    public string SubnetMask { get => _mask; set => Set(ref _mask, value); }
    private string _mask = "";

    public string Gateway { get => _gw; set => Set(ref _gw, value); }
    private string _gw = "";

    public string PrimaryDNS { get => _dns1; set => Set(ref _dns1, value); }
    private string _dns1 = "";

    public string SecondaryDNS { get => _dns2; set => Set(ref _dns2, value); }
    private string _dns2 = "";

    public string RawXml { get => _rawXml; set => Set(ref _rawXml, value); }
    private string _rawXml = "";

    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand ApplyCommand { get; }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        Busy = true;
        try
        {
            using var client = _clientFactory();
            var x = await client.GetNetworkInterfaceAsync(InterfaceId, ct);
            RawXml = x.ToString();

            var ns = x.Root?.GetDefaultNamespace() ?? XNamespace.None;

            var ipEl = x.Root?.Element(ns + "IPAddress");
            var addrType = ipEl?.Element(ns + "addressingType")?.Value ?? "static";
            UseDhcp = addrType.Equals("dhcp", StringComparison.OrdinalIgnoreCase);

            IPv4Address = ipEl?.Element(ns + "ipAddress")?.Value ?? "";
            SubnetMask = ipEl?.Element(ns + "subnetMask")?.Value ?? "";
            Gateway = ipEl?.Element(ns + "DefaultGateway")?.Element(ns + "ipAddress")?.Value ?? "";
            PrimaryDNS = ipEl?.Element(ns + "PrimaryDNS")?.Element(ns + "ipAddress")?.Value ?? "";
            SecondaryDNS = ipEl?.Element(ns + "SecondaryDNS")?.Element(ns + "ipAddress")?.Value ?? "";
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
            using var client = _clientFactory();

            var current = await client.GetNetworkInterfaceAsync(InterfaceId, ct);
            var ns = current.Root?.GetDefaultNamespace() ?? XNamespace.None;
            var currentIp = current.Root?.Element(ns + "IPAddress");

            var ipDoc = new XDocument(new XElement(ns + "IPAddress"));
            var ip = ipDoc.Root!;

            if (currentIp != null)
            {
                foreach (var attr in currentIp.Attributes())
                {
                    ip.SetAttributeValue(attr.Name, attr.Value);
                }
            }

            HikvisionXml.SetOrAdd(ip, ns + "ipVersion", "v4");
            HikvisionXml.SetOrAdd(ip, ns + "addressingType", UseDhcp ? "dhcp" : "static");

            if (!UseDhcp)
            {
                HikvisionXml.SetOrAdd(ip, ns + "ipAddress", IPv4Address);
                HikvisionXml.SetOrAdd(ip, ns + "subnetMask", SubnetMask);
                HikvisionXml.EnsurePath(ip, ns + "DefaultGateway", ns + "ipAddress", Gateway);
                HikvisionXml.EnsurePath(ip, ns + "PrimaryDNS", ns + "ipAddress", PrimaryDNS);
                HikvisionXml.EnsurePath(ip, ns + "SecondaryDNS", ns + "ipAddress", SecondaryDNS);
            }
            else
            {
                ip.Element(ns + "ipAddress")?.Remove();
                ip.Element(ns + "subnetMask")?.Remove();
                ip.Element(ns + "DefaultGateway")?.Remove();
                ip.Element(ns + "PrimaryDNS")?.Remove();
                ip.Element(ns + "SecondaryDNS")?.Remove();
            }

            var resp = await client.SetNetworkInterfaceIpAddressAsync(InterfaceId, ipDoc, ct);
            RawXml = resp.ToString();

            var refreshed = await client.GetNetworkInterfaceAsync(InterfaceId, ct);
            RawXml = refreshed.ToString();
        }
        finally
        {
            Busy = false;
        }
    }
}