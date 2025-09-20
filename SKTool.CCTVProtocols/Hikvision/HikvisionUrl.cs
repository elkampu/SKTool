using System;

namespace SKTool.CCTVProtocols.Hikvision;

public sealed class HikvisionUrl
{
    public string Host { get; }
    public int Port { get; }
    public bool UseHttps { get; }

    public HikvisionUrl(string host, int? port = null, bool useHttps = true)
    {
        Host = host.Trim();
        UseHttps = useHttps;
        Port = port ?? (useHttps ? HikvisionConstants.DefaultHttpsPort : HikvisionConstants.DefaultHttpPort);
    }

    public Uri Build(string path)
    {
        var scheme = UseHttps ? "https" : "http";
        if (!path.StartsWith("/")) path = "/" + path;
        return new Uri($"{scheme}://{Host}:{Port}{path}");
    }

    public override string ToString() => $"{(UseHttps ? "https" : "http")}://{Host}:{Port}";
}