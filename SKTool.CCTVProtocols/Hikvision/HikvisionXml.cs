using System.Xml.Linq;

namespace SKTool.CCTVProtocols.Hikvision;

public static class HikvisionXml
{
    public static XNamespace NsOf(XDocument doc) => doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

    public static XElement Ensure(XElement parent, XName name)
    {
        var el = parent.Element(name);
        if (el is null)
        {
            el = new XElement(name);
            parent.Add(el);
        }
        return el;
    }

    public static XElement SetOrAdd(XElement parent, XName name, string value)
    {
        var el = Ensure(parent, name);
        el.Value = value;
        return el;
    }

    public static void RemoveIfExists(XElement parent, XName name)
    {
        parent.Element(name)?.Remove();
    }

    public static bool IsVer20(XElement root)
    {
        var nsName = root.GetDefaultNamespace().NamespaceName ?? "";
        return nsName.Contains("ver20", System.StringComparison.OrdinalIgnoreCase);
    }
}