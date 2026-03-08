using System.ComponentModel.Design.Serialization;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Xml.Linq;

namespace Paw.BindingGen;

public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("hello");

        var glSpecXml = new FileInfo(@"C:\workspace\repos\OpenGL-Registry\xml\gl.xml");
        var outputDir = new DirectoryInfo(@"C:\workspace\repos\Paw\src\Paw.Core\Engine\");

        var parser = new GlSpecParser(glSpecXml);
        var glSpec = parser.Parse();

        var generator = new GlBindingGenerator(glSpec, outputDir);
        generator.Generate();

        Console.WriteLine("bye");
    }
}

internal class GlBindingGenerator
{
    private const string _fallbackEnumGroupName = "Enum";
    private const string _indent = "    ";
    private const string _className = "GL2";
    private const string _namespace = "Paw.Core.Engine";

    private readonly GlSpec _spec;
    private readonly DirectoryInfo _outputDir;

    public GlBindingGenerator(GlSpec spec, DirectoryInfo outputDir)
    {
        _spec = spec;
        _outputDir = outputDir;
    }

    public void Generate()
    {
        var (enumNames, commandNames) = GetEnumsAndCommandsToGenerate();

        ResolveNamingCollisions();

        string enumsCode = GenerateEnums(enumNames);
        string commandsCode = GenerateCommands(commandNames);

        var enumsCodeFile = new FileInfo(Path.Combine(_outputDir.FullName, $"{_className}.Enums.cs"));
        var commandsCodeFile = new FileInfo(Path.Combine(_outputDir.FullName, $"{_className}.Commands.cs"));

        File.WriteAllText(enumsCodeFile.FullName, enumsCode);
        File.WriteAllText(commandsCodeFile.FullName, commandsCode);

        //
        Console.WriteLine($"...");
    }

    private void ResolveNamingCollisions()
    {
        // ...
    }
    

    private string GenerateEnums(IReadOnlyList<string> enumNames)
    {
        var groups = new Dictionary<string, List<(string, string)>>
        {
            { _fallbackEnumGroupName, new List<(string, string)>() }
        };

        foreach (var enumName in enumNames)
        {
            var enumSpec = _spec.Enums[enumName];

            foreach (string groupName in SplitEnumGroups(enumSpec.Group))
            {
                if (!groups.TryGetValue(groupName, out var values))
                    groups.Add(groupName, values = new List<(string, string)>());

                values.Add((enumSpec.Name, enumSpec.Value));
            }
        }

        var sb = new StringBuilder();
        WriteFileStart(sb);
        WriteNamespace(sb);
        WriteClassStart(sb);

        foreach (var group in groups)
        {
            sb.AppendLine($"{_indent}public enum {group.Key} : uint");
            sb.AppendLine($"{_indent}{{");

            foreach (var value in group.Value)
            {
                if (!CheckEnumValue(value.Item2))
                {
                    Console.WriteLine($"Skipping enum value '{value.Item1}' = '{value.Item2}'");
                    continue;
                }

                sb.AppendLine($"{_indent}{_indent}{value.Item1} = {value.Item2},");
            }

            sb.AppendLine($"{_indent}}}");
            sb.AppendLine();
        }

        WriteClassEnd(sb);

        return sb.ToString();
    }

    private static IEnumerable<string> SplitEnumGroups(string? group)
    {
        if (string.IsNullOrWhiteSpace(group))
            return [_fallbackEnumGroupName];

        return group.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
    }

