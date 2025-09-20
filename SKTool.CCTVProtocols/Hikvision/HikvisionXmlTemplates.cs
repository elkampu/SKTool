namespace SKTool.CCTVProtocols.Hikvision;

public static class HikvisionXmlTemplates
{
    public const string SetTimeTemplate = """
        <Time>
            <timeMode>manual</timeMode>
            <localTime>2025-01-01T12:00:00Z</localTime>
            <timeZone>UTC+00:00</timeZone>
            <dstMode>off</dstMode>
        </Time>
        """;

    public const string SetTimeNtpTemplate = """
        <Time>
            <timeMode>NTP</timeMode>
            <timeZone>UTC+00:00</timeZone>
            <dstMode>off</dstMode>
        </Time>
        """;

    public const string NtpServersTemplate = """
        <NTPServerList>
            <NTPServer>
                <id>1</id>
                <addressingFormatType>domain</addressingFormatType>
                <hostName>pool.ntp.org</hostName>
                <portNo>123</portNo>
                <synchronizeInterval>60</synchronizeInterval>
                <enabled>true</enabled>
            </NTPServer>
        </NTPServerList>
        """;

    public const string RebootTemplate = """
        <reboot>
            <reason>Manual</reason>
        </reboot>
        """;

}