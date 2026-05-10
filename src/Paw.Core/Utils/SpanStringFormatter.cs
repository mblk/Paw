using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Paw.Core.Utils;

public readonly ref struct SpanStringFormatter // TODO remove or cleanup
{
    private readonly Span<char> _buffer;
    private readonly IFormatProvider? _formatProvider;

    public SpanStringFormatter(Span<char> buffer, IFormatProvider? formatProvider = null)
    {
        _buffer = buffer;
        _formatProvider = formatProvider;
    }

    public ReadOnlySpan<char> Format([InterpolatedStringHandlerArgument("")] ref AppendInterpolatedStringHandler handler)
    {
        return _buffer[..handler.NumChars];
    }

    [InterpolatedStringHandler]
    public ref struct AppendInterpolatedStringHandler
    {
        private readonly Span<char> _buffer;
        private readonly IFormatProvider? _formatProvider;

        private int _numChars;

        public readonly int NumChars => _numChars;

        public AppendInterpolatedStringHandler(int literalLength, int formattedCount, SpanStringFormatter parent)
        {
            _buffer = parent._buffer;
            _formatProvider = parent._formatProvider;
            _numChars = 0;
        }

        public void AppendLiteral(string value)
        {
            Append(value);
        }

        public void AppendFormatted(string? value)
        {
            if (!string.IsNullOrEmpty(value))
                Append(value);
        }

        [Obsolete("implicit string creation")]
        public void AppendFormatted(object? value)
        {
            if (value is not null)
                Append(value.ToString());
        }

        public void AppendFormatted(ReadOnlySpan<char> value)
        {
            Append(value);
        }

        public void AppendFormatted(char value)
        {
            if (_numChars < _buffer.Length)
                _buffer[_numChars++] = value;
            else
                ReportError();
        }

        public void AppendFormatted<T>(T value, string? format = null)
            where T : ISpanFormattable
        {
            Span<char> target = _buffer[_numChars..];

            if (value.TryFormat(target, out int charsWritten, format, _formatProvider))
                _numChars += charsWritten;
            else
                ReportError();
        }

        private void Append(ReadOnlySpan<char> value)
        {
            if (value.TryCopyTo(_buffer[_numChars..]))
                _numChars += value.Length;
            else
                ReportError();
        }

        [Conditional("DEBUG")]
        private static void ReportError()
        {
            if (Debugger.IsAttached)
                Debugger.Break();
        }
    }
}
