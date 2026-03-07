using System.Globalization;

namespace Paw.Core.Utils;

[AttributeUsage(AttributeTargets.Assembly)]
public class BuildDateAttribute : Attribute
{
    public DateTime DateTime { get; }

    public BuildDateAttribute(string value) // Value is set in csproj
    {
        DateTime = DateTime.ParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None);
    }
}