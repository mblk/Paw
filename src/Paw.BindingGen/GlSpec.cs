namespace Paw.BindingGen;

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
                                int PointerCount,
                                IReadOnlyList<ParamSpec> Params);
    internal record ParamSpec(string Name,
                              string? Type,
                              string? Group,
                              string? Len,
                              string? Class,
                              int PointerCount);

    internal record FeatureSpec(string Name,
                                int Major,
                                int Minor,
                                IReadOnlyList<string> RequiredEnums,
                                IReadOnlyList<string> RequiredCommands,
                                IReadOnlyList<string> RemovedEnums,
                                IReadOnlyList<string> RemovedCommands);
}
