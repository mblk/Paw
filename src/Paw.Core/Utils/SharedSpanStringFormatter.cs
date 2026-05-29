using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Paw.Core.Utils;

public static class SharedSpanStringFormatter
{
    private static readonly char[] _buffer = new char[1024];

    public static ReadOnlySpan<char> Format([InterpolatedStringHandlerArgument()] ref AppendInterpolatedStringHandler handler)
    {
        // TODO verify single threaded access
        // TODO or add per-thread buffer

        return _buffer.AsSpan()[..handler.NumChars];
    }

    [InterpolatedStringHandler]
    public ref struct AppendInterpolatedStringHandler
    {
        private readonly Span<char> _buffer;
        private readonly IFormatProvider? _formatProvider = null; // ?

        private int _numChars;

        public readonly int NumChars => _numChars;

        public AppendInterpolatedStringHandler(int literalLength, int formattedCount)
        {
            _buffer = SharedSpanStringFormatter._buffer;
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
            if (value.TryFormat(_buffer[_numChars..], out int charsWritten, format, _formatProvider))
                _numChars += charsWritten;
            else
                ReportError();
        }

        public void AppendFormatted(vec2 vec, string? format = null)
        {
            Append("[");
            AppendFormatted(vec.X, format);
            Append(";");
            AppendFormatted(vec.Y, format);
            Append("]");
        }

        public void AppendFormatted(vec3 vec, string? format = null)
        {
            Append("[");
            AppendFormatted(vec.X, format);
            Append(";");
            AppendFormatted(vec.Y, format);
            Append(";");
            AppendFormatted(vec.Z, format);
            Append("]");
        }

        public void AppendFormatted(vec4 vec, string? format = null)
        {
            Append("[");
            AppendFormatted(vec.X, format);
            Append(";");
            AppendFormatted(vec.Y, format);
            Append(";");
            AppendFormatted(vec.Z, format);
            Append(";");
            AppendFormatted(vec.W, format);
            Append("]");
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
