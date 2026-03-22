using System.Buffers.Binary;
using System.IO.Compression;

namespace Paw.Core.Engine;

public class TextureLoader : AssetLoader<Texture>
{
    public TextureLoader(IAssetManager assetManager, IAssetReader assetReader, GL gl)
        : base(assetManager, assetReader, gl)
    {
    }

    public override AssetLoadResult<Texture> Load(string name)
    {
        var path = Reader.GetAssetPath(AssetType.Texture, $"{name}.png");
        byte[] data = Reader.ReadFileAsBytes(path);

        var imageLoader = new PngImageLoader();
        var imageLoadResult = imageLoader.Load(data, false);

        var texture = new Texture(GL, imageLoadResult.Width, imageLoadResult.Height, imageLoadResult.Data);

        var sourceFiles = new HashSet<string>() { path };

        return new AssetLoadResult<Texture>(texture, sourceFiles);
    }

    public override AssetLoadResult<Texture> Reload(Texture asset)
    {
        throw new NotImplementedException();
    }
}

public unsafe class Texture : Asset, IDisposable
{
    private readonly GL _gl;
    private readonly GL.TextureId _id;

    public Texture(GL gl, int width, int height, ReadOnlySpan<byte> data) // uses DSA
    {
        _gl = gl;

        GL.TextureId id = default;
        _gl.CreateTextures(GL.TextureTarget.TEXTURE_2D, 1, &id);
        _id = id;

        int levels = (int)Math.Floor(Math.Log2(Math.Max(width, height))) + 1;
        Console.WriteLine($"Texture size {width}x{height} -> using levels: {levels}");

        _gl.TextureStorage2D(id, levels, GL.SizedInternalFormat.RGBA8, width, height);

        _gl.TextureParameteri(id, GL.TextureParameterName.TEXTURE_MIN_FILTER, (int)GL.TextureMinFilter.LINEAR);
        _gl.TextureParameteri(id, GL.TextureParameterName.TEXTURE_MAG_FILTER, (int)GL.TextureMinFilter.LINEAR);
        _gl.TextureParameteri(id, GL.TextureParameterName.TEXTURE_WRAP_S, (int)GL.TextureWrapMode.CLAMP_TO_EDGE);
        _gl.TextureParameteri(id, GL.TextureParameterName.TEXTURE_WRAP_T, (int)GL.TextureWrapMode.CLAMP_TO_EDGE);

        fixed (void* pData = data)
        {
            _gl.TextureSubImage2D(id, 0, 0, 0, width, height, GL.PixelFormat.RGBA, GL.PixelType.UNSIGNED_BYTE, pData);
        }

        _gl.GenerateTextureMipmap(id);
    }

    public void Dispose()
    {
        GL.TextureId texId = _id;
        _gl.DeleteTextures(1, &texId);
    }

    public void Bind(int unit)
    {
        _gl.BindTextureUnit((uint)unit, _id);
    }

    public void Unbind(int unit)
    {
        _gl.BindTextureUnit((uint)unit, default);
    }
}

public abstract class ImageLoader
{
    public record ImageLoadResult(int Width, int Height, byte[] Data);

    public abstract ImageLoadResult Load(ReadOnlySpan<byte> data, bool flipY);
}

public class PngImageLoader : ImageLoader
{
    //
    // spec: https://www.w3.org/TR/png-3/
    //

    private static readonly byte[] _signature = [ 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A ];

    // Length + Type + CRC
    private static readonly int _minimumChunkSize = 12;

    // IHDR + IDAT + IEND
    private static readonly int _minimumValidLength = _signature.Length + 3 * _minimumChunkSize;

    private readonly ref struct Chunk
    {
        public readonly uint Length;
        public readonly string Type;
        public readonly ReadOnlySpan<byte> Data;
        public readonly uint CRC;

        public Chunk(uint length, string type, ReadOnlySpan<byte> data, uint crc)
        {
            Length = length;
            Type = type;
            Data = data;
            CRC = crc;
        }
    }

