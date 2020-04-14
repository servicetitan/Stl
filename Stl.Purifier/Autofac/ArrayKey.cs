using System;

namespace Stl.Purifier.Autofac
{
    public readonly struct ArrayKey : IEquatable<ArrayKey>
    {
        public object[] Arguments { get; }
        public int UsedArgumentBitmap { get; }
        public int HashCode { get; }

        public ArrayKey(object[] arguments, int usedArgumentBitmap = int.MaxValue)
        {
            UsedArgumentBitmap = usedArgumentBitmap;
            var hashCode = 0;
            for (var i = 0; i < arguments.Length; i++, usedArgumentBitmap >>= 1) {
                if ((usedArgumentBitmap & 1) == 0)
                    continue;
                var item = arguments[i];
                unchecked {
                    hashCode = hashCode * 347 + (item?.GetHashCode() ?? 0);
                }
            }

            HashCode = hashCode;
            Arguments = arguments;
        }

        public override string ToString() => $"[{string.Join(", ", Arguments)}]";

        public bool Equals(ArrayKey other)
        {
            if (HashCode != other.HashCode)
                return false;
            var otherItems = other.Arguments;
            if (ReferenceEquals(Arguments, otherItems))
                return true;
            if (Arguments.Length != otherItems.Length)
                return false;
            var usedArgumentBitmap = UsedArgumentBitmap;
            for (var i = 0; i < Arguments.Length; i++, usedArgumentBitmap >>= 1) {
                if ((usedArgumentBitmap & 1) == 0)
                    continue;
                if (!Equals(Arguments[i], otherItems[i]))
                    return false;
            }
            return true;
        }

        public override bool Equals(object? obj) 
            => obj is ArrayKey other && Equals(other);
        public override int GetHashCode() => HashCode;
        public static bool operator ==(ArrayKey left, ArrayKey right) => left.Equals(right);
        public static bool operator !=(ArrayKey left, ArrayKey right) => !left.Equals(right);
    }
}
