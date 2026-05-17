using Paw.Core.Assets;
using Paw.Core.Graphics;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Paw.Core.Resources;

[JsonSerializable(typeof(MaterialDef))]
public partial class AssetDefJsonContext : JsonSerializerContext
{
}

public interface IAssetReader
{
    IEnumerable<string> GetAllLoadableAssetsOfType(AssetType assetType);
    string GetAssetPath(AssetType assetType, string assetName);
    string ReadFileAsString(string assetPath);
    string[] ReadFileAsLines(string assetPath);
    byte[] ReadFileAsBytes(string assetPath);
    T ReadFileAsJson<T>(string assetPath, JsonTypeInfo<T> typeInfo) where T : class;
}

public interface IAssetManager
{
    Shader LoadShader(string name);
    Texture LoadTexture(string name);
    Font LoadFont(string name);
    Material LoadMaterial(string name);

    string[] LoadDataFile(string name);
}


public abstract class Asset
{
    // hmm
}

public class AssetManager : IDisposable, IAssetManager
{
    private readonly IAssetReader _reader;
    private readonly ShaderLoader _shaderLoader;
    private readonly TextureLoader _textureLoader;
    private readonly FontLoader _fontLoader;
    private readonly MaterialLoader _materialLoader;

    private readonly Dictionary<string, Shader> _shaders = [];
    private readonly Dictionary<string, Texture> _textures = [];
    private readonly Dictionary<string, Font> _fonts = [];
    private readonly Dictionary<string, Material> _materials = [];

    public IReadOnlyDictionary<string, Material> Materials => _materials;

    public static DirectoryInfo FindBaseDirectory()
    {
        var startDir = new DirectoryInfo(Directory.GetCurrentDirectory());
        var currentDir = startDir;
        while (currentDir != null)
        {
            var assetsDir = currentDir.GetDirectories("Assets").SingleOrDefault();
            if (assetsDir is not null)
            {
                Console.WriteLine($"Found Assets directory: {assetsDir.FullName}");
                return assetsDir;
            }
            currentDir = currentDir.Parent;
        }

        throw new Exception($"Could not find asset base directory. Started search at: {startDir.FullName}");
    }

    public GL GL { get; }

    public AssetManager(DirectoryInfo baseDir, GL gl)
    {
        GL = gl;

        _reader = new FileSystemAssetReader(baseDir);
        _shaderLoader = new ShaderLoader(this, _reader, gl);
        _textureLoader = new TextureLoader(this, _reader, gl);
        _fontLoader = new FontLoader(this, _reader, gl);
        _materialLoader = new MaterialLoader(this, _reader, gl);
    }

    public IEnumerable<string> GetAllLoadableAssetsOfType(AssetType type)
    {
        return _reader.GetAllLoadableAssetsOfType(type);
    }

    public Shader LoadShader(string name)
    {
        if (_shaders.TryGetValue(name, out var cachedShader))
            return cachedShader;

        var loadedShader = _shaderLoader.Load(name);

        var shader = loadedShader.Asset;
        var sourceFiles = loadedShader.SourceFiles;

        RegisterAssetDependencies(shader, sourceFiles);

        _shaders.Add(name, shader);

        return shader;
    }

    public Texture LoadTexture(string name)
    {
        if (_textures.TryGetValue(name, out var cachedTexture))
            return cachedTexture;

        var loadedTexture = _textureLoader.Load(name);

        var texture = loadedTexture.Asset;
        var sourceFiles = loadedTexture.SourceFiles;

        RegisterAssetDependencies(texture, sourceFiles);

        _textures.Add(name, texture);

        return texture;
    }

    public Font LoadFont(string name)
    {
        if (_fonts.TryGetValue(name, out var cachedFont))
            return cachedFont;

        var loadedFont = _fontLoader.Load(name);

        var font = loadedFont.Asset;
        var sourceFiles = loadedFont.SourceFiles;

        RegisterAssetDependencies(font, sourceFiles);

        _fonts.Add(name, font);

        return font;
    }

    public Material LoadMaterial(string name)
    {
        if (_materials.TryGetValue(name, out var cachedMaterial))
            return cachedMaterial;

        var loadedMaterial = _materialLoader.Load(name);

        var material = loadedMaterial.Asset;
        var sourceFiles = loadedMaterial.SourceFiles;

        RegisterAssetDependencies(material, sourceFiles);

        _materials.Add(name, material);

        return material;
    }

    public string[] LoadDataFile(string name)
    {
        string path = _reader.GetAssetPath(AssetType.Data, name);

        string[] lines = _reader.ReadFileAsLines(path);

        return lines;
    }