    private static bool CheckEnumValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (value.StartsWith("0x"))
        {
            return UInt32.TryParse(value[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint _);
        }
        else
        {
            return UInt32.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out uint _);
        }
    }

    private string GenerateCommands(IReadOnlyList<string> commandNames)
    {
        var delegates = new List<string>();
        var loads = new List<string>();
        var wrappers = new List<string>();

        foreach (var commandName in commandNames)
        {
            var commandSpec = _spec.Commands[commandName];

            string delegateName = GetDelegateName(commandSpec.Name);
            string returnType = ResolveType(commandSpec.ReturnType, commandSpec.Group, "void");

            var paramTypes = new List<string>();
            var paramTypesAndNames = new List<(string,string)>();
            foreach (var paramSpec in commandSpec.Params)
            {
                string paramType = ResolveType(paramSpec.Type, paramSpec.Group, "void");

                if (paramSpec.IsPointer)
                {
                    paramType = $"{paramType}*";
                }

                paramTypes.Add(paramType);
                paramTypesAndNames.Add((paramType, SanitizeParameterName(paramSpec.Name)));
            }

            var genericArgs = new List<string>();
            genericArgs.AddRange(paramTypes);
            genericArgs.Add(returnType);

            // TODO try making it readonly
            // private delegate* unmanaged[Cdecl]<int, uint*, void> _genBuffers;
            // _genBuffers = (delegate* unmanaged[Cdecl]<int, uint*, void>)Load("glGenBuffers");

            string delegateType = $"delegate* unmanaged[Cdecl]<{String.Join(", ", genericArgs)}>";
            string delegateDefinition = $"private {delegateType} {delegateName};";
            string delegateLoad = $"{delegateName} = ({delegateType})Load(\"{commandSpec.Name}\");";

            delegates.Add(delegateDefinition);
            loads.Add(delegateLoad);

            /// <summary>
            /// generate buffer object names
            /// </summary>
            /// <param name="n"></param>
            /// <param name="buffers"></param>
            //[MethodImpl(MethodImplOptions.AggressiveInlining)]
            //public void GenBuffers(int n, uint* buffers)
            //{
            //    _genBuffers(n, buffers);
            //    CheckError();
            //}
            var wrapper = new StringBuilder();

            wrapper.Append(_indent);
            wrapper.AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");

            wrapper.Append(_indent);
            wrapper.Append($"public {returnType} {GetWrapperName(commandSpec.Name)}(");
            wrapper.Append(String.Join(", ", paramTypesAndNames.Select(x => $"{x.Item1} {x.Item2}")));
            wrapper.AppendLine(")");

            wrapper.Append(_indent);
            wrapper.AppendLine("{");

            wrapper.Append(_indent);
            wrapper.Append(_indent);
            wrapper.AppendLine("//...");
            wrapper.AppendLine("throw new System.NotImplementedException();");

            wrapper.Append(_indent);
            wrapper.AppendLine("}");

            wrappers.Add(wrapper.ToString());
        }



        // -------------------------------------------

        var sb = new StringBuilder();
        WriteFileStart(sb);

        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine();

        WriteNamespace(sb);
        WriteClassStart(sb);

        // delegates
        foreach (var @delegate in delegates)
        {
            sb.Append(_indent);
            sb.AppendLine(@delegate);
        }
        sb.AppendLine();

        // loads
        sb.Append(_indent);
        sb.AppendLine("private void LoadFunctions()");
        sb.AppendLine("{");
        foreach (var load in loads)
        {
            sb.Append(_indent);
            sb.Append(_indent);
            sb.AppendLine(load);
        }
        sb.Append(_indent);
        sb.AppendLine("}");
        sb.AppendLine();

        // wrappers
        foreach (var wrapper in wrappers)
        {
            sb.Append(wrapper);
            sb.AppendLine();
        }

        WriteClassEnd(sb);

        return sb.ToString();
    }

    private static string GetDelegateName(string commandName)
    {
        return $"_{FirstToLower(TrimGl(commandName))}";
    }

    private static string GetWrapperName(string commandName)
    {
        return $"{FirstToUpper(TrimGl(commandName))}";
    }

    private static string SanitizeParameterName(string parameterName)
    {
        return IsReservedKeyword(parameterName)
            ? $"@{parameterName}"
            : parameterName;
    }

    private static string TrimGl(string s)
    {
        if (s.StartsWith("gl", StringComparison.OrdinalIgnoreCase))
            return s[2..];

        return s;
    }

    private static bool IsReservedKeyword(string name)
    {
        return name switch
        {
            "params" or "ref" or "string" => true, // TODO extend
            _ => false,
        };
    }

    private string ResolveType(string? type, string? group, string noneType)
    {
        // Enum?
        if (!string.IsNullOrWhiteSpace(group))
        {
            return group!;
        }

        // None?
        if (string.IsNullOrWhiteSpace(type))
            return noneType;

        switch (type)
        {
            case "GLenum": return _fallbackEnumGroupName;

            case "GLboolean": return "byte";

            case "GLbitfield": return "uint"; // not sure

            case "GLchar": return "char";
            case "GLbyte": return "char";
            case "GLubyte": return "byte";

            case "GLshort": return "short";
            case "GLushort": return "ushort";

            case "GLint": return "int";
            case "GLuint": return "uint";

            case "GLint64": return "long";
            case "GLuint64": return "ulong";

            case "GLsizei": return "int";

            case "GLfloat": return "float";
            case "GLdouble": return "double";

            case "GLintptr": return "nint";
            case "GLsizeiptr": return "nint"; // not sure
            case "GLsync": return "nint"; // not sure

            case "GLDEBUGPROC": return "DebugProc"; // special case
        }

        Console.WriteLine($"Unknown: {type}");

        return "unknown";
    }

    private static string FirstToLower(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            throw new ArgumentException("String is empty or whitespace");

        return $"{char.ToLower(s[0])}{s[1..]}";
    }

    private static string FirstToUpper(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            throw new ArgumentException("String is empty or whitespace");

        return $"{char.ToUpper(s[0])}{s[1..]}";
    }

    private static void WriteFileStart(StringBuilder sb)
    {
        sb.AppendLine("// Generated by Paw.BindingGen");
        sb.AppendLine();
    }

    private static void WriteNamespace(StringBuilder sb)
    {
        sb.AppendLine($"namespace {_namespace};");
        sb.AppendLine();
    }

    private static void WriteClassStart(StringBuilder sb)
    {
        sb.AppendLine($"public unsafe partial class {_className}");
        sb.AppendLine("{");
    }

    private static void WriteClassEnd(StringBuilder sb)
    {
        sb.AppendLine("}");
    }

    private (IReadOnlyList<string>, IReadOnlyList<string>) GetEnumsAndCommandsToGenerate()
    {
        var featuresInOrder = _spec.Features.Values
            .OrderBy(x => x.Major)
            .ThenBy(x => x.Minor)
            .ToArray();

        var enums = new HashSet<string>();
        var commands = new HashSet<string>();

        foreach (var feature in featuresInOrder)
        {
            foreach (var @enum in feature.RequiredEnums)
                enums.Add(@enum);

            foreach (var command in feature.RequiredCommands)
                commands.Add(command);

            foreach (var @enum in feature.RemovedEnums)
                enums.Remove(@enum);

            foreach (var command in feature.RemovedCommands)
                commands.Remove(command);
        }

        return (enums.ToArray(), commands.ToArray());
    }
}



