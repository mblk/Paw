using Paw.BindingGen.Extensions;
using System.Globalization;
using System.Text;

namespace Paw.BindingGen;

internal class GlBindingGenerator
{
    private const string _fallbackEnumGroupName = "Enum";
    private const string _indent = "    ";
    private const string _className = "GL";
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

        var enumGroupMappings = ResolveNamingCollisions(enumNames, commandNames);

        string enumsCode = GenerateEnums(enumNames, enumGroupMappings);
        string commandsCode = GenerateCommands(commandNames, enumGroupMappings);

        var enumsCodeFile = new FileInfo(Path.Combine(_outputDir.FullName, $"{_className}.Enums.cs"));
        var commandsCodeFile = new FileInfo(Path.Combine(_outputDir.FullName, $"{_className}.Commands.cs"));

        File.WriteAllText(enumsCodeFile.FullName, enumsCode);
        File.WriteAllText(commandsCodeFile.FullName, commandsCode);

        //
        Console.WriteLine($"...");
    }

    private IReadOnlyDictionary<string, string> ResolveNamingCollisions(IReadOnlyList<string> enumNames, IReadOnlyList<string> commandNames)
    {
        var trimmedCommandNames = commandNames
            .Select(TrimCommandName)
            .ToHashSet();

        var allEnumGroups = enumNames
            .Select(n => _spec.Enums[n])
            .SelectMany(spec => SplitEnumGroups(spec.Group))
            .ToHashSet();

        var enumGroupMappings = new Dictionary<string, string>();

        foreach (var enumGroup in allEnumGroups)
        {
            string mappedEnumGroup = TrimEnumName(enumGroup);

            if (trimmedCommandNames.Contains(enumGroup))
            {
                Console.WriteLine($"Found name collision: {enumGroup}");
                mappedEnumGroup += "Enum";
            }

            enumGroupMappings.Add(enumGroup, mappedEnumGroup);
        }

        return enumGroupMappings;
    }
    

    private string GenerateEnums(IReadOnlyList<string> enumNames, IReadOnlyDictionary<string, string> enumGroupMappings)
    {
        // Find all enums with keys and values
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
                    groups.Add(groupName, values = []);

                values.Add((enumSpec.Name, enumSpec.Value));
            }
        }
        
        // Write code
        var sb = new StringBuilder();
        WriteFileStart(sb);
        WriteNamespace(sb);
        WriteClassStart(sb);

        foreach (var group in groups)
        {
            string mappedGroupName = enumGroupMappings[group.Key];

            sb.AppendLine($"{_indent}public enum {mappedGroupName} : uint");
            sb.AppendLine($"{_indent}{{");

            foreach (var value in group.Value)
            {
                if (!CheckEnumValue(value.Item2))
                {
                    Console.WriteLine($"Skipping enum value '{value.Item1}' = '{value.Item2}'");
                    continue;
                }

                sb.AppendLine($"{_indent}{_indent}{TrimEnumKey(value.Item1)} = {value.Item2},");
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

    private string GenerateCommands(IReadOnlyList<string> commandNames, IReadOnlyDictionary<string, string> enumGroupMappings)
    {
        var delegates = new List<string>();
        var loads = new List<string>();
        var wrappers = new List<string>();

        foreach (var commandName in commandNames)
        {
            var commandSpec = _spec.Commands[commandName];

            string delegateName = GetDelegateName(commandSpec.Name);
            (string returnType, TypeMapping returnTypeMapping) = ResolveType(commandSpec.ReturnType, commandSpec.Group, "void");

            for (int i = 0; i < commandSpec.PointerCount; i++)
            {
                returnType += "*";
            }

            var paramTypes = new List<string>();
            var paramNames = new List<string>();
            var paramTypesAndNames = new List<(string,string)>();
            foreach (var paramSpec in commandSpec.Params)
            {
                string? mappedGroupName = !string.IsNullOrWhiteSpace(paramSpec.Group)
                    ? enumGroupMappings[paramSpec.Group]
                    : null;

                (string paramType, TypeMapping paramTypeMapping) = ResolveType(paramSpec.Type, mappedGroupName, "void");

                for(int i=0; i<paramSpec.PointerCount; i++)
                {
                    paramType += "*";
                }

                string paramName = SanitizeParameterName(paramSpec.Name);

                paramTypes.Add(paramType);
                paramNames.Add(paramName);
                paramTypesAndNames.Add((paramType, paramName));
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

            if (returnType != "void")
            {
                wrapper.Append("return ");
            }
            wrapper.Append($"{delegateName}(");
            wrapper.Append(String.Join(", ", paramNames));
            wrapper.AppendLine(");");

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
        return $"_{TrimCommandName(commandName).FirstToLower()}";
    }

    private static string GetWrapperName(string commandName)
    {
        return $"{TrimCommandName(commandName).FirstToUpper()}";
    }

    private static string SanitizeParameterName(string parameterName)
    {
        return IsReservedKeyword(parameterName)
            ? $"@{parameterName}"
            : parameterName;
    }

    private static string TrimCommandName(string s)
    {
        if (s.StartsWith("gl", StringComparison.OrdinalIgnoreCase))
            return s[2..];

        return s;
    }

    private static string TrimEnumName(string s)
    {
        if (s.EndsWith("arb", StringComparison.OrdinalIgnoreCase))
            return s[..^3];

        return s;
    }

    private static string TrimEnumKey(string s)
    {
        if (s.StartsWith("gl_", StringComparison.OrdinalIgnoreCase))
            return s[3..];

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

    private enum TypeMapping
    {
        None,
        ByteToBool,
    }

    private (string, TypeMapping) ResolveType(string? glType, string? group, string noneType)
    {
        // Enum?
        if (!string.IsNullOrWhiteSpace(group))
        {
            return (group, TypeMapping.None);
        }

        // None?
        if (string.IsNullOrWhiteSpace(glType))
            return (noneType, TypeMapping.None);

        if (glType == "GLboolean")
        {
            return ("byte", TypeMapping.ByteToBool);
        }

        string mappedType = glType switch
        {
            "GLenum" => _fallbackEnumGroupName,

            "GLchar" => "char",
            "GLbyte" => "char",
            "GLubyte" => "byte",

            "GLshort" => "short",
            "GLushort" => "ushort",
            "GLint" => "int",
            "GLuint" => "uint",
            "GLint64" => "long",
            "GLuint64" => "ulong",
            "GLfloat" => "float",
            "GLdouble" => "double",

            "GLbitfield" => "uint", // not sure

            "GLsizei" => "int",

            "GLintptr" => "nint",
            "GLsizeiptr" => "nint", // not sure
            "GLsync" => "nint", // not sure

            "GLDEBUGPROC" => "DebugProc", // special case

            _ => "unknown",
        };

        return (mappedType, TypeMapping.None);
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
