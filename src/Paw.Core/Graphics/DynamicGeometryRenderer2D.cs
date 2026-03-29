using Paw.Core.Assets;
using Paw.Core.Resources;
using Paw.Core.Utils;
using System.Collections.Frozen;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Paw.Core.Graphics;

public unsafe class DynamicGeometryRenderer2D : IDisposable
{
    // TODO:
    // - what about index buffers?

    [StructLayout(LayoutKind.Sequential)]
    public struct VertexPCT
    {
        public Vector2 Position;
        public Vector3 Color;
        public Vector2 UV;
    }

    private const int _initialVertexBufferSize = 1024;

    private readonly FrozenDictionary<string, PerMaterialData> _perMaterialDatas;
    private readonly FrozenDictionary<string, Font> _fonts;




    public class PerMaterialData : IDisposable
    {
        public readonly Material Material;
        public readonly BufferObject VertexBuffer;
        public readonly VertexArrayObject<VertexPCT> VertexArray;
        public readonly Writer Writer;
        public readonly List<VertexPCT> Vertices = new(_initialVertexBufferSize);

        public PerMaterialData(GL gl, Material material)
        {
            Material = material;
            VertexBuffer = new BufferObject(gl);
            VertexBuffer.SetSizeAndUsage(sizeof(VertexPCT) * _initialVertexBufferSize, GL.BufferUsage.STREAM_DRAW);
            VertexArray = new VertexArrayObject<VertexPCT>(gl, VertexBuffer);
            Writer = new Writer(Vertices);
        }

        public void Dispose()
        {
            VertexArray.Dispose();
            VertexBuffer.Dispose();
        }
    }

    public class Writer
    {
        private readonly List<VertexPCT> _vertices;

        public Writer(List<VertexPCT> vertices)
        {
            _vertices = vertices;
        }

        public void AddTriangle(Vector2 p1, Vector3 c1, Vector2 p2, Vector3 c2, Vector2 p3, Vector3 c3)
        {
            _vertices.Add(new VertexPCT() { Position = p1, Color = c1, UV = new(0.0f, 0.0f) });
            _vertices.Add(new VertexPCT() { Position = p2, Color = c2, UV = new(1.0f, 0.0f) });
            _vertices.Add(new VertexPCT() { Position = p3, Color = c3, UV = new(0.5f, 1.0f) });
        }

        public void AddRectangle(Vector2 center, Vector2 size, Vector3 color)
        {
            Vector2 halfSize = size * 0.5f;

            Vector2 tl = new(center.X - halfSize.X, center.Y - halfSize.Y);
            Vector2 tr = new(center.X + halfSize.X, center.Y - halfSize.Y);
            Vector2 br = new(center.X + halfSize.X, center.Y + halfSize.Y);
            Vector2 bl = new(center.X - halfSize.X, center.Y + halfSize.Y);

            _vertices.Add(new VertexPCT { Position = bl, Color = color, UV = new(0.0f, 1.0f) });
            _vertices.Add(new VertexPCT { Position = br, Color = color, UV = new(1.0f, 1.0f) });
            _vertices.Add(new VertexPCT { Position = tr, Color = color, UV = new(1.0f, 0.0f) });

            _vertices.Add(new VertexPCT { Position = bl, Color = color, UV = new(0.0f, 1.0f) });
            _vertices.Add(new VertexPCT { Position = tr, Color = color, UV = new(1.0f, 0.0f) });
            _vertices.Add(new VertexPCT { Position = tl, Color = color, UV = new(0.0f, 0.0f) });
        }

        public void AddRotatedRectangle(Vector2 center, Vector2 size, float angle, Vector3 color)
        {
            Vector2 halfSize = size * 0.5f;

            Vector2 tl = center + new Vector2(-halfSize.X, -halfSize.Y).Rotate(angle);
            Vector2 tr = center + new Vector2(halfSize.X, -halfSize.Y).Rotate(angle);
            Vector2 br = center + new Vector2(halfSize.X, halfSize.Y).Rotate(angle);
            Vector2 bl = center + new Vector2(-halfSize.X, halfSize.Y).Rotate(angle);

            _vertices.Add(new VertexPCT { Position = bl, Color = color, UV = new(0.0f, 1.0f) });
            _vertices.Add(new VertexPCT { Position = br, Color = color, UV = new(1.0f, 1.0f) });
            _vertices.Add(new VertexPCT { Position = tr, Color = color, UV = new(1.0f, 0.0f) });

            _vertices.Add(new VertexPCT { Position = bl, Color = color, UV = new(0.0f, 1.0f) });
            _vertices.Add(new VertexPCT { Position = tr, Color = color, UV = new(1.0f, 0.0f) });
            _vertices.Add(new VertexPCT { Position = tl, Color = color, UV = new(0.0f, 0.0f) });
        }

