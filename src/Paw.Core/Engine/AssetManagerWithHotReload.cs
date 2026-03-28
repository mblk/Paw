using Paw.Core.Graphics;
using System.Collections.Concurrent;

namespace Paw.Core.Engine;

public class AssetManagerWithHotReload : AssetManager
{
    //
    // config
    //

    private readonly static string[] _patterns = [
        "*.vert",
        "*.frag",
    ];

    private const int _maxRetryCount = 3;

    private readonly static TimeSpan[] _debounces = [
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
    ];

    //
    // state
    //

    private readonly FileSystemWatcher _watcher;

    // Filled by FSW, read by main thread
    private readonly ConcurrentQueue<string> _changedFiles = new();

    // Main thread only
    private readonly Dictionary<string, HashSet<Asset>> _dependencies = [];
    private readonly Dictionary<Asset, (DateTime, int)> _pendingReloads = [];

    public AssetManagerWithHotReload(DirectoryInfo baseDir, GL gl)
        : base(baseDir, gl)
    {
        _watcher = CreateWatcher(baseDir);
    }

    private FileSystemWatcher CreateWatcher(DirectoryInfo baseDir)
    {
        var watcher = new FileSystemWatcher(baseDir.FullName, "*")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            IncludeSubdirectories = true,
        };

        watcher.Changed += OnChanged;
        watcher.Created += OnCreated;
        watcher.Renamed += OnRenamed;
        watcher.Deleted += OnDeleted;
        watcher.Error += OnError;

        watcher.EnableRaisingEvents = true;

        return watcher;
    }

    public override void Dispose()
    {
        _watcher.Dispose();

        base.Dispose();
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        Console.WriteLine($"FileSystemWatcher error: {e.GetException()}");
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        //Console.WriteLine($"File deleted: {e.FullPath}");

        EnqueueIfMatch(e.FullPath);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        //Console.WriteLine($"File renamed from {e.OldFullPath} to {e.FullPath}");

        EnqueueIfMatch(e.OldFullPath);
        EnqueueIfMatch(e.FullPath);
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        //Console.WriteLine($"File created: {e.FullPath}");

        EnqueueIfMatch(e.FullPath);
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        //Console.WriteLine($"File changed: {e.FullPath}");

        EnqueueIfMatch(e.FullPath);
    }

    private void EnqueueIfMatch(string path)
    {
        if (Matches(path))
        {
            _changedFiles.Enqueue(path);
        }
    }

    private static bool Matches(string path)
    {
        var name = Path.GetFileName(path);
        foreach (var pat in _patterns)
            if (System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(pat, name))
                return true;
        return false;
    }

    protected override void RegisterAssetDependencies(Asset asset, IReadOnlySet<string> files)
    {
        base.RegisterAssetDependencies(asset, files);

        // remove old dependencies
        foreach (var kv in _dependencies)
        {
            kv.Value.Remove(asset);
        }

        // clean up empty entries
        foreach (var emptyKey in _dependencies
            .Where(kv => kv.Value.Count == 0)
            .Select(kv => kv.Key)
            .ToArray())
        {
            _dependencies.Remove(emptyKey);
        }

        // add new dependencies
        foreach (var file in files)
        {
            if (!_dependencies.TryGetValue(file, out var assets))
                _dependencies.Add(file, assets = []);

            assets.Add(asset);
        }
    }

    /// <summary>
    /// Must be called from the main thread.
    /// </summary>
    public void ProcessChanges()
    {
        var now = DateTime.Now;

        while (_changedFiles.TryDequeue(out var path))
        {
            Console.WriteLine($"ProcessChange: '{path}'");

            if (_dependencies.TryGetValue(path, out var assets))
            {
                foreach (var asset in assets)
                {
                    _pendingReloads[asset] = (now.Add(GetDebounce(0)), 0);
                }
            }
        }

        var toReload = _pendingReloads
            .Where(kv => kv.Value.Item1 <= now)
            .ToArray();

        foreach (var (asset, (targetTime, retryCount)) in toReload)
        {
            _pendingReloads.Remove(asset);

            Console.WriteLine($"Reloading asset {asset} try {retryCount} ...");

            if (!base.ReloadAsset(asset))
            {
                if (retryCount < _maxRetryCount)
                {
                    var nextRetry = retryCount + 1;
                    var retryDebounce = GetDebounce(retryCount);

                    _pendingReloads[asset] = (now.Add(retryDebounce), nextRetry);

                    Console.WriteLine($"Scheduling retry {retryCount + 1} for asset {asset} ...");
                }
                else
                {
                    Console.WriteLine($"Giving up reloading asset {asset} after {retryCount} retries.");
                }
            }
        }
    }

    private static TimeSpan GetDebounce(int retryCount)
    {
        if (retryCount < 0)
            return _debounces[0];

        if (retryCount >= _debounces.Length)
            return _debounces[^1];

        return _debounces[retryCount];
    }
}
