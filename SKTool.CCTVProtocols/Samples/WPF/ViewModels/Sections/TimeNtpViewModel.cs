using System;
using System.Threading.Tasks;
using System.Xml.Linq;
using SKTool.CCTVProtocols.Hikvision;

namespace SKTool.CCTVProtocols.Samples.WPF.ViewModels;

public sealed class TimeNtpViewModel : ViewModelBase
{
    private readonly Func<HikvisionClient> _clientFactory;

    public TimeNtpViewModel(Func<HikvisionClient> clientFactory)
    {
        _clientFactory = clientFactory;
        LoadCommand = new AsyncRelayCommand(LoadAsync, () => !Busy);
        SetManualNowCommand = new AsyncRelayCommand(SetManualNowAsync, () => !Busy);
        ApplyNtpCommand = new AsyncRelayCommand(ApplyNtpAsync, () => !Busy);
        SetSpainTzCommand = new AsyncRelayCommand(SetSpainTzAsync, () => !Busy);
    }

    private bool _busy;
    public bool Busy { get => _busy; set { Set(ref _busy, value); LoadCommand.RaiseCanExecuteChanged(); SetManualNowCommand.RaiseCanExecuteChanged(); ApplyNtpCommand.RaiseCanExecuteChanged(); SetSpainTzCommand.RaiseCanExecuteChanged(); } }

    public string TimeMode { get => _timeMode; set => Set(ref _timeMode, value); }
    private string _timeMode = "manual"; // manual or NTP

    public string DeviceTime { get => _deviceTime; set => Set(ref _deviceTime, value); }
    private string _deviceTime = "";

    public string TimeZone { get => _tz; set => Set(ref _tz, value); }
    private string _tz = "UTC+00:00";

    // UI flag, mapped to device schemas
    public string DstMode { get => _dst; set => Set(ref _dst, value); }
    private string _dst = "off"; // "on" or "off"

    public string NtpHost { get => _ntpHost; set => Set(ref _ntpHost, value); }
    private string _ntpHost = "pool.ntp.org";

    public int NtpPort { get => _ntpPort; set => Set(ref _ntpPort, value); }
    private int _ntpPort = 123;

    public int SyncIntervalMinutes { get => _syncMin; set => Set(ref _syncMin, value); }
    private int _syncMin = 60;

    public string RawTimeXml { get => _rawTime; set => Set(ref _rawTime, value); }
    private string _rawTime = "";

    public string RawNtpXml { get => _rawNtp; set => Set(ref _rawNtp, value); }
    private string _rawNtp = "";

    public IReadOnlyList<string> AvailableTimeZones { get; } =
        new[]
        {
        "UTC-12:00","UTC-11:00","UTC-10:00","UTC-09:00","UTC-08:00","UTC-07:00","UTC-06:00","UTC-05:00","UTC-04:00","UTC-03:30",
        "UTC-03:00","UTC-02:00","UTC-01:00","UTC+00:00","UTC+01:00","UTC+02:00","UTC+03:00","UTC+03:30","UTC+04:00","UTC+04:30",
        "UTC+05:00","UTC+05:30","UTC+05:45","UTC+06:00","UTC+06:30","UTC+07:00","UTC+08:00","UTC+08:30","UTC+09:00","UTC+09:30",
        "UTC+10:00","UTC+10:30","UTC+11:00","UTC+12:00","UTC+13:00","UTC+14:00"
        };

    public AsyncRelayCommand LoadCommand { get; }
    public AsyncRelayCommand SetManualNowCommand { get; }
    public AsyncRelayCommand ApplyNtpCommand { get; }
    public AsyncRelayCommand SetSpainTzCommand { get; }

    public async Task LoadAsync()
    {
        Busy = true;
        try
        {
            using var client = _clientFactory();
            var t = await client.GetTimeAsync();
            RawTimeXml = t.ToString();

            var tns = t.Root?.GetDefaultNamespace() ?? XNamespace.None;

            TimeMode = t.Root?.Element(tns + "timeMode")?.Value ?? TimeMode;
            DeviceTime = t.Root?.Element(tns + "localTime")?.Value ?? DeviceTime;
            TimeZone = t.Root?.Element(tns + "timeZone")?.Value ?? TimeZone;

            // Read DST in either schema
            var ds = t.Root?.Element(tns + "daylightSaving");
            if (ds != null)
            {
                var enabled = ds.Element(tns + "enabled")?.Value?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
                DstMode = enabled ? "on" : "off";
            }
            else
            {
                var dstEl = t.Root?.Element(tns + "dstMode");
                if (dstEl != null)
                    DstMode = dstEl.Value;
            }

            var ntp = await client.GetNtpServersAsync();
            RawNtpXml = ntp.ToString();

            var nns = ntp.Root?.GetDefaultNamespace() ?? XNamespace.None;
            var first = ntp.Root?.Element(nns + "NTPServer")
                        ?? ntp.Root?.Element(nns + "NTPServers")?.Element(nns + "NTPServer");
            if (first is not null)
            {
                NtpHost = first.Element(nns + "hostName")?.Value
                          ?? first.Element(nns + "ipV4Address")?.Value
                          ?? first.Element(nns + "ipAddress")?.Value
                          ?? NtpHost;
                if (int.TryParse(first.Element(nns + "portNo")?.Value, out var p)) NtpPort = p;
                if (int.TryParse(first.Element(nns + "synchronizeInterval")?.Value, out var s)) SyncIntervalMinutes = s;
            }
        }
        finally
        {
            Busy = false;
        }
    }

