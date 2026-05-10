using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Paw.Core.Utils;

public ref struct SpanStringBuilder // TODO remove or cleanup
{
    private readonly Span<char> _buffer;
    private int _numChars;

    public SpanStringBuilder(Span<char> buffer)
    {
        _buffer = buffer;
        _numChars = 0;
    }

    public static implicit operator ReadOnlySpan<char>(SpanStringBuilder sb) => sb.GetFilledSpan();

    public void Clear()
    {
        _numChars = 0;
    }

    public readonly ReadOnlySpan<char> GetFilledSpan()
    {
        return _buffer[0.._numChars];
    }

    public void Append(ReadOnlySpan<char> text)
    {
        Span<char> target = _buffer[_numChars..];

        if (text.TryCopyTo(target))
            _numChars += text.Length;
        else
            Debugger.Break();
    }

    public void Append(char value)
    {
        if (_numChars < _buffer.Length)
            _buffer[_numChars++] = value;
        else
            Debugger.Break();
    }

    public void Append<T>(T value, ReadOnlySpan<char> format = default, IFormatProvider? formatProvider = null)
        where T : ISpanFormattable
    {
        Span<char> target = _buffer[_numChars..];

        if (value.TryFormat(target, out int charsWritten, format, formatProvider))
            _numChars += charsWritten;
        else
            Debugger.Break();
    }

    public void Append([InterpolatedStringHandlerArgument("")] AppendInterpolatedStringHandler handler)
    {
        _numChars = handler.NumChars;
    }

    [InterpolatedStringHandler]
    public ref struct AppendInterpolatedStringHandler
    {
        private readonly Span<char> _buffer;
        private int _numChars;

        internal readonly int NumChars => _numChars;

        public AppendInterpolatedStringHandler(int literalLength, int formattedCount, SpanStringBuilder builder)
        {
            _buffer = builder._buffer;
            _numChars = builder._numChars;
        }

        public void AppendLiteral(string value)
        {
            AppendFormatted(value);
        }

        public void AppendFormatted(ReadOnlySpan<char> value)
        {
            Span<char> target = _buffer[_numChars..];

            if (value.TryCopyTo(target))
                _numChars += value.Length;
            else
                Debugger.Break();
        }

        public void AppendFormatted(char value)
        {
            if (_numChars < _buffer.Length)
                _buffer[_numChars++] = value;
            else
                Debugger.Break();
        }

        public void AppendFormatted<T>(T value, string? format = null, IFormatProvider? formatProvider = null)
            where T : ISpanFormattable
        {
            Span<char> target = _buffer[_numChars..];

            if (value.TryFormat(target, out int charsWritten, format, formatProvider))
                _numChars += charsWritten;
            else
                Debugger.Break();
        }
    }
}
