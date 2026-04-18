using Paw.Core.Graphics;
using Paw.Core.Resources;
using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Paw.Core.Assets;

public class FontLoader : AssetLoader<Font>
{
    public FontLoader(IAssetManager assetManager, IAssetReader assetReader, GL gl)
        : base(assetManager, assetReader, gl)
    {
    }

    public override AssetLoadResult<Font> Load(string name)
    {
        var metaDataPath = Reader.GetAssetPath(AssetType.Font, $"{name}.fnt");
        byte[] metaDataBytes = Reader.ReadFileAsBytes(metaDataPath);

        var metaData = FontMetaDataLoader.Load(metaDataBytes);

        var sourceFiles = new HashSet<string>() { metaDataPath };

        var font = new Font(metaData);

        return new AssetLoadResult<Font>(font, sourceFiles);
    }

    public override AssetLoadResult<Font> Reload(Font asset)
    {
        throw new NotImplementedException();
    }
}

public class FontMetaData
{
    public class CharacterData
    {
        public required Vector2 UvMin { get; init; }
        public required Vector2 UvMax { get; init; }
        public required Vector2 Size { get; init; }
        public required Vector2 Offset { get; init; }
        public required float XAdvance { get; init; }
    }

    public required int LineHeight { get; init; }
    public required int LineBase { get; init; }
    public required int TextureWidth { get; init; }
    public required int TextureHeight { get; init; }

    public required IReadOnlyDictionary<uint, CharacterData> Characters { get; init; }
}

public static class FontMetaDataLoader
{
    //
    // spec: https://www.angelcode.com/products/bmfont/doc/file_format.html
    //

    private static readonly byte[] _signature = [0x42, 0x4D, 0x46, 0x03];

