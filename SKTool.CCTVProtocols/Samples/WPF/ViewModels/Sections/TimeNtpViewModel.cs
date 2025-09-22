using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using SKTool.CCTVProtocols.Hikvision;
using SKTool.CCTVProtocols.Hikvision.Models;

namespace SKTool.CCTVProtocols.Samples.WPF.ViewModels;

public sealed class TimeNtpViewModel : ViewModelBase
{
    private readonly Func<HikvisionClient> _clientFactory;

    public TimeNtpViewModel(Func<HikvisionClient> clientFactory)
    {
        _clientFactory = clientFactory;
        LoadCommand = new AsyncRelayCommand(ct => LoadAsync(ct), () => !Busy);
        SetManualNowCommand = new AsyncRelayCommand(ct => SetManualNowAsync(ct), () => !Busy);
        ApplyNtpCommand = new AsyncRelayCommand(ct => ApplyNtpAsync(ct), () => !Busy);
        SetSpainTzCommand = new AsyncRelayCommand(ct => SetSpainTzAsync(ct), () => !Busy);
    }

    private bool _busy;
    public bool Busy { get => _busy; set { Set(ref _busy, value); LoadCommand.RaiseCanExecuteChanged(); SetManualNowCommand.RaiseCanExecuteChanged(); ApplyNtpCommand.RaiseCanExecuteChanged(); SetSpainTzCommand.RaiseCanExecuteChanged(); } }

    public string TimeMode { get => _timeMode; set => Set(ref _timeMode, value); }
    private string _timeMode = "manual"; // manual or NTP

    public string DeviceTime { get => _deviceTime; set => Set(ref _deviceTime, value); }
    private string _deviceTime = "";

    public string TimeZone { get => _tz; set => Set(ref _tz, value); }
    private string _tz = "UTC+00:00";

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

    public async Task LoadAsync(CancellationToken ct = default)
    {
        Busy = true;
        try
        {
            using var client = _clientFactory();
            var t = await client.GetTimeAsync(ct);
            RawTimeXml = t.ToString();

            var cfg = TimeConfig.FromXml(t);
            TimeMode = cfg.TimeMode;
            DeviceTime = cfg.LocalTime;
            TimeZone = cfg.TimeZone;
            DstMode = cfg.DstEnabled ? "on" : "off";

            var ntp = await client.GetNtpServersAsync(ct);
            RawNtpXml = ntp.ToString();

            var s = NtpServerListXml.ReadFirst(ntp);
            NtpHost = s.HostName;
            NtpPort = s.Port;
            SyncIntervalMinutes = s.SyncIntervalMinutes;
        }
        finally
        {
            Busy = false;
        }
    }

    public async Task SetManualNowAsync(CancellationToken ct = default)
    {
        Busy = true;
        try
        {
            using var client = _clientFactory();
            var timeDoc = await client.GetTimeAsync(ct);
            var cfg = TimeConfig.FromXml(timeDoc);

            var resolvedTz = await ResolveTimeZoneAsync(client, TimeZone, ct);
            cfg.TimeMode = "manual";
            cfg.TimeZone = resolvedTz;
            cfg.DstEnabled = DstMode.Equals("on", StringComparison.OrdinalIgnoreCase);
            if (cfg.DstEnabled)
            {
                cfg.DstStartTime = "M3.5.0/02:00:00";
                cfg.DstEndTime = "M10.5.0/03:00:00";
                cfg.DstOffsetMinutes = 60;
            }
            cfg.LocalTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            cfg.ApplyTo(timeDoc);
            var resp = await client.SetTimeAsync(timeDoc, ct);
            RawTimeXml = resp.ToString();

            await LoadAsync(ct);
        }
        finally
        {
            Busy = false;
        }
    }

    public async Task ApplyNtpAsync(CancellationToken ct = default)
    {
        Busy = true;
        try
        {
            using var client = _clientFactory();

            var timeDoc = await client.GetTimeAsync(ct);
            var cfg = TimeConfig.FromXml(timeDoc);

            var resolvedTz = await ResolveTimeZoneAsync(client, TimeZone, ct);
            cfg.TimeMode = "NTP";
            cfg.TimeZone = resolvedTz;
            cfg.DstEnabled = DstMode.Equals("on", StringComparison.OrdinalIgnoreCase);
            if (cfg.DstEnabled)
            {
                cfg.DstStartTime = "M3.5.0/02:00:00";
                cfg.DstEndTime = "M10.5.0/03:00:00";
                cfg.DstOffsetMinutes = 60;
            }

            cfg.ApplyTo(timeDoc);
            var r1 = await client.SetTimeAsync(timeDoc, ct);
            RawTimeXml = r1.ToString();

            var ntpDoc = await client.GetNtpServersAsync(ct);
            var s = new NtpServer
            {
                HostName = NtpHost,
                Port = NtpPort,
                SyncIntervalMinutes = SyncIntervalMinutes,
                Enabled = true
            };
            NtpServerListXml.WriteFirst(ntpDoc, s);

            var r2 = await client.SetNtpServersAsync(ntpDoc, ct);
            RawNtpXml = r2.ToString();

            await LoadAsync(ct);
        }
        finally
        {
            Busy = false;
        }
    }

    public async Task SetSpainTzAsync(CancellationToken ct = default)
    {
        Busy = true;
        try
        {
            TimeZone = "UTC+01:00"; // CET base
            DstMode = "on";         // Enable DST
            await ApplyNtpAsync(ct);
        }
        finally
        {
            Busy = false;
        }
    }

    private static async Task<string> ResolveTimeZoneAsync(HikvisionClient client, string preferred, CancellationToken ct)
    {
        try
        {
            var caps = await client.GetTimeCapabilitiesAsync(ct);
            var list = new List<string>();
            foreach (var el in caps.Descendants())
            {
                var name = el.Name.LocalName.ToLowerInvariant();
                if (name is "timezone" or "timezoneitem" or "timezonevalue" or "name")
                {
                    var val = el.Value?.Trim();
                    if (!string.IsNullOrEmpty(val)) list.Add(val);
                }
            }
            if (list.Count == 0) return preferred;

            var exact = list.FirstOrDefault(s => string.Equals(s, preferred, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            if (preferred.Contains("+01:00", StringComparison.OrdinalIgnoreCase))
            {
                var cet = list.FirstOrDefault(s => s.Contains("CET", StringComparison.OrdinalIgnoreCase))
                       ?? list.FirstOrDefault(s => s.Contains("GMT+01:00", StringComparison.OrdinalIgnoreCase))
                       ?? list.FirstOrDefault(s => s.Contains("UTC+01:00", StringComparison.OrdinalIgnoreCase));
                if (cet != null) return cet;
            }

            return preferred;
        }
        catch
        {
            return preferred;
        }
    }
}