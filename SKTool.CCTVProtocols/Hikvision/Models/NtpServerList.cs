using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace SKTool.CCTVProtocols.Hikvision.Models;

public sealed class NtpServer
{
    public string Id { get; set; } = "1";
    public string HostName { get; set; } = "pool.ntp.org";
    public int Port { get; set; } = 123;
    public int SyncIntervalMinutes { get; set; } = 60;
    public bool Enabled { get; set; } = true;
}

public static class NtpServerListXml
{
    public static NtpServer ReadFirst(XDocument doc)
    {
        var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
        var server = doc.Root?.Element(ns + "NTPServer")
                    ?? doc.Root?.Element(ns + "NTPServers")?.Element(ns + "NTPServer")
                    ?? doc.Root?.Element(ns + "NTPServerList")?.Element(ns + "NTPServer");

        var s = new NtpServer();
        if (server is null) return s;

        s.Id = server.Element(ns + "id")?.Value ?? "1";
        s.HostName = server.Element(ns + "hostName")?.Value
                  ?? server.Element(ns + "ipV4Address")?.Value
                  ?? server.Element(ns + "ipAddress")?.Value
                  ?? s.HostName;

        if (int.TryParse(server.Element(ns + "portNo")?.Value, out var p)) s.Port = p;
        if (int.TryParse(server.Element(ns + "synchronizeInterval")?.Value, out var si)) s.SyncIntervalMinutes = si;
        s.Enabled = string.Equals(server.Element(ns + "enabled")?.Value, "true", System.StringComparison.OrdinalIgnoreCase);

        return s;
    }

    public static void WriteFirst(XDocument doc, NtpServer s)
    {
        var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

        var server = doc.Root?.Element(ns + "NTPServer")
                    ?? doc.Root?.Element(ns + "NTPServers")?.Element(ns + "NTPServer")
                    ?? doc.Root?.Element(ns + "NTPServerList")?.Element(ns + "NTPServer");

        if (doc.Root is null)
        {
            doc.Add(new XElement(ns + "NTPServerList"));
        }

        if (server is null)
        {
            // Prefer NTPServerList schema
            if (doc.Root!.Element(ns + "NTPServerList") == null && doc.Root!.Name.LocalName != "NTPServerList")
            {
                var list = new XElement(ns + "NTPServerList");
                doc.Root!.Add(list);
                server = new XElement(ns + "NTPServer");
                list.Add(server);
            }
            else
            {
                server = new XElement(ns + "NTPServer");
                doc.Root!.Add(server);
            }
        }

        if (server.Element(ns + "id") is null)
            server.Add(new XElement(ns + "id", string.IsNullOrEmpty(s.Id) ? "1" : s.Id));

        HikvisionXml.SetOrAdd(server, ns + "addressingFormatType", "domain");
        HikvisionXml.SetOrAdd(server, ns + "hostName", s.HostName);
        HikvisionXml.SetOrAdd(server, ns + "portNo", s.Port.ToString());
        HikvisionXml.SetOrAdd(server, ns + "synchronizeInterval", s.SyncIntervalMinutes.ToString());
        HikvisionXml.SetOrAdd(server, ns + "enabled", s.Enabled ? "true" : "false");
    }
}