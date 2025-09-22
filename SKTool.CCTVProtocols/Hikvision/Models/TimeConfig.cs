using System;
using System.Xml.Linq;

namespace SKTool.CCTVProtocols.Hikvision.Models;

public sealed class TimeConfig
{
    public string TimeMode { get; set; } = "manual"; // manual | NTP
    public string LocalTime { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
    public string TimeZone { get; set; } = "UTC+00:00";
    public bool DstEnabled { get; set; } = false;
    public string? DstStartTime { get; set; } // e.g., M3.5.0/02:00:00
    public string? DstEndTime { get; set; }   // e.g., M10.5.0/03:00:00
    public int? DstOffsetMinutes { get; set; } // e.g., 60

    public static TimeConfig FromXml(XDocument doc)
    {
        var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
        var cfg = new TimeConfig
        {
            TimeMode = doc.Root?.Element(ns + "timeMode")?.Value ?? "manual",
            LocalTime = doc.Root?.Element(ns + "localTime")?.Value ?? DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            TimeZone = doc.Root?.Element(ns + "timeZone")?.Value ?? "UTC+00:00"
        };

        var ds = doc.Root?.Element(ns + "daylightSaving");
        if (ds != null)
        {
            cfg.DstEnabled = string.Equals(ds.Element(ns + "enabled")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            cfg.DstStartTime = ds.Element(ns + "startTime")?.Value;
            cfg.DstEndTime = ds.Element(ns + "endTime")?.Value;
            if (int.TryParse(ds.Element(ns + "offset")?.Value, out var off)) cfg.DstOffsetMinutes = off;
        }
        else
        {
            var dstMode = doc.Root?.Element(ns + "dstMode")?.Value;
            cfg.DstEnabled = string.Equals(dstMode, "on", StringComparison.OrdinalIgnoreCase);
        }

        return cfg;
    }

    public void ApplyTo(XDocument doc)
    {
        var root = doc.Root ?? throw new InvalidOperationException("Time XML missing root.");
        var ns = root.GetDefaultNamespace();

        HikvisionXml.SetOrAdd(root, ns + "timeMode", TimeMode);
        HikvisionXml.SetOrAdd(root, ns + "timeZone", TimeZone);

        // legacy or ver20
        var isVer20 = HikvisionXml.IsVer20(root) || root.Element(ns + "daylightSaving") is not null;
        if (isVer20)
        {
            var ds = HikvisionXml.Ensure(root, ns + "daylightSaving");
            HikvisionXml.SetOrAdd(ds, ns + "enabled", DstEnabled ? "true" : "false");
            if (DstEnabled)
            {
                if (!string.IsNullOrWhiteSpace(DstStartTime)) HikvisionXml.SetOrAdd(ds, ns + "startTime", DstStartTime);
                if (!string.IsNullOrWhiteSpace(DstEndTime)) HikvisionXml.SetOrAdd(ds, ns + "endTime", DstEndTime);
                if (DstOffsetMinutes.HasValue) HikvisionXml.SetOrAdd(ds, ns + "offset", DstOffsetMinutes.Value.ToString());
            }
            else
            {
                ds.Element(ns + "startTime")?.Remove();
                ds.Element(ns + "endTime")?.Remove();
                ds.Element(ns + "offset")?.Remove();
            }
            root.Element(ns + "dstMode")?.Remove();
        }
        else
        {
            HikvisionXml.SetOrAdd(root, ns + "dstMode", DstEnabled ? "on" : "off");
            root.Element(ns + "daylightSaving")?.Remove();
        }

        if (string.Equals(TimeMode, "NTP", StringComparison.OrdinalIgnoreCase))
        {
            root.Element(ns + "localTime")?.Remove();
        }
        else
        {
            HikvisionXml.SetOrAdd(root, ns + "localTime", LocalTime);
        }
    }
}