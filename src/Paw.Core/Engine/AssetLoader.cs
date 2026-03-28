using Paw.Core.Graphics;

namespace Paw.Core.Engine;

public abstract class AssetLoader<T>
    where T : Asset
{
    protected IAssetManager AssetManager { get; }
    protected IAssetReader Reader { get; }
    protected GL GL { get; }

    public AssetLoader(IAssetManager assetManager, IAssetReader assetReader, GL gl)
    {
        AssetManager = assetManager;
        Reader = assetReader;
        GL = gl;
    }

    public abstract AssetLoadResult<T> Load(string name);

    public abstract AssetLoadResult<T> Reload(T asset);
}

public class AssetLoadResult<T>
    where T : Asset
{
    public T Asset { get; }
    public IReadOnlySet<string> SourceFiles { get; }

    public AssetLoadResult(T asset, IReadOnlySet<string> sourceFiles)
    {
        Asset = asset;
        SourceFiles = sourceFiles;
    }
}