    private class Header
    {
        public uint Width;
        public uint Height;
        public byte BitDepth;
        public byte ColorType;
        public byte Compression;
        public byte Filter;
        public byte Interlace;
    }

    public PngImageLoader()
    {
    }

    public override ImageLoadResult Load(ReadOnlySpan<byte> data, bool flipY)
    {
        CheckSignature(data);
        ProcessChunks(data, out Header header, out Span<byte> compressedData);

        int width = (int)header.Width;
        int height = (int)header.Height;

        Span<byte> decompressedData = DecompressData(compressedData);
        Span<byte> reconstructedData = ReconstructImage(decompressedData, width, height);

        if (flipY)
        {
            FlipY(reconstructedData, width, height);
        }

        return new ImageLoadResult(width, height, reconstructedData.ToArray());
    }

    private static void CheckSignature(ReadOnlySpan<byte> data)
    {
        if (data.Length < _minimumValidLength)
            throw new Exception($"Data too short");

        if (!data[0.._signature.Length].SequenceEqual(_signature))
            throw new Exception($"Invalid signature");
    }

    private static void ProcessChunks(ReadOnlySpan<byte> data, out Header outHeader, out Span<byte> outCompressedImageData)
    {
        ReadOnlySpan<byte> next = data[_signature.Length..];

        Header? header = null;
        bool foundEnd = false;
        List<byte> compressedImageData = new List<byte>(data.Length);

        while (next.Length >= _minimumChunkSize)
        {
            Chunk chunk = ReadNextChunk(ref next);

            switch (chunk.Type)
            {
                case "IHDR":
                {
                    if (header is not null) throw new Exception($"Found multiple IHDR chunks");

                    header = new Header()
                    {
                        Width = BinaryPrimitives.ReadUInt32BigEndian(chunk.Data[0..4]),
                        Height = BinaryPrimitives.ReadUInt32BigEndian(chunk.Data[4..8]),
                        BitDepth = chunk.Data[8],
                        ColorType = chunk.Data[9],
                        Compression = chunk.Data[10],
                        Filter = chunk.Data[11],
                        Interlace = chunk.Data[12],
                    };
                    break;
                }

                case "IDAT":
                {
                    compressedImageData.AddRange(chunk.Data);
                    break;
                }

                case "IEND":
                {
                    if (foundEnd) throw new Exception($"Found multiple IEND chunks");

                    foundEnd = true;
                    break;
                }

                default:
                {
                    Console.WriteLine($"Ignoring Chunk: {chunk.Type}");
                    break;
                }
            }
        }

        if (header is null) throw new Exception($"Missing IHDR chunk");
        if (!foundEnd) throw new Exception($"Missing IEND chunk");

        Console.WriteLine($"Header:");
        Console.WriteLine($"  Size:        {header.Width}x{header.Height}");
        Console.WriteLine($"  BitDepth:    {header.BitDepth}");
        Console.WriteLine($"  ColorType:   {header.ColorType}");
        Console.WriteLine($"  Compression: {header.Compression}");
        Console.WriteLine($"  Filter:      {header.Filter}");
        Console.WriteLine($"  Interlace:   {header.Interlace}");

        Console.WriteLine($"Data: {compressedImageData.Count} bytes");

        if (header.BitDepth != 8) throw new Exception($"Unsupported bit depth {header.BitDepth}, must be 8");
        if (header.ColorType != 6) throw new Exception($"Unsupported color type {header.ColorType}, must be 6 (RGBA)");
        if (header.Compression != 0) throw new Exception($"Unsupported compression method {header.Compression}, must be 0");
        if (header.Filter != 0) throw new Exception($"Unsupported filter method {header.Filter}, must be 0");
        if (header.Interlace != 0) throw new Exception($"Unsupported interlace method {header.Interlace}, must be 0");

        outHeader = header;
        outCompressedImageData = compressedImageData.ToArray();
    }