    public bool ReloadAsset(Asset asset)
    {
        try
        {
            switch (asset)
            {
                case Shader shader:
                {
                    Debug.Assert(_shaders.ContainsKey(shader.Name));
                    var reloadedShader = _shaderLoader.Reload(shader);
                    RegisterAssetDependencies(shader, reloadedShader.SourceFiles);
                    break;
                }

                case Texture texture:
                {
                    // ...
                    break;
                }

                case Font font:
                {
                    // ...
                    break;
                }

                default:
                {
                    Console.WriteLine($"Error: Unknown asset type to reload: {asset.GetType().Name}");
                    break;
                }
            }

            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error reloading asset: {e.Message}");
            return false;
        }
    }

    public virtual void Dispose()
    {
        foreach (var material in _materials.Values)
        {
            //material.Dispose();
        }
        _materials.Clear();

        foreach (var shader in _shaders.Values)
        {
            shader.Dispose();
        }
        _shaders.Clear();

        foreach (var texture in _textures.Values)
        {
            texture.Dispose();
        }
        _textures.Clear();

        foreach (var font in _fonts.Values)
        {
            font.Dispose();
        }
        _fonts.Clear();
    }

    protected virtual void RegisterAssetDependencies(Asset asset, IReadOnlySet<string> files)
    {
        // Override in derived classes to track file dependencies for reloading.
    }

    private static string GetAssetSubDirectoryName(AssetType type) => type switch
    {
        AssetType.Shader => "Shaders",
        AssetType.Texture => "Textures",
        AssetType.Font => "Fonts",
        AssetType.Material => "Materials",
        AssetType.Model => "Models",
        AssetType.Data => "Data",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    private class FileSystemAssetReader : IAssetReader
    {
        private readonly DirectoryInfo _baseDir;

        public FileSystemAssetReader(DirectoryInfo baseDir)
        {
            if (!baseDir.Exists)
                throw new DirectoryNotFoundException($"Asset base directory does not exist: {baseDir.FullName}");

            _baseDir = baseDir;
        }

        public IEnumerable<string> GetAllLoadableAssetsOfType(AssetType assetType)
        {
            var assetDir = new DirectoryInfo(Path.Combine(_baseDir.FullName, GetAssetSubDirectoryName(assetType)));

            var assetFiles = assetDir.GetFiles("*.*", SearchOption.TopDirectoryOnly);

            return assetFiles.Select(fi => Path.GetFileNameWithoutExtension(fi.Name));
        }

        public string GetAssetPath(AssetType assetType, string assetName)
        {
            return Path.Combine(_baseDir.FullName, GetAssetSubDirectoryName(assetType), assetName);
        }

        public string ReadFileAsString(string assetPath)
        {
            var fileInfo = new FileInfo(assetPath);
            if (!fileInfo.Exists)
                throw new FileNotFoundException($"Asset file does not exist: {fileInfo.FullName}");

            return File.ReadAllText(fileInfo.FullName);
        }

        public string[] ReadFileAsLines(string assetPath)
        {
            var fileInfo = new FileInfo(assetPath);
            if (!fileInfo.Exists)
                throw new FileNotFoundException($"Asset file does not exist: {fileInfo.FullName}");

            return File.ReadAllLines(fileInfo.FullName);
        }

        public byte[] ReadFileAsBytes(string assetPath)
        {
            var fileInfo = new FileInfo(assetPath);
            if (!fileInfo.Exists)
                throw new FileNotFoundException($"Asset file does not exist: {fileInfo.FullName}");

            return File.ReadAllBytes(fileInfo.FullName);
        }

        public T ReadFileAsJson<T>(string assetPath, JsonTypeInfo<T> typeInfo) where T : class
        {
            string json = ReadFileAsString(assetPath);

            T? obj = JsonSerializer.Deserialize<T>(json, typeInfo);
            if (obj is null)
                throw new Exception($"Failed to deserialize asset: {assetPath} as {typeof(T).FullName}");

            return obj;
        }
    }

    private class ArchiveAssetReader : IAssetReader
    {
        public ArchiveAssetReader(FileInfo archiveFile)
        {
            //
        }

        public IEnumerable<string> GetAllLoadableAssetsOfType(AssetType assetType)
        {
            throw new NotImplementedException();
        }

        public string GetAssetPath(AssetType assetType, string assetName)
        {
            throw new NotImplementedException();
        }

        public string ReadFileAsString(string assetPath)
        {
            throw new NotImplementedException();
        }

        public string[] ReadFileAsLines(string assetPath)
        {
            throw new NotImplementedException();
        }

        public byte[] ReadFileAsBytes(string assetPath)
        {
            throw new NotImplementedException();
        }

        public T ReadFileAsJson<T>(string assetPath, JsonTypeInfo<T> typeInfo) where T : class
        {
            throw new NotImplementedException();
        }
    }
}