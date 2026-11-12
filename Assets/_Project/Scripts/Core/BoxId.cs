using System;

namespace Pvz.Core
{
    /// <summary>
    /// Identifier encoded in a box QR code, e.g. "BOX-001". Normalised to trimmed upper case,
    /// so "box-001 " and "BOX-001" are the same box.
    /// </summary>
    public readonly struct BoxId : IEquatable<BoxId>
    {
        private readonly string _value;

        public BoxId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Box id must not be empty.", nameof(value));
            _value = value.Trim().ToUpperInvariant();
        }

        public string Value => _value ?? string.Empty;

        public bool IsEmpty => string.IsNullOrEmpty(_value);

        public static BoxId Parse(string value) => new BoxId(value);

        public static bool TryParse(string value, out BoxId id)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                id = default;
                return false;
            }
            id = new BoxId(value);
            return true;
        }

        public bool Equals(BoxId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is BoxId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;

        public static bool operator ==(BoxId left, BoxId right) => left.Equals(right);
        public static bool operator !=(BoxId left, BoxId right) => !left.Equals(right);
    }
}
