using Paw.BindingGen.Extensions;
using System.Globalization;
using System.Xml.Linq;

namespace Paw.BindingGen;

internal class GlSpecParser
{
    private readonly FileInfo _specFile;

    private readonly Dictionary<string, string> _types = [];
    private readonly Dictionary<string, string> _kinds = [];
    private readonly Dictionary<string, GlSpec.EnumSpec> _enums = [];
    private readonly Dictionary<string, GlSpec.CommandSpec> _commands = [];
    private readonly Dictionary<string, GlSpec.FeatureSpec> _features = [];

    public GlSpecParser(FileInfo specFile)
    {
        _specFile = specFile;
    }

    public GlSpec Parse()
    {
        var doc = XDocument.Load(_specFile.FullName, LoadOptions.None);

        var registry = doc.Element("registry") ?? throw new InvalidOperationException("missing registry");

        foreach (var types in registry.Elements("types"))
        {
            foreach (var type in types.Elements("type"))
            {
                string? nameAttr = type.Attribute("name")?.Value;
                string? nameTag = type.Element("name")?.Value;
                string name = nameAttr ?? nameTag ?? throw new InvalidOperationException($"missing name on type: {type}");
                string content = type.Value;

                _types.Add(name, content);
                //Console.WriteLine($"Type: {name}: \"{content}\"");
            }
        }

        foreach (var kinds in registry.Elements("kinds"))
        {
            foreach (var kind in kinds.Elements("kind"))
            {
                string name = kind.MandatoryAttributeValue("name");
                string desc = kind.MandatoryAttributeValue("desc");

                _kinds.Add(name, desc);
                //Console.WriteLine($"Kind {name}: {desc}");
            }
        }

        foreach (var enums in registry.Elements("enums"))
        {
            foreach (var @enum in enums.Elements("enum"))
            {
                string name = @enum.MandatoryAttributeValue("name");
                string value = @enum.MandatoryAttributeValue("value");
                string? group = @enum.OptionalAttributeValue("group");
                string? api = @enum.OptionalAttributeValue("api");
                string? type = @enum.OptionalAttributeValue("type");
                string? alias = @enum.OptionalAttributeValue("alias");

                if (!MatchApi(api))
                    continue;

                //Console.WriteLine($"enum {name} {group} {value}");
                _enums.Add(name, new GlSpec.EnumSpec(name, value, group, type, alias));
            }
        }

        foreach (var commands in registry.Elements("commands"))
        {
            foreach (var command in commands.Elements("command"))
            {
                var proto = command.Element("proto") ?? throw new InvalidOperationException($"missing proto on command: {command}");

                string? group = proto.OptionalAttributeValue("group");
                string name = proto.MandatoryElementValue("name");
                string? ptype = proto.OptionalElementValue("ptype"); // ref to type
                string? alias = command.Element("alias")?.Attribute("name")?.Value;
                string? vecEquiv = command.Element("vecequiv")?.Attribute("name")?.Value;
                int returnPointerCount = proto.Value.ToCharArray().Count('*');

                var paramSpecs = new List<GlSpec.ParamSpec>();

                foreach (var param in command.Elements("param"))
                {
                    string? paramGroup = param.OptionalAttributeValue("group");
                    string? paramLen = param.OptionalAttributeValue("len");
                    string? paramClass = param.OptionalAttributeValue("class"); // shader/buffer/etc
                    string paramName = param.MandatoryElementValue("name");
                    string? paramType = param.OptionalElementValue("ptype"); // ref
                    int paramPointerCount = param.Value.ToCharArray().Count('*');

                    //Console.WriteLine($"  param '{paramName}'");
                    paramSpecs.Add(new GlSpec.ParamSpec(paramName, paramType, paramGroup, paramLen, paramClass, paramPointerCount));
                }

                //Console.WriteLine($"Command '{name}'");
                _commands.Add(name, new GlSpec.CommandSpec(name, group, ptype, alias, vecEquiv, returnPointerCount, paramSpecs));
            }
        }

        foreach (var feature in registry.Elements("feature"))
        {
            string api = feature.MandatoryAttributeValue("api");
            string name = feature.MandatoryAttributeValue("name");
            string number = feature.MandatoryAttributeValue("number");

            (int major, int minor) = ParseMajorMinorNumber(number);

            if (!MatchApi(api))
                continue;

            List<string> requiredEnums = [];
            List<string> requiredCommands = [];
            List<string> removedEnums = [];
            List<string> removedCommands = [];

            foreach (var require in feature.Elements("require"))
            {
                string? profile = require.OptionalAttributeValue("profile");

                if (!MatchProfile(profile))
                    continue;


                var enums = require.Elements("enum").Select(x => x.MandatoryAttributeValue("name"));
                var commands = require.Elements("command").Select(x => x.MandatoryAttributeValue("name"));

                requiredEnums.AddRange(enums);
                requiredCommands.AddRange(commands);
            }

            foreach (var remove in feature.Elements("remove"))
            {
                string? profile = remove.OptionalAttributeValue("profile");

                if (!MatchProfile(profile))
                    continue;

                var enums = remove.Elements("enum").Select(x => x.MandatoryAttributeValue("name"));
                var commands = remove.Elements("command").Select(x => x.MandatoryAttributeValue("name"));

                removedEnums.AddRange(enums);
                removedCommands.AddRange(commands);
            }

            //Console.WriteLine($"Feature {api} {name} {number}");
            _features.Add(name, new GlSpec.FeatureSpec(name, major, minor, requiredEnums, requiredCommands, removedEnums, removedCommands));
        }

        return new GlSpec(_types, _kinds, _enums, _commands, _features);
    }

    private static bool MatchApi(string? api)
    {
        return string.IsNullOrWhiteSpace(api) || string.Equals(api, "gl", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchProfile(string? profile)
    {
        return string.IsNullOrWhiteSpace(profile) || string.Equals(profile, "core", StringComparison.OrdinalIgnoreCase);
    }

    private static (int, int) ParseMajorMinorNumber(string number)
    {
        string[] parts = number.Split('.');

        if (parts.Length != 2)
            throw new InvalidOperationException($"Failed to parse major minor number: '{number}'");

        if (!Int32.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int major))
            throw new InvalidOperationException($"Failed to parse major minor number: '{number}'");

        if (!Int32.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int minor))
            throw new InvalidOperationException($"Failed to parse major minor number: '{number}'");

        return (major, minor);
    }
}