    private const int _minimumSize = 10;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct InfoBlock
    {
        public short FontSize;
        public byte BitField;
        public byte ChatSet;
        public ushort StretchH;
        public byte AA;
        public byte PaddingUp;
        public byte PaddingRight;
        public byte PaddingDown;
        public byte PaddingLeft;
        public byte SpacingHoriz;
        public byte SpacingVert;
        public byte Outline;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct CommonBlock
    {
        public ushort LineHeight;
        public ushort Base;
        public ushort ScaleW;
        public ushort ScaleH;
        public ushort Pages;
        public byte BitField;
        public byte AlphaChannel;
        public byte RedChannel;
        public byte GreenChannel;
        public byte BlueChannel;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct CharInfo
    {
        public uint Id;
        public ushort X;
        public ushort Y;
        public ushort Width;
        public ushort Height;
        public short XOffset;
        public short YOffset;
        public short XAdvance;
        public byte Page;
        public byte Channel;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct KerningPair
    {
        public uint First;
        public uint Second;
        public short Amount;
    }

    public unsafe static FontMetaData Load(ReadOnlySpan<byte> data)
    {
        Console.WriteLine($"FontMetaData: Got {data.Length} bytes");

        Debug.Assert(sizeof(InfoBlock) == 14);
        Debug.Assert(sizeof(CommonBlock) == 15);
        Debug.Assert(sizeof(CharInfo) == 20);
        Debug.Assert(sizeof(KerningPair) == 10);

        if (data.Length < _minimumSize)
            throw new Exception("Data is too short");

        if (!data[..4].SequenceEqual(_signature))
            throw new Exception("Invalid signature");

        // xxx
        var characters = new Dictionary<uint, FontMetaData.CharacterData>();
        // xxx

        ReadOnlySpan<byte> next = data.Slice(4);

        bool infoBlockFound = false;
        InfoBlock infoBlock = default;
        bool commonBlockFound = false;
        CommonBlock commonBlock = default;
        var pageNames = new List<string>();

        while (next.Length > 0)
        {
            byte blockType = ReadByte(ref next);
            int blockSize = ReadInt32(ref next);
            ReadOnlySpan<byte> blockData = ReadBytes(ref next, blockSize);

            Console.WriteLine($"Block type={blockType} size={blockSize}");

            fixed (void* ptr = blockData)
            {
                switch (blockType)
                {
                    // info
                    case 1:
                    {
                        infoBlock = *(InfoBlock*)ptr;
                        infoBlockFound = true;

                        var nameBytes = blockData.Slice(sizeof(InfoBlock));
                        var name = Encoding.ASCII.GetString(nameBytes);
                        Console.WriteLine($"Name: '{name}'");
                        break;
                    }

                    // common
                    case 2:
                    {
                        commonBlock = *(CommonBlock*)ptr;
                        commonBlockFound = true;
                        break;
                    }

                    // pages
                    case 3:
                    {
                        ReadOnlySpan<byte> remData = blockData;

                        while (remData.Length > 0)
                        {
                            int endIndex = remData.IndexOf((byte)0);

                            if (endIndex != -1)
                            {
                                var pageName = Encoding.ASCII.GetString(remData[0..endIndex]);
                                Console.WriteLine($"pageName: '{pageName}'");
                                pageNames.Add(pageName);
                                remData = remData.Slice(endIndex + 1);
                            }
                            else
                            {
                                var pageName = Encoding.ASCII.GetString(remData);
                                Console.WriteLine($"pageName: '{pageName}'");
                                pageNames.Add(pageName);
                                remData = remData.Slice(remData.Length);
                            }
                        }

                        break;
                    }

                    // chars
                    case 4:
                    {
                        if (!commonBlockFound) throw new Exception("Common block missing");

                        fixed (void* pStart = blockData)
                        {
                            int numChars = blockSize / sizeof(CharInfo);
                            CharInfo* charInfos = (CharInfo*)pStart;

                            for (int i = 0; i < numChars; i++)
                            {
                                CharInfo ci = charInfos[i];

                                Console.WriteLine($"Char id={ci.Id} x={ci.X} y={ci.Y} w={ci.Width} h={ci.Height} xo={ci.XOffset} yo={ci.YOffset} xadv={ci.XAdvance} page={ci.Page} channel={ci.Channel}");

                                characters.Add(ci.Id, new FontMetaData.CharacterData()
                                {
                                    UvMin = new Vector2((float)ci.X / (float)commonBlock.ScaleW,
                                                        (float)ci.Y / (float)commonBlock.ScaleH),
                                    UvMax = new Vector2((float)(ci.X + ci.Width) / (float)commonBlock.ScaleW,
                                                        (float)(ci.Y + ci.Height) / (float)commonBlock.ScaleH),
                                    Size = new Vector2(ci.Width, ci.Height),
                                    Offset = new Vector2(ci.XOffset, ci.YOffset),
                                    XAdvance = ci.XAdvance,
                                });
                            }
                        }
                        break;
                    }

                    // kerning pairs
                    case 5:
                    {
                        fixed (void* pStart = blockData)
                        {
                            int numPairs = blockSize / sizeof(KerningPair);
                            KerningPair* pairs = (KerningPair*)pStart;

                            for (int i = 0; i < numPairs; i++)
                            {
                                KerningPair kp = pairs[i];

                                Console.WriteLine($"Kerning pair {kp.First} {kp.Second} {kp.Amount}");
                            }
                        }
                        break;
                    }

                    default:
                        throw new Exception($"Invalid block type {blockType}");
                }
            }
        }

        if (!infoBlockFound) throw new Exception("Missing info block");
        if (!commonBlockFound) throw new Exception("Missing common block");

        if (commonBlock.Pages != 1) throw new Exception("Only 1 page is supported");


        Console.WriteLine($"Info block:");
        Console.WriteLine($"  FontSize: {infoBlock.FontSize}");
        Console.WriteLine($"  BitField: {infoBlock.BitField}");
        Console.WriteLine($"  ChatSet: {infoBlock.ChatSet}");
        Console.WriteLine($"  StretchH: {infoBlock.StretchH}");
        Console.WriteLine($"  AA: {infoBlock.AA}");
        Console.WriteLine($"  PaddingUp: {infoBlock.PaddingUp}");
        Console.WriteLine($"  PaddingRight: {infoBlock.PaddingRight}");
        Console.WriteLine($"  PaddingDown: {infoBlock.PaddingDown}");
        Console.WriteLine($"  PaddingLeft: {infoBlock.PaddingLeft}");
        Console.WriteLine($"  SpacingHoriz: {infoBlock.SpacingHoriz}");
        Console.WriteLine($"  SpacingVert: {infoBlock.SpacingVert}");
        Console.WriteLine($"  Outline: {infoBlock.Outline}");

        Console.WriteLine($"Common block:");
        Console.WriteLine($"  LineHeight: {commonBlock.LineHeight}");
        Console.WriteLine($"  Base: {commonBlock.Base}");
        Console.WriteLine($"  ScaleW: {commonBlock.ScaleW}");
        Console.WriteLine($"  ScaleH: {commonBlock.ScaleH}");
        Console.WriteLine($"  Pages: {commonBlock.Pages}");
        Console.WriteLine($"  BitField: {commonBlock.BitField}");
        Console.WriteLine($"  AlphaChannel: {commonBlock.AlphaChannel}");
        Console.WriteLine($"  RedChannel: {commonBlock.RedChannel}");
        Console.WriteLine($"  GreenChannel: {commonBlock.GreenChannel}");
        Console.WriteLine($"  BlueChannel: {commonBlock.BlueChannel}");

        return new FontMetaData()
        {
            LineHeight = commonBlock.LineHeight,
            LineBase = commonBlock.Base,
            TextureWidth = commonBlock.ScaleW,
            TextureHeight = commonBlock.ScaleH,

            Characters = characters.ToFrozenDictionary(),
        };
    }

    private static byte ReadByte(ref ReadOnlySpan<byte> data)
    {
        if (data.Length < 1) throw new Exception("Not enough data left to read byte");

        var value = data[0];
        data = data[1..];
        return value;
    }

    private static int ReadInt32(ref ReadOnlySpan<byte> data)
    {
        if (data.Length < 4) throw new Exception("Not enough data left to read int32");

        var value = BinaryPrimitives.ReadInt32LittleEndian(data[0..4]);
        data = data[4..];
        return value;
    }

    private static ReadOnlySpan<byte> ReadBytes(ref ReadOnlySpan<byte> data, int length)
    {
        if (length < 0) throw new Exception("Length must not be negative");
        if (data.Length < length) throw new Exception($"Not enough data left to read {length} bytes");

        ReadOnlySpan<byte> result = data[0..length];
        data = data[length..];
        return result;
    }
}

public class Font : Asset, IDisposable
{
    public FontMetaData MetaData { get; }

    public Font(FontMetaData metaData)
    {
        MetaData = metaData;
    }

    public void Dispose()
    {
    }
}