        public void AddRectangleWithUV(Vector2 center, Vector2 size, Vector3 color, Vector2 uvMin, Vector2 uvMax)
        {
            Vector2 halfSize = size * 0.5f;

            Vector2 tl = new(center.X - halfSize.X, center.Y - halfSize.Y);
            Vector2 tr = new(center.X + halfSize.X, center.Y - halfSize.Y);
            Vector2 br = new(center.X + halfSize.X, center.Y + halfSize.Y);
            Vector2 bl = new(center.X - halfSize.X, center.Y + halfSize.Y);

            _vertices.Add(new VertexPCT { Position = bl, Color = color, UV = new(uvMin.X, uvMax.Y) });
            _vertices.Add(new VertexPCT { Position = br, Color = color, UV = new(uvMax.X, uvMax.Y) });
            _vertices.Add(new VertexPCT { Position = tr, Color = color, UV = new(uvMax.X, uvMin.Y) });
            _vertices.Add(new VertexPCT { Position = bl, Color = color, UV = new(uvMin.X, uvMax.Y) });
            _vertices.Add(new VertexPCT { Position = tr, Color = color, UV = new(uvMax.X, uvMin.Y) });
            _vertices.Add(new VertexPCT { Position = tl, Color = color, UV = new(uvMin.X, uvMin.Y) });
        }

        public void AddText(Font font, Vector2 position, float scale, string text)
        {
            Vector3 color = new Vector3(1, 1, 1);

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
                float yb = currentPosition.Y + font.MetaData.LineBase * scale;

                Vector2 tl = new(xl, yt);
                Vector2 tr = new(xr, yt);
                Vector2 br = new(xr, yb);
                Vector2 bl = new(xl, yb);

                _vertices.Add(new VertexPCT { Position = bl, Color = color, UV = new(uvMin.X, uvMax.Y) });
                _vertices.Add(new VertexPCT { Position = br, Color = color, UV = new(uvMax.X, uvMax.Y) });
                _vertices.Add(new VertexPCT { Position = tr, Color = color, UV = new(uvMax.X, uvMin.Y) });
                _vertices.Add(new VertexPCT { Position = bl, Color = color, UV = new(uvMin.X, uvMax.Y) });
                _vertices.Add(new VertexPCT { Position = tr, Color = color, UV = new(uvMax.X, uvMin.Y) });
                _vertices.Add(new VertexPCT { Position = tl, Color = color, UV = new(uvMin.X, uvMin.Y) });

                currentPosition.X += charData.XAdvance * scale;
            }
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

    public void Render(Matrix4x4 mvp)
    {
        foreach (var (_, perMaterialData) in _perMaterialDatas)
        {
            int vertexCount = perMaterialData.Vertices.Count;
            if (vertexCount == 0)
                continue;

            var material = perMaterialData.Material;
            var vertexBuffer = perMaterialData.VertexBuffer;
            var vertexArray = perMaterialData.VertexArray;
            var vertices = perMaterialData.Vertices;

            vertexBuffer.SetData(vertices, GL.BufferUsage.STREAM_DRAW);
            vertices.Clear();

            vertexArray.Bind();
            {
                material.SetUniform("uMVP", mvp);
                material.Bind();
                {
                    for (int pass = 1; pass <= material.Passes; pass++)
                    {
                        material.SetPass(pass);
                        vertexArray.Draw(GL.PrimitiveType.TRIANGLES, 0, vertexCount);
                    }
                }
                material.Unbind();
            }
            vertexArray.Unbind();
        }
    }

    public void Dispose()
    {
        foreach (var (_, perMaterialData) in _perMaterialDatas)
        {
            perMaterialData.Dispose();
        }
    }
}
