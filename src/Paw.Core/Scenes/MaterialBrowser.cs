using Paw.Core.Assets;
using Paw.Core.Graphics;
using Paw.Core.Resources;

namespace Paw.Core.Scenes;

public class MaterialBrowser : Scene
{
    private enum RenderMode
    {
        Quad1,
        Quad2,
        Mesh1,
        Mesh2,
    }

    private readonly RenderMode[] _allRenderModes = Enum.GetValues<RenderMode>();
    private RenderMode _renderMode;

    private (string, Material)[] _materials = null!;

    private string? _selectedMaterialId;

    private DynamicGeometryRenderer2D _renderer = null!;

    public MaterialBrowser(SceneContext context)
        : base(context)
    {
    }

    public override void Load()
    {
        var allMaterials = AssetManager.GetAllLoadableAssetsOfType(AssetType.Material).ToArray();

        _renderer = new DynamicGeometryRenderer2D(AssetManager, allMaterials, []);

        _materials = AssetManager.Materials.Select(x => (x.Key, x.Value)).OrderBy(x => x.Key).ToArray();
    }

    public override void Unload()
    {
        _renderer.Dispose();
    }

    public override void Update(UpdateContext context)
    {
        if (context.Input.Keyboard.WasPressed(Platforms.Key.Escape))
        {
            context.SceneController.RequestSceneChange("menu");
        }
    }

    public override void Render(RenderContext context)
    {
        UI.SetNextWindowPosition(new Vector2(50, 200));
        using (UI.BeginWindow(new Vector2(300, 600), "Materials"))
        {
            UI.Radiobuttons("RenderMode", ref _renderMode, _allRenderModes);

            UI.Label(Format($"Materials:"));

            using (UI.BeginList(new Vector2(100, 200), "List"))
            {
                foreach (var (id, material) in _materials)
                {
                    if (UI.ListItem(id, _selectedMaterialId == id))
                    {
                        _selectedMaterialId = id;
                    }
                }
            }

            if (UI.Button("Reload all"))
            {
                foreach (var (_, material) in _materials)
                {
                    AssetManager.ReloadAsset(material);
                }
            }
        }

        var dt = context.DeltaTime;
        var (width, height) = context.WindowSize;

        var mOrthoProj = Matrix4x4.CreateOrthographicOffCenter(0, width, height, 0, -1, 1);
        var mModel = Matrix4x4.Identity;
        var mView = Matrix4x4.Identity;
        var mvp = mModel * mView * mOrthoProj;

        if (_selectedMaterialId is not null)
        {
            var writer = _renderer.GetWriter(_selectedMaterialId);

            Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
            Vector2 size = new Vector2(MathF.Min(width, height) - 100);
            Vector4 color = new Vector4(1, 1, 1, 1);

            Vector2 halfSize = size * 0.5f;

            Vector2 tl = new(center.X - halfSize.X, center.Y - halfSize.Y);
            Vector2 tr = new(center.X + halfSize.X, center.Y - halfSize.Y);
            Vector2 br = new(center.X + halfSize.X, center.Y + halfSize.Y);
            Vector2 bl = new(center.X - halfSize.X, center.Y + halfSize.Y);

            switch (_renderMode)
            {
                case RenderMode.Quad1:
                {
                    writer.AddVertex(new DynamicGeometryRenderer2D.Vertex { Position = bl, Color = color, UV = new(0.0f, 1.0f) });
                    writer.AddVertex(new DynamicGeometryRenderer2D.Vertex { Position = br, Color = color, UV = new(1.0f, 1.0f) });
                    writer.AddVertex(new DynamicGeometryRenderer2D.Vertex { Position = tr, Color = color, UV = new(1.0f, 0.0f) });

                    writer.AddVertex(new DynamicGeometryRenderer2D.Vertex { Position = bl, Color = color, UV = new(0.0f, 1.0f) });
                    writer.AddVertex(new DynamicGeometryRenderer2D.Vertex { Position = tr, Color = color, UV = new(1.0f, 0.0f) });
                    writer.AddVertex(new DynamicGeometryRenderer2D.Vertex { Position = tl, Color = color, UV = new(0.0f, 0.0f) });

                    break;
                }

                case RenderMode.Quad2:
                {
                    writer.AddVertex(new DynamicGeometryRenderer2D.Vertex { Position = bl, Color = color, UV = new(0.0f, 2.0f) });
                    writer.AddVertex(new DynamicGeometryRenderer2D.Vertex { Position = br, Color = color, UV = new(2.0f, 2.0f) });
                    writer.AddVertex(new DynamicGeometryRenderer2D.Vertex { Position = tr, Color = color, UV = new(2.0f, 0.0f) });

                    writer.AddVertex(new DynamicGeometryRenderer2D.Vertex { Position = bl, Color = color, UV = new(0.0f, 2.0f) });
                    writer.AddVertex(new DynamicGeometryRenderer2D.Vertex { Position = tr, Color = color, UV = new(2.0f, 0.0f) });
                    writer.AddVertex(new DynamicGeometryRenderer2D.Vertex { Position = tl, Color = color, UV = new(0.0f, 0.0f) });

                    break;
                }

                default:
                    break;
            }
        }

        _renderer.Render(mvp);
    }
}
