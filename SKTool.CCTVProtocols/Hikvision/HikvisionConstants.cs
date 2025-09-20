using System;

namespace SKTool.CCTVProtocols.Hikvision;

public static class HikvisionConstants
{
    // System
    public const string SystemDeviceInfo = "/ISAPI/System/deviceInfo";
    public const string SystemTime = "/ISAPI/System/time";
    public const string SystemTimeNtpServers = "/ISAPI/System/time/ntpServers";
    public const string SystemReboot = "/ISAPI/System/reboot";

    // Network
    public static string NetworkInterface(int id) => $"/ISAPI/System/Network/interfaces/{id}";

    // Streaming
    public const string StreamingChannels = "/ISAPI/Streaming/channels";
    public static string StreamingChannel(int channelId) => $"/ISAPI/Streaming/channels/{channelId}";
    public static string StreamingChannelPicture(int channelId) => $"/ISAPI/Streaming/channels/{channelId}/picture";

    // Timeouts
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    // Default ports
    public const int DefaultHttpPort = 80;
    public const int DefaultHttpsPort = 443;

    // Media types
    public const string XmlMediaType = "application/xml";
    public const string JpegMediaType = "image/jpeg";
}