internal record GlSpec(IReadOnlyDictionary<string, string> Types,
                       IReadOnlyDictionary<string, string> Kinds,
                       IReadOnlyDictionary<string, GlSpec.EnumSpec> Enums,
                       IReadOnlyDictionary<string, GlSpec.CommandSpec> Commands,
                       IReadOnlyDictionary<string, GlSpec.FeatureSpec> Features)
{
    internal record EnumSpec(string Name,
                             string Value,
                             string? Group,
                             string? Type,
                             string? Alias);
    internal record CommandSpec(string Name,
                                string? Group,
                                string? ReturnType,
                                string? Alias,
                                string? VecEquiv,
                                IReadOnlyList<ParamSpec> Params);
    internal record ParamSpec(string Name,
                              string? Type,
                              string? Group,
                              string? Len,
                              string? Class,
                              bool IsPointer);

    internal record FeatureSpec(string Name,
                                int Major,
                                int Minor,
                                IReadOnlyList<string> RequiredEnums,
                                IReadOnlyList<string> RequiredCommands,
                                IReadOnlyList<string> RemovedEnums,
                                IReadOnlyList<string> RemovedCommands);
}



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

                var paramSpecs = new List<GlSpec.ParamSpec>();

                foreach (var param in command.Elements("param"))
                {
                    string? paramGroup = param.OptionalAttributeValue("group");
                    string? paramLen = param.OptionalAttributeValue("len");
                    string? paramClass = param.OptionalAttributeValue("class"); // shader/buffer/etc
                    string paramName = param.MandatoryElementValue("name");
                    string? paramType = param.OptionalElementValue("ptype"); // ref

                    bool isPointer = param.Value.Contains('*') || !string.IsNullOrWhiteSpace(paramLen);

                    //Console.WriteLine($"  param '{paramName}'");
                    paramSpecs.Add(new GlSpec.ParamSpec(paramName, paramType, paramGroup, paramLen, paramClass, isPointer));
                }

                //Console.WriteLine($"Command '{name}'");
                _commands.Add(name, new GlSpec.CommandSpec(name, group, ptype, alias, vecEquiv, paramSpecs));
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