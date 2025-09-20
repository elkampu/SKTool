using SKTool.CCTVProtocols.Hikvision;

namespace SKTool.CCTVProtocols.Samples.WPF.Models;

public sealed class DeviceModel
{
    public string Host { get; set; } = "192.168.2.170";
    public int Port { get; set; } = 80;
    public bool UseHttps { get; set; } = false;
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "Securitas4813"; // Replace for your environment
    public bool PreferDigest { get; set; } = true;

    public HikvisionDevice ToDevice()
    {
        var url = new HikvisionUrl(Host, Port, UseHttps);
        return new HikvisionDevice(url, Username, Password);
    }
}