    public async Task SetManualNowAsync()
    {
        Busy = true;
        try
        {
            using var client = _clientFactory();
            var timeDoc = await client.GetTimeAsync();
            var ns = timeDoc.Root?.GetDefaultNamespace() ?? XNamespace.None;

            SetOrAddNs(timeDoc.Root!, ns + "timeMode", "manual");
            SetOrAddNs(timeDoc.Root!, ns + "timeZone", TimeZone);

            ApplyDstToTimeDocument(timeDoc.Root!, ns, DstMode);

            // Some models expect UTC string, others local. Using UTC string is commonly accepted.
            SetOrAddNs(timeDoc.Root!, ns + "localTime", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));

            var resp = await client.SetTimeAsync(timeDoc);
            RawTimeXml = resp.ToString();

            await LoadAsync();
        }
        finally
        {
            Busy = false;
        }
    }

    public async Task ApplyNtpAsync()
    {
        Busy = true;
        try
        {
            using var client = _clientFactory();

            // Update time mode/TZ/DST using device's current XML (preserve namespace)
            var timeDoc = await client.GetTimeAsync();
            var tns = timeDoc.Root?.GetDefaultNamespace() ?? XNamespace.None;

            SetOrAddNs(timeDoc.Root!, tns + "timeMode", "NTP");
            SetOrAddNs(timeDoc.Root!, tns + "timeZone", TimeZone);

            ApplyDstToTimeDocument(timeDoc.Root!, tns, DstMode);

            var r1 = await client.SetTimeAsync(timeDoc);
            RawTimeXml = r1.ToString();

            // Update NTP server list preserving namespace
            var ntpDoc = await client.GetNtpServersAsync();
            var nns = ntpDoc.Root?.GetDefaultNamespace() ?? XNamespace.None;

            var server = ntpDoc.Root?.Element(nns + "NTPServer")
                         ?? ntpDoc.Root?.Element(nns + "NTPServers")?.Element(nns + "NTPServer");

            if (server is null)
            {
                if (ntpDoc.Root is null)
                {
                    ntpDoc.Add(new XElement(nns + "NTPServerList"));
                }
                server = new XElement(nns + "NTPServer");
                ntpDoc.Root!.Add(server);
            }

            SetOrAddNs(server, nns + "addressingFormatType", "domain");
            SetOrAddNs(server, nns + "hostName", NtpHost);
            SetOrAddNs(server, nns + "portNo", NtpPort.ToString());
            SetOrAddNs(server, nns + "synchronizeInterval", SyncIntervalMinutes.ToString());
            SetOrAddNs(server, nns + "enabled", "true");

            var r2 = await client.SetNtpServersAsync(ntpDoc);
            RawNtpXml = r2.ToString();

            await LoadAsync();
        }
        finally
        {
            Busy = false;
        }
    }

    // Set Spain: CET (UTC+01:00) with DST on and EU rules
    public async Task SetSpainTzAsync()
    {
        Busy = true;
        try
        {
            TimeZone = "UTC+01:00"; // CET base
            DstMode = "on";         // Enable DST
            await ApplyNtpAsync();
        }
        finally
        {
            Busy = false;
        }
    }

    // Applies DST using the correct schema for the device:
    // - ver20 (xmlns contains "ver20"): <daylightSaving><enabled>true</enabled><startTime>...</startTime><endTime>...</endTime><offset>60</offset></daylightSaving>
    // - legacy ver10: <dstMode>on|off</dstMode>
    private static void ApplyDstToTimeDocument(XElement timeRoot, XNamespace ns, string dstMode)
    {
        var enable = dstMode.Equals("on", StringComparison.OrdinalIgnoreCase);

        var isVer20 = (timeRoot.GetDefaultNamespace().NamespaceName?.IndexOf("ver20", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
        var hasDaylightSaving = timeRoot.Element(ns + "daylightSaving") is not null;

        if (isVer20 || hasDaylightSaving)
        {
            var ds = timeRoot.Element(ns + "daylightSaving");
            if (ds is null)
            {
                ds = new XElement(ns + "daylightSaving");
                timeRoot.Add(ds);
            }

            SetOrAddNs(ds, ns + "enabled", enable ? "true" : "false");

            // EU (Spain) DST: last Sunday of March 02:00 to last Sunday of October 03:00, offset 60
            // Many Hikvision firmwares accept "M<month>.<week>.<day>/<HH:mm:ss>" format:
            //   week 5 = last week, day 0 = Sunday
            SetOrAddNs(ds, ns + "startTime", "M3.5.0/02:00:00");
            SetOrAddNs(ds, ns + "endTime", "M10.5.0/03:00:00");
            SetOrAddNs(ds, ns + "offset", "60");

            // Remove legacy node if present to avoid conflicts on some models
            var legacy = timeRoot.Element(ns + "dstMode");
            legacy?.Remove();
        }
        else
        {
            // Legacy schema: keep it simple
            SetOrAddNs(timeRoot, ns + "dstMode", enable ? "on" : "off");

            // If device also had a 'daylightSaving' node, drop it to avoid "invalid content"
            var ds = timeRoot.Element(ns + "daylightSaving");
            ds?.Remove();
        }
    }

    private static void SetOrAdd(XElement parent, string name, string value)
    {
        var el = parent.Element(name);
        if (el is null) parent.Add(el = new XElement(name));
        el.Value = value;
    }

    private static void SetOrAddNs(XElement parent, XName name, string value)
    {
        var el = parent.Element(name);
        if (el is null) parent.Add(el = new XElement(name));
        el.Value = value;
    }
}