    private static Chunk ReadNextChunk(ref ReadOnlySpan<byte> next)
    {
        uint length = ReadUint32(ref next);
        string type = ReadString(ref next, 4);
        ReadOnlySpan<byte> chunkData = ReadBytes(ref next, (int)length);
        uint crc = ReadUint32(ref next);

        return new Chunk(length, type, chunkData, crc);
    }

    private static uint ReadUint32(ref ReadOnlySpan<byte> next)
    {
        // Big endian first
        uint value = ((uint)next[0] << 24) | ((uint)next[1] << 16) | ((uint)next[2] << 8) | (uint)next[3];
        next = next[4..];
        return value;
    }

    private static ReadOnlySpan<byte> ReadBytes(ref ReadOnlySpan<byte> input, int length)
    {
        ReadOnlySpan<byte> result = input[0..length];
        input = input[length..];
        return result;
    }

    private static string ReadString(ref ReadOnlySpan<byte> next, int length)
    {
        string value = "";
        for (int i = 0; i < length; i++)
            value += (char)next[i];
        next = next[length..];
        return value;
    }

    private unsafe static Span<byte> DecompressData(ReadOnlySpan<byte> compressedData)
    {
        fixed (byte* pData = compressedData)
        {
            using var inputStream = new UnmanagedMemoryStream(pData, compressedData.Length);
            using var zlibStream = new ZLibStream(inputStream, CompressionMode.Decompress, false);
            using var outputStream = new MemoryStream();

            zlibStream.CopyTo(outputStream);

            float factor = (float)outputStream.Length / (float)compressedData.Length;
            Console.WriteLine($"Decompressed image data from {compressedData.Length} to {outputStream.Length} bytes (factor {factor:F2})");

            return outputStream.ToArray();
        }
    }

    private static Span<byte> ReconstructImage(ReadOnlySpan<byte> filteredDataFull, int width, int height)
    {
        int bytesPerLine = width * 4;
        int bytesPerLineWithFilter = bytesPerLine + 1;

        Span<byte> reconDataFull = new byte[bytesPerLine * height];
        ReadOnlySpan<byte> prevReconLine = new byte[bytesPerLine];

        for (int line = 0; line < height; line++)
        {
            ReadOnlySpan<byte> filteredLineWithFilter = filteredDataFull.Slice(line * bytesPerLineWithFilter, bytesPerLineWithFilter);
            ReadOnlySpan<byte> filteredLine = filteredLineWithFilter[1..];
            Span<byte> reconLine = reconDataFull.Slice(line * bytesPerLine, bytesPerLine);

            byte filterType = filteredLineWithFilter[0]; // https://www.w3.org/TR/png-3/#9Filter-types

            for (int i = 0; i < bytesPerLine; i++)
            {
                byte x = filteredLine[i]; // Filt(x)
                byte b = prevReconLine[i]; // Recon(b)
                byte a = (i >= 4) ? reconLine[i - 4] : (byte)0; // Recon(a)
                byte c = (i >= 4) ? prevReconLine[i - 4] : (byte)0; // Recon(c)

                reconLine[i] = (byte)(filterType switch
                {
                    0 => x,
                    1 => x + a,
                    2 => x + b,
                    3 => x + ((a + b) >> 1),
                    4 => x + Paeth(a, b, c),
                    _ => throw new Exception($"Filter type {filterType} not supported"),
                });
            }
            prevReconLine = reconLine;
        }

        return reconDataFull;
    }

    private static byte Paeth(byte a, byte b, byte c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);

        if (pa <= pb && pa <= pc) return a;
        if (pb <= pc) return b;
        return c;
    }

    private static void FlipY(Span<byte> data, int width, int height)
    {
        int bytesPerLine = width * 4;

        Span<byte> temp = new byte[bytesPerLine];

        for (int y1 = 0; y1 < height / 2; y1++)
        {
            int y2 = height - y1 - 1;

            Span<byte> data1 = data.Slice(y1 * bytesPerLine, bytesPerLine);
            Span<byte> data2 = data.Slice(y2 * bytesPerLine, bytesPerLine);

            data1.CopyTo(temp);
            data2.CopyTo(data1);
            temp.CopyTo(data2);
        }
    }
}
