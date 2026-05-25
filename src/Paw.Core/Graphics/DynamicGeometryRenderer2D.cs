using Paw.Core.Assets;
using Paw.Core.Resources;
using Paw.Core.Utils;
using System.Collections.Frozen;
using System.Runtime.InteropServices;

namespace Paw.Core.Graphics;

public unsafe class DynamicGeometryRenderer2D : IDisposable
{
    // TODO:
    // - what about index buffers?

    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex
    {
        public Vector2 Position;
        public Vector4 Color;
        public Vector2 UV;
    }

    private const int _initialVertexBufferSize = 1024;

    private readonly FrozenDictionary<string, PerMaterialData> _perMaterialDatas;
    private readonly FrozenDictionary<string, Font> _fonts;

    private readonly GL _gl;


    private sealed class PrimitiveBatch : IDisposable
    {
        public readonly BufferObject VertexBuffer;
        public readonly VertexArrayObject<Vertex> VertexArray;
        public readonly List<Vertex> Vertices = new(_initialVertexBufferSize);

        public PrimitiveBatch(GL gl)
        {
            VertexBuffer = new BufferObject(gl);
            VertexBuffer.SetSizeAndUsage(sizeof(Vertex) * _initialVertexBufferSize, GL.BufferUsage.STREAM_DRAW);
            VertexArray = new VertexArrayObject<Vertex>(gl, VertexBuffer);
        }

        public void Dispose()
        {
            VertexArray.Dispose();
            VertexBuffer.Dispose();
        }
    }

    private sealed class PerMaterialData : IDisposable
    {
        public readonly Material Material;
        public readonly PrimitiveBatch Triangles;
        public readonly PrimitiveBatch Lines;
        public readonly PrimitiveBatch Points;
        public readonly Writer Writer;

        public PerMaterialData(GL gl, Material material)
        {
            Material = material;
            Triangles = new PrimitiveBatch(gl);
            Lines = new PrimitiveBatch(gl);
            Points = new PrimitiveBatch(gl);
            Writer = new Writer(Triangles.Vertices, Lines.Vertices, Points.Vertices);
        }

        public void Dispose()
        {
            Triangles.Dispose();
            Lines.Dispose();
            Points.Dispose();
        }
    }

    public class Writer
    {
        private readonly List<Vertex> _triangles;
        private readonly List<Vertex> _lines;
        private readonly List<Vertex> _points;

        public Writer(List<Vertex> triangles, List<Vertex> lines, List<Vertex> points)
        {
            _triangles = triangles;
            _lines = lines;
            _points = points;
        }

        public void AddVertex(Vertex vertex)
        {
            _triangles.Add(vertex);
        }

        public void AddTriangle(Vector2 p1, Vector4 c1, Vector2 p2, Vector4 c2, Vector2 p3, Vector4 c3)
        {
            _triangles.Add(new Vertex() { Position = p1, Color = c1, UV = new(0.0f, 0.0f) });
            _triangles.Add(new Vertex() { Position = p2, Color = c2, UV = new(1.0f, 0.0f) });
            _triangles.Add(new Vertex() { Position = p3, Color = c3, UV = new(0.5f, 1.0f) });
        }

        public void AddRectangle(Vector2 center, Vector2 size, Vector4 color)
        {
            Vector2 halfSize = size * 0.5f;

            Vector2 tl = new(center.X - halfSize.X, center.Y - halfSize.Y);
            Vector2 tr = new(center.X + halfSize.X, center.Y - halfSize.Y);
            Vector2 br = new(center.X + halfSize.X, center.Y + halfSize.Y);
            Vector2 bl = new(center.X - halfSize.X, center.Y + halfSize.Y);

            _triangles.Add(new Vertex { Position = bl, Color = color, UV = new(0.0f, 1.0f) });
            _triangles.Add(new Vertex { Position = br, Color = color, UV = new(1.0f, 1.0f) });
            _triangles.Add(new Vertex { Position = tr, Color = color, UV = new(1.0f, 0.0f) });

            _triangles.Add(new Vertex { Position = bl, Color = color, UV = new(0.0f, 1.0f) });
            _triangles.Add(new Vertex { Position = tr, Color = color, UV = new(1.0f, 0.0f) });
            _triangles.Add(new Vertex { Position = tl, Color = color, UV = new(0.0f, 0.0f) });
        }

