using Paw.Core.Graphics;
using Paw.Core.Resources;
using System.Numerics;
using System.Text.Json.Serialization;

namespace Paw.Core.Assets;

public class MaterialDef
{
    [JsonIgnore]
    public string Id { get; set; } = null!;

    public required string Name { get; init; }
    public required string Shader { get; init; }
    public required IReadOnlyList<string> Textures { get; init; }
    public required int Passes { get; init; }
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

        var material = new Material(shader, textures, def.Passes);
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
    private readonly Shader _shader;
    private readonly IReadOnlyList<Texture> _textures;

    public int Passes { get; }

    public Material(Shader shader, IReadOnlyList<Texture> textures, int passes)
    {
        _shader = shader;
        _textures = textures.ToArray();
        Passes = passes;
    }

    public void Bind()
    {
        for (int i = 0; i < _textures.Count; i++)
        {
            _textures[i].Bind(i);

        }

        if (_textures.Count == 1)
        {
            _shader.SetUniform("uTex", 0);
        }
        else if (_textures.Count > 1)
        {
            for (int i = 0; i < _textures.Count; i++)
            {
                _shader.SetUniform($"uTex{i + 1}", i); // TODO maybe use glsl array instead?
            }
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

    public void SetPass(int pass)
    {
        if (Passes > 1)
        {
            _shader.SetUniform("uPass", pass);
        }
    }

    public void SetUniform(string name, Matrix4x4 value, bool transpose = false)
    {
        _shader.SetUniform(name, value, transpose);
    }
}