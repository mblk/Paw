using Paw.Core.Platforms;

namespace Paw.Core.Platforms.LinuxWayland;

public class LinuxWaylandPlatform : IPlatform
{
    public LinuxWaylandPlatform(PlatformOptions options)
    {
    }

    public IWindow CreateWindow(WindowOptions options)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
    }
}
