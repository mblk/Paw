using System.Diagnostics;

namespace Paw.Core.Utils;

public ref struct SpanStringBuilder
{
    private readonly Span<char> _buffer;
    private int _numChars;

    public SpanStringBuilder(Span<char> buffer)
    {
        _buffer = buffer;
        _numChars = 0;
    }

    public static implicit operator ReadOnlySpan<char>(SpanStringBuilder sb) => sb.GetSpan();

    public void Clear()
    {
        _numChars = 0;
    }

    public ReadOnlySpan<char> GetSpan()
    {
        return _buffer[0.._numChars];
    }

    public void Append(ReadOnlySpan<char> text)
    {
        Span<char> target = _buffer[_numChars..];

        if (text.TryCopyTo(target))
        {
            _numChars += text.Length;
        }
        else
        {
            Debugger.Break();
        }
    }

    public void Append(int value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null)
    {
        Span<char> target = _buffer[_numChars..];

        if (value.TryFormat(target, out int charWritten, format, formatProvider))
        {
            _numChars += charWritten;
        }
        else
        {
            Debugger.Break();
        }
    }

    public void Append(float value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null)
    {
        Span<char> target = _buffer[_numChars..];

        if (value.TryFormat(target, out int charWritten, format, formatProvider))
        {
            _numChars += charWritten;
        }
        else
        {
            Debugger.Break();
        }
    }

    public void Append(double value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null)
    {
        Span<char> target = _buffer[_numChars..];

        if (value.TryFormat(target, out int charWritten, format, formatProvider))
        {
            _numChars += charWritten;
        }
        else
        {
            Debugger.Break();
        }
    }




}