        public void AddRectangleWithPositionSize(Vector2 topLeft, Vector2 size, Vector4 color) // need to find better naming schema
        {
            Vector2 tl = topLeft;
            Vector2 tr = new(topLeft.X + size.X, topLeft.Y);
            Vector2 br = new(topLeft.X + size.X, topLeft.Y + size.Y);
            Vector2 bl = new(topLeft.X, topLeft.Y + size.Y);

            _triangles.Add(new Vertex { Position = bl, Color = color, UV = new(0.0f, 1.0f) });
            _triangles.Add(new Vertex { Position = br, Color = color, UV = new(1.0f, 1.0f) });
            _triangles.Add(new Vertex { Position = tr, Color = color, UV = new(1.0f, 0.0f) });

            _triangles.Add(new Vertex { Position = bl, Color = color, UV = new(0.0f, 1.0f) });
            _triangles.Add(new Vertex { Position = tr, Color = color, UV = new(1.0f, 0.0f) });
            _triangles.Add(new Vertex { Position = tl, Color = color, UV = new(0.0f, 0.0f) });
        }

        public void AddRotatedRectangle(Vector2 center, Vector2 size, float angle, Vector4 color)
        {
            Vector2 halfSize = size * 0.5f;

            Vector2 tl = center + new Vector2(-halfSize.X, -halfSize.Y).Rotate(angle);
            Vector2 tr = center + new Vector2(halfSize.X, -halfSize.Y).Rotate(angle);
            Vector2 br = center + new Vector2(halfSize.X, halfSize.Y).Rotate(angle);
            Vector2 bl = center + new Vector2(-halfSize.X, halfSize.Y).Rotate(angle);

            _triangles.Add(new Vertex { Position = bl, Color = color, UV = new(0.0f, 1.0f) });
            _triangles.Add(new Vertex { Position = br, Color = color, UV = new(1.0f, 1.0f) });
            _triangles.Add(new Vertex { Position = tr, Color = color, UV = new(1.0f, 0.0f) });

            _triangles.Add(new Vertex { Position = bl, Color = color, UV = new(0.0f, 1.0f) });
            _triangles.Add(new Vertex { Position = tr, Color = color, UV = new(1.0f, 0.0f) });
            _triangles.Add(new Vertex { Position = tl, Color = color, UV = new(0.0f, 0.0f) });
        }

        public void AddRectangleWithUV(Vector2 center, Vector2 size, Vector4 color, Vector2 uvMin, Vector2 uvMax)
        {
            Vector2 halfSize = size * 0.5f;

            Vector2 tl = new(center.X - halfSize.X, center.Y - halfSize.Y);
            Vector2 tr = new(center.X + halfSize.X, center.Y - halfSize.Y);
            Vector2 br = new(center.X + halfSize.X, center.Y + halfSize.Y);
            Vector2 bl = new(center.X - halfSize.X, center.Y + halfSize.Y);

            _triangles.Add(new Vertex { Position = bl, Color = color, UV = new(uvMin.X, uvMax.Y) });
            _triangles.Add(new Vertex { Position = br, Color = color, UV = new(uvMax.X, uvMax.Y) });
            _triangles.Add(new Vertex { Position = tr, Color = color, UV = new(uvMax.X, uvMin.Y) });
            _triangles.Add(new Vertex { Position = bl, Color = color, UV = new(uvMin.X, uvMax.Y) });
            _triangles.Add(new Vertex { Position = tr, Color = color, UV = new(uvMax.X, uvMin.Y) });
            _triangles.Add(new Vertex { Position = tl, Color = color, UV = new(uvMin.X, uvMin.Y) });
        }

        public void AddText(Font font, Vector2 position, Vector4 color, float scale, string text)
        {
            Vector2 currentPosition = position;

            foreach (char c in text)
            {
                if (!font.MetaData.Characters.TryGetValue(c, out var charData))
                {
                    Console.WriteLine($"char data {(uint)c} '{c}' not found");
                    continue;
                }

                if (c == ' ')
                {
                    currentPosition.X += charData.XAdvance * scale;
                    continue;
                }

                Vector2 uvMin = charData.UvMin;
                Vector2 uvMax = charData.UvMax;
                Vector2 size = charData.Size * scale;
                Vector2 offset = charData.Offset * scale;

                float xl = currentPosition.X + offset.X;
                float xr = xl + size.X;

                float yt = currentPosition.Y + offset.Y;
                float yb = yt + size.Y;

                Vector2 tl = new(xl, yt);
                Vector2 tr = new(xr, yt);
                Vector2 br = new(xr, yb);
                Vector2 bl = new(xl, yb);

                _triangles.Add(new Vertex { Position = bl, Color = color, UV = new(uvMin.X, uvMax.Y) });
                _triangles.Add(new Vertex { Position = br, Color = color, UV = new(uvMax.X, uvMax.Y) });
                _triangles.Add(new Vertex { Position = tr, Color = color, UV = new(uvMax.X, uvMin.Y) });
                _triangles.Add(new Vertex { Position = bl, Color = color, UV = new(uvMin.X, uvMax.Y) });
                _triangles.Add(new Vertex { Position = tr, Color = color, UV = new(uvMax.X, uvMin.Y) });
                _triangles.Add(new Vertex { Position = tl, Color = color, UV = new(uvMin.X, uvMin.Y) });

                currentPosition.X += charData.XAdvance * scale;
            }
        }

