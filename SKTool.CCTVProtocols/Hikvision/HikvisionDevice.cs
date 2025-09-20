namespace SKTool.CCTVProtocols.Hikvision;

public class HikvisionDevice
{
    public HikvisionUrl Url { get; }
    public string Username { get; }
    public string Password { get; }

    public HikvisionDevice(HikvisionUrl url, string username, string password)
    {
        Url = url;
        Username = username;
        Password = password;
    }

    public override string ToString() => $"{Url} ({Username})";
}