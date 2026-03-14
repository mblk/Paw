using System.Xml.Linq;

namespace Paw.BindingGen.Extensions;

public static class XElementExtensions
{
    public static string MandatoryElementValue(this XElement element, XName name)
    {
        return element.Element(name)?.Value ?? throw new InvalidOperationException($"missing mandatory element '${name}' on {element}");
    }

    public static string? OptionalElementValue(this XElement element, XName name)
    {
        return element.Element(name)?.Value;
    }

    public static string MandatoryAttributeValue(this XElement element, XName name)
    {
        return element.Attribute(name)?.Value ?? throw new InvalidOperationException($"missing mandatory attribute '${name}' on {element}");
    }

    public static string? OptionalAttributeValue(this XElement element, XName name)
    {
        return element.Attribute(name)?.Value;
    }
}
