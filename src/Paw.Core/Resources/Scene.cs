using Paw.Core.Platforms;

namespace Paw.Core.Resources;

public abstract class Scene
{
    protected AssetManager AssetManager { get; }

    public Scene(SceneContext context)
    {
        AssetManager = context.AssetManager;
    }

    public abstract void Load();
    public abstract void Unload();

    public abstract void Update(UpdateContext context);
    public abstract void Render(RenderContext context);
}

public class SceneContext
{
    public required AssetManager AssetManager { get; init; }
}

public class UpdateContext
{
    public required float DeltaTime { get; init; }
    public required (int, int) WindowSize { get; init; }
    public required IInput Input { get; init; }
    public required ISceneController SceneController { get; init; }
}

public class RenderContext
{
    public required float DeltaTime { get; init; }
    public required (int, int) WindowSize { get; init; }
}

public interface ISceneController
{
    void RequestSceneChange(string newSceneId);

    void RequestExit();
}

public class SceneManager : IDisposable, ISceneController
{
    private readonly Dictionary<string, Func<SceneContext, Scene>> _sceneFactories = [];
    private readonly SceneContext _context;

    private bool _sceneChangeRequested;
    private string _requestedSceneId = String.Empty;
    private bool _exitRequested;

    public Scene? CurrentScene { get; private set; }

    public bool ExitRequested => _exitRequested;

    public SceneManager(SceneContext context)
    {
        _context = context;
    }

    public void Dispose()
    {
        CurrentScene?.Unload();
        CurrentScene = null;
    }

    public void Update(UpdateContext context)
    {
        if (_sceneChangeRequested)
        {
            SetCurrentScene(_requestedSceneId);
            _sceneChangeRequested = false;
            _requestedSceneId = String.Empty;
        }

        CurrentScene?.Update(context);
    }

    public void Render(RenderContext context)
    {
        CurrentScene?.Render(context);
    }

    public void RegisterScene(string id, Func<SceneContext, Scene> sceneFactory)
    {
        Console.WriteLine($"RegisterScene: '{id}'");
        _sceneFactories.Add(id, sceneFactory);
    }

    public void RequestSceneChange(string newSceneId)
    {
        Console.WriteLine($"RequestSceneChange: '{newSceneId}'");

        _sceneChangeRequested = true;
        _requestedSceneId = newSceneId;
    }

    public void RequestExit()
    {
        Console.WriteLine($"RequestExit");

        _exitRequested = true;
    }

    public void SetCurrentScene(string newSceneId)
    {
        Console.WriteLine($"SetCurrentScene: '{newSceneId}'");

        if (!_sceneFactories.TryGetValue(newSceneId, out var sceneFactory))
            throw new Exception($"Unknown scene id: '{newSceneId}'");

        CurrentScene?.Unload();
        CurrentScene = sceneFactory(_context);
        CurrentScene.Load();
    }
}