using System.Numerics;
using System.Runtime.InteropServices;
using Paw.Core.Engine.Assets;
using Paw.Core.Utils;

namespace Paw.Core.Engine;

public unsafe class DynamicGeometryRenderer2D : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    public struct VertexPC
    {
        public Vector2 Position;
        public Vector3 Color;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VertexPCT
    {
        public Vector2 Position;
        public Vector3 Color;
        public Vector2 UV;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VertexFont
    {
        public Vector2 Position;
        public Vector3 Color;
        public Vector2 UV;
    }

    private const int _initialVertexBufferSize = 1024;


    private readonly Shader _shader;
    private readonly Shader _textureShader;
    private readonly Shader _fontShader;

    private readonly Texture _texture1;
    private readonly Texture _texture2;

    private readonly Font _font1;

    private readonly Material _material1;

    private readonly Dictionary<string, Material> _materials = [];
    
    // TODO: vertex buffer per material
    // - what format?
    // - what about index buffers?

    private readonly BufferObject _vertexBufferObject;
    private readonly VertexArrayObject<VertexPC> _vertexArrayObject;
    private readonly List<VertexPC> _vertexBuffer = new(_initialVertexBufferSize);

    private readonly BufferObject _textureVertexBufferObject;
    private readonly VertexArrayObject<VertexPCT> _textureVertexArrayObject;
    private readonly List<VertexPCT> _textureVertexBuffer = new(_initialVertexBufferSize);

    private readonly BufferObject _fontVertexBufferObject;
    private readonly VertexArrayObject<VertexPCT> _fontVertexArrayObject;
    private readonly List<VertexPCT> _fontVertexBuffer = new(_initialVertexBufferSize);

    public DynamicGeometryRenderer2D(AssetManager assetManager)
        :this(assetManager, [])
    {
    }
    
    public DynamicGeometryRenderer2D(AssetManager assetManager, IReadOnlyList<string> materials)
    {
        _shader = assetManager.LoadShader("triangle");
        _textureShader = assetManager.LoadShader("texture");
        _fontShader = assetManager.LoadShader("font");

        _texture1 = assetManager.LoadTexture("1");
        _texture2 = assetManager.LoadTexture("2");

        _font1 = assetManager.LoadFont("font1");

        foreach (var materialId in materials)
        {
            var material = assetManager.LoadMaterial(materialId);
            _materials.Add(materialId, material);
        }
        
        _material1 = assetManager.LoadMaterial("block");

        _vertexBufferObject = new BufferObject(assetManager.GL);
        _vertexBufferObject.SetSizeAndUsage(sizeof(VertexPC) * _initialVertexBufferSize, GL.BufferUsage.STREAM_DRAW);
        _vertexArrayObject = new VertexArrayObject<VertexPC>(assetManager.GL, _vertexBufferObject);

        _textureVertexBufferObject = new BufferObject(assetManager.GL);
        _textureVertexBufferObject.SetSizeAndUsage(sizeof(VertexPCT) * _initialVertexBufferSize, GL.BufferUsage.STREAM_DRAW);
        _textureVertexArrayObject = new VertexArrayObject<VertexPCT>(assetManager.GL, _textureVertexBufferObject);

        _fontVertexBufferObject = new BufferObject(assetManager.GL);
        _fontVertexBufferObject.SetSizeAndUsage(sizeof(VertexPCT) * _initialVertexBufferSize, GL.BufferUsage.STREAM_DRAW);
        _fontVertexArrayObject = new VertexArrayObject<VertexPCT>(assetManager.GL, _fontVertexBufferObject);
    }
    
    public void AddTriangle(Vector2 p1, Vector3 c1, Vector2 p2, Vector3 c2, Vector2 p3, Vector3 c3)
    {
        _vertexBuffer.Add(new VertexPC() { Position = p1, Color = c1 });
        _vertexBuffer.Add(new VertexPC() { Position = p2, Color = c2 });
        _vertexBuffer.Add(new VertexPC() { Position = p3, Color = c3 });
    }

    public void AddRectangle(Vector2 center, Vector2 size, Vector3 color)
    {
        Vector2 halfSize = size * 0.5f;

        Vector2 tl = new(center.X - halfSize.X, center.Y - halfSize.Y);
        Vector2 tr = new(center.X + halfSize.X, center.Y - halfSize.Y);
        Vector2 br = new(center.X + halfSize.X, center.Y + halfSize.Y);
        Vector2 bl = new(center.X - halfSize.X, center.Y + halfSize.Y);

        _vertexBuffer.Add(new VertexPC { Position = bl, Color = color });
        _vertexBuffer.Add(new VertexPC { Position = br, Color = color });
        _vertexBuffer.Add(new VertexPC { Position = tr, Color = color });

        _vertexBuffer.Add(new VertexPC { Position = bl, Color = color });
        _vertexBuffer.Add(new VertexPC { Position = tr, Color = color });
        _vertexBuffer.Add(new VertexPC { Position = tl, Color = color });
    }

    public void AddRotatedRectangle(Vector2 center, Vector2 size, float angle, Vector3 color)
    {
        Vector2 halfSize = size * 0.5f;

        Vector2 tl = center + new Vector2(-halfSize.X, -halfSize.Y).Rotate(angle);
        Vector2 tr = center + new Vector2( halfSize.X, -halfSize.Y).Rotate(angle);
        Vector2 br = center + new Vector2( halfSize.X,  halfSize.Y).Rotate(angle);
        Vector2 bl = center + new Vector2(-halfSize.X,  halfSize.Y).Rotate(angle);
        
        _vertexBuffer.Add(new VertexPC { Position = bl, Color = color });
        _vertexBuffer.Add(new VertexPC { Position = br, Color = color });
        _vertexBuffer.Add(new VertexPC { Position = tr, Color = color });

        _vertexBuffer.Add(new VertexPC { Position = bl, Color = color });
        _vertexBuffer.Add(new VertexPC { Position = tr, Color = color });
        _vertexBuffer.Add(new VertexPC { Position = tl, Color = color });
    }
    
    public void AddRectangleWithTexture(Vector2 center, Vector2 size, Vector3 color, Vector2 uvMin, Vector2 uvMax)
    {
        Vector2 halfSize = size * 0.5f;

        Vector2 tl = new(center.X - halfSize.X, center.Y - halfSize.Y);
        Vector2 tr = new(center.X + halfSize.X, center.Y - halfSize.Y);
        Vector2 br = new(center.X + halfSize.X, center.Y + halfSize.Y);
        Vector2 bl = new(center.X - halfSize.X, center.Y + halfSize.Y);

        _textureVertexBuffer.Add(new VertexPCT { Position = bl, Color = color, UV = new(uvMin.X, uvMax.Y) });
        _textureVertexBuffer.Add(new VertexPCT { Position = br, Color = color, UV = new(uvMax.X, uvMax.Y) });
        _textureVertexBuffer.Add(new VertexPCT { Position = tr, Color = color, UV = new(uvMax.X, uvMin.Y) });
        _textureVertexBuffer.Add(new VertexPCT { Position = bl, Color = color, UV = new(uvMin.X, uvMax.Y) });
        _textureVertexBuffer.Add(new VertexPCT { Position = tr, Color = color, UV = new(uvMax.X, uvMin.Y) });
        _textureVertexBuffer.Add(new VertexPCT { Position = tl, Color = color, UV = new(uvMin.X, uvMin.Y) });
    }
    
    public void AddText(Vector2 position, float scale, string text)
    {
        var font = _font1;

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

            _fontVertexBuffer.Add(new VertexPCT { Position = bl, Color = color, UV = new(uvMin.X, uvMax.Y) });
            _fontVertexBuffer.Add(new VertexPCT { Position = br, Color = color, UV = new(uvMax.X, uvMax.Y) });
            _fontVertexBuffer.Add(new VertexPCT { Position = tr, Color = color, UV = new(uvMax.X, uvMin.Y) });
            _fontVertexBuffer.Add(new VertexPCT { Position = bl, Color = color, UV = new(uvMin.X, uvMax.Y) });
            _fontVertexBuffer.Add(new VertexPCT { Position = tr, Color = color, UV = new(uvMax.X, uvMin.Y) });
            _fontVertexBuffer.Add(new VertexPCT { Position = tl, Color = color, UV = new(uvMin.X, uvMin.Y) });

            currentPosition.X += charData.XAdvance * scale;
        }
    }

    public void Render(Matrix4x4 mvp)
    {
        if (_vertexBuffer.Count > 0)
        {
            ReadOnlySpan<VertexPC> span = CollectionsMarshal.AsSpan(_vertexBuffer);

            _vertexBufferObject.SetData(span, GL.BufferUsage.STREAM_DRAW);

            //_shader.Use();
            //_shader.SetUniform("uMVP", mvp);

            _material1.Bind();
            _material1.SetUniform("uMVP", mvp);
            
            _vertexArrayObject.Bind();
            _vertexArrayObject.Draw(GL.PrimitiveType.TRIANGLES, 0, (uint)_vertexBuffer.Count);
            _vertexArrayObject.Unbind();

            _material1.Unbind();
            
            //_shader.Unuse();

            _vertexBuffer.Clear();
        }

        if (_textureVertexBuffer.Count > 0)
        {
            ReadOnlySpan<VertexPCT> span = CollectionsMarshal.AsSpan(_textureVertexBuffer);

            _textureVertexBufferObject.SetData(span, GL.BufferUsage.STREAM_DRAW);

            _texture1.Bind(0);
            {
                _textureShader.Use();
                _textureShader.SetUniform("uMVP", mvp);
                _textureShader.SetUniform("uTex", 0);
                {
                    _textureVertexArrayObject.Bind();
                    _textureVertexArrayObject.Draw(GL.PrimitiveType.TRIANGLES, 0, (uint)_textureVertexBuffer.Count);
                    _textureVertexArrayObject.Unbind();
                }
                _textureShader.Unuse();
            }
            _texture1.Unbind(0);

            _textureVertexBuffer.Clear();
        }

        if (_fontVertexBuffer.Count > 0)
        {
            ReadOnlySpan<VertexPCT> span = CollectionsMarshal.AsSpan(_fontVertexBuffer);

            _fontVertexBufferObject.SetData(span, GL.BufferUsage.STREAM_DRAW);

            _font1.Texture.Bind(0);
            {
                _fontShader.Use();
                _fontShader.SetUniform("uMVP", mvp);
                _fontShader.SetUniform("uTex", 0);
                {
                    _fontVertexArrayObject.Bind();
                    {
                        _fontShader.SetUniform("uPass1", 1.0f);
                        _fontShader.SetUniform("uPass2", 0.0f);
                        _fontVertexArrayObject.Draw(GL.PrimitiveType.TRIANGLES, 0, (uint)_fontVertexBuffer.Count);

                        _fontShader.SetUniform("uPass1", 0.0f);
                        _fontShader.SetUniform("uPass2", 1.0f);
                        _fontVertexArrayObject.Draw(GL.PrimitiveType.TRIANGLES, 0, (uint)_fontVertexBuffer.Count);
                    }
                    _fontVertexArrayObject.Unbind();
                }
                _fontShader.Unuse();
            }
            _font1.Texture.Unbind(0);

            _fontVertexBuffer.Clear();
        }
    }

    public void Dispose()
    {
        _vertexArrayObject.Dispose();
        _vertexBufferObject.Dispose();

        _textureVertexArrayObject.Dispose();
        _textureVertexBufferObject.Dispose();

        _fontVertexArrayObject.Dispose();
        _fontVertexBufferObject.Dispose();
    }
}
