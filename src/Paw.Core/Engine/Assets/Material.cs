using System.Numerics;
using System.Text.Json.Serialization;

namespace Paw.Core.Engine.Assets;

public class MaterialDef
{
    [JsonIgnore]
    public string Id { get; set; } = null!;
    public required string Name { get; init; }
    public required string Shader { get; init; }
    public required IReadOnlyList<string> Textures { get; init; }
}

public class MaterialLoader : AssetLoader<Material>
{
    public MaterialLoader(IAssetManager assetManager, IAssetReader assetReader, GL gl)
        : base(assetManager, assetReader, gl)
    {
    }

    public override AssetLoadResult<Material> Load(string name)
    {
        string path = Reader.GetAssetPath(AssetType.Material, $"{name}.json");

        MaterialDef def = Reader.ReadFileAsJson<MaterialDef>(path, AssetDefJsonContext.Default.MaterialDef);
        def.Id = name;

        Shader shader = AssetManager.LoadShader(def.Shader);
        IReadOnlyList<Texture> textures = def.Textures.Select(AssetManager.LoadTexture).ToArray();

        var material = new Material(shader, textures);
        var sourceFiles = new HashSet<string> { path };

        return new AssetLoadResult<Material>(material, sourceFiles);
    }

    public override AssetLoadResult<Material> Reload(Material asset)
    {
        throw new NotSupportedException();
    }
}

public class Material : Asset
{
    public readonly Shader _shader;
    public readonly IReadOnlyList<Texture> _textures;

    public Material(Shader shader, IReadOnlyList<Texture> textures)
    {
        _shader = shader;
        _textures = textures.ToArray();
    }

    public void Bind()
    {
        for (int i = 0; i < _textures.Count; i++)
        {
            _textures[i].Bind(i);
        }
        
        _shader.Use();
    }

    public void Unbind()
    {
        _shader.Unuse();
        
        for (int i = 0; i < _textures.Count; i++)
        {
            _textures[i].Unbind(i);
        }
    }

    public void SetUniform(string name, Matrix4x4 value, bool transpose = false)
    {
        _shader.SetUniform(name, value, transpose);
    }
}