        public void AddPoint(Vector2 position, Vector4 color)
        {
            _points.Add(new Vertex { Position = position, Color = color, UV = Vector2.Zero });
        }

        public void AddLine(Vector2 start, Vector4 startColor, Vector2 end, Vector4 endColor)
        {
            _lines.Add(new Vertex { Position = start, Color = startColor, UV = Vector2.Zero });
            _lines.Add(new Vertex { Position = end, Color = endColor, UV = Vector2.Zero });
        }

        public void AddLine(Vector2 start, Vector2 end, Vector4 color)
        {
            AddLine(start, color, end, color);
        }
    }

    public DynamicGeometryRenderer2D(AssetManager assetManager, IReadOnlyList<string> materialsToLoad, IReadOnlyList<string> fontsToLoad)
    {
        var perMaterialDatas = new Dictionary<string, PerMaterialData>();
        foreach (var materialId in materialsToLoad)
        {
            var material = assetManager.LoadMaterial(materialId);
            var perMaterialData = new PerMaterialData(assetManager.GL, material);
            perMaterialDatas.Add(materialId, perMaterialData);
        }
        _perMaterialDatas = perMaterialDatas.ToFrozenDictionary();

        var fonts = new Dictionary<string, Font>();
        foreach (var fontId in fontsToLoad)
        {
            var font = assetManager.LoadFont(fontId);
            fonts.Add(fontId, font);
        }
        _fonts = fonts.ToFrozenDictionary();

        _gl = assetManager.GL;
    }

    public Writer GetWriter(string materialId)
    {
        if (!_perMaterialDatas.TryGetValue(materialId, out var perMaterialData))
            throw new ArgumentException($"Material '{materialId}' not loaded");

        return perMaterialData.Writer;
    }

    public Font GetFont(string fontId)
    {
        if (!_fonts.TryGetValue(fontId, out var font))
            throw new ArgumentException($"Font '{fontId}' not loaded");

        return font;
    }

    public void Render(Matrix4x4 mvp, Action<Material>? uniformConfig = null)
    {
        foreach (var (_, perMaterialData) in _perMaterialDatas)
        {
            var material = perMaterialData.Material;

            if (uniformConfig is not null) // TODO need MaterialInstance type/asset - one material can have multiple users (each with different uniforms, etc)
            {
                uniformConfig(material);
            }

            material.SetUniform("uMVP", mvp);
            material.Bind();
            {
                _gl.Enable(GL.EnableCap.BLEND);
                _gl.BlendFunc(GL.BlendingFactor.SRC_ALPHA, GL.BlendingFactor.ONE_MINUS_SRC_ALPHA);
                {
                    RenderBatch(perMaterialData.Triangles, material, GL.PrimitiveType.TRIANGLES);
                    RenderBatch(perMaterialData.Lines, material, GL.PrimitiveType.LINES);
                    RenderBatch(perMaterialData.Points, material, GL.PrimitiveType.POINTS);
                }
                _gl.Disable(GL.EnableCap.BLEND);
            }
            material.Unbind();
        }
    }

    private static void RenderBatch(PrimitiveBatch batch, Material material, GL.PrimitiveType primitiveType)
    {
        var vertexCount = batch.Vertices.Count;
        if (vertexCount == 0)
            return;

        batch.VertexBuffer.SetData(batch.Vertices, GL.BufferUsage.STREAM_DRAW);
        batch.Vertices.Clear();

        batch.VertexArray.Bind();
        {
            for (int pass = 1; pass <= material.Passes; pass++)
            {
                material.SetPass(pass);
                batch.VertexArray.Draw(primitiveType, 0, vertexCount);
            }
        }
        batch.VertexArray.Unbind();
    }

    public void Dispose()
    {
        foreach (var (_, perMaterialData) in _perMaterialDatas)
        {
            perMaterialData.Dispose();
        }
    }
}
