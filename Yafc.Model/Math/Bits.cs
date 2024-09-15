using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Yafc.Model;
public struct Bits {
    private int _length;
    public int length {
        readonly get => _length;
        [MemberNotNull(nameof(data))] // Setting length is guaranteed to set a non-null data.
        private set {
            Array.Resize(ref data, (int)Math.Ceiling(value / 64f));
            _length = value;
        }
    }

    // Turning Bits into a struct this lets us guarantee Bits variables are non-null, but we have to handle data being null instead.
    // Arrays of Bits are initialized to zeros (nulls), even if we added a default constructor that said otherwise.
    private ulong[]? data;

    public bool this[int i] {
        readonly get {
            ArgumentOutOfRangeException.ThrowIfNegative(i, nameof(i));
            if (data is null || length <= i) {
                return false;
            }
            return (data[i / 64] & (1ul << (i % 64))) != 0;
        }
        [MemberNotNull(nameof(data))]
        set {
            ArgumentOutOfRangeException.ThrowIfNegative(i, nameof(i));
            if (length <= i) {
                length = i + 1;
            }
            // null-forgiving: length must be non-zero, meaning data cannot be null.
            if (value) {
                // set bit
                data![i / 64] |= 1ul << (i % 64);
            }
            else {
                // clear bit
                data![i / 64] &= ~(1ul << (i % 64));
            }
        }
    }

    public Bits() { }

    public Bits(bool setBit0) {
        if (setBit0) {
            this[0] = true;
        }
    }

    // Make a copy of Bits
    public Bits(Bits original) {
        data = (ulong[]?)original.data?.Clone();
        _length = original.length;
    }

    public static Bits operator &(Bits a, Bits b) {
        if (a.data is null || b.data is null) {
            return default;
        }

        Bits result = default;
        result.length = Math.Max(a.length, b.length);

        for (int i = 0; i < result.data.Length; i++) {
            if (a.data.Length <= i || b.data.Length <= i) {
                result.data[i] = 0;
            }
            else {
                result.data[i] = a.data[i] & b.data[i];
            }
        }

        return result;
    }

    public static Bits operator |(Bits a, Bits b) {
        if (a.data is null) { return new(b); }
        if (b.data is null) { return new(a); }

        Bits result = default;
        result.length = Math.Max(a.length, b.length);

        for (int i = 0; i < result.data.Length; i++) {
            if (a.data.Length <= i) {
                result.data[i] = b.data[i];
            }
            else if (b.data.Length <= i) {
                result.data[i] = a.data[i];
            }
            else {
                result.data[i] = a.data[i] | b.data[i];
            }
        }

        return result;
    }

    public static Bits operator <<(Bits a, int shift) {
        if (shift != 1) {
            throw new NotImplementedException("only shifting by 1 is supported");
        }
        if (a.data is null) { return default; }

        Bits result = default;
        result.length = a.length + 1;

        // bits that 'fell off' in the previous shifting operation
        ulong carrier = 0ul;
        for (int i = 0; i < a.data.Length; i++) {
            result.data[i] = (a.data[i] << shift) | carrier;
            carrier = a.data[i] & ~(~0ul >> shift); // Mask with 'shift amount of MSB'
        }
        if (carrier != 0) {
            // Reason why only shift == 1 is supported, it is messy to map the separate bits back on data[length] and data[length - 1]
            // Since shift != 1 is never used, its implementation is omitted
            result[result.length] = true;
        }

        return result;
    }

    public static bool operator <(Bits a, Bits b) {
        if (b.data is null) {
            // b doesn't have a value (treat as zero), so a >= b
            return false;
        }

        if (a.data is null) {
            // true if b has a bit set (a == 0 and b > 0)
            return !b.IsClear();
        }

        int maxLength = Math.Max(a.data.Length, b.data.Length);
        for (int i = maxLength - 1; i >= 0; i--) {
            if (a.data.Length <= i) {
                if (b.data[i] != 0) {
                    // b is larger, so a < b
                    return true;
                }
            }
            else if (b.data.Length <= i) {
                if (a.data[i] != 0) {
                    // a is larger, so a > b
                    return false;
                }
            }
            else if (a.data[i] < b.data[i]) {
                return true;
            }
            else if (a.data[i] > b.data[i]) {
                return false;
            }

            // bits are equal, go to next pair
        }

        // a and b are fully equal, so not a < b
        return false;
    }

    public static bool operator >(Bits a, Bits b) {
        if (a.data is null) {
            // a doesn't have a possible value, so b >= a
            return false;
        }

        if (b.data is null) {
            // true if a has a bit set
            return !a.IsClear();
        }

        int maxLength = Math.Max(a.data.Length, b.data.Length);
        for (int i = maxLength - 1; i >= 0; i--) {
            if (a.data.Length <= i) {
                if (b.data[i] != 0) {
                    // b is larger, so a < b
                    return false;
                }
            }
            else if (b.data.Length <= i) {
                if (a.data[i] != 0) {
                    // a is larger, so a > b
                    return true;
                }
            }
            else if (a.data[i] < b.data[i]) {
                return false;
            }
            else if (a.data[i] > b.data[i]) {
                return true;
            }

            // bits are equal, go to next pair
        }

        // a and b are equal, so not a > b
        return false;
    }

    public static Bits operator -(Bits a, ulong b) {
        if (a.IsClear()) {
            throw new ArgumentOutOfRangeException(nameof(a), "a is 0, so the result would become negative!");
        }

        if (b != 1) {
            throw new NotImplementedException("only subtracting by 1 is supported");
        }

        Bits result = new Bits(a);

        // Only works for subtracting by 1!
        // subtract by 1: find lowest bit that is set, unset this bit and set all previous bits
        int index = 0;
        while (result[index] == false) {
            result[index] = true;
            index++;
        }
        result[index] = false;

        return result;
    }


    // Check if the first ulong of a equals to b, rest of a needs to be 0
    public static bool operator ==(Bits a, ulong b) {
        if (a.length == 0) {
            return b == 0;
        }

        if (a.data![0] != b) {
            return false;
        }

        for (int i = 1; i < a.data.Length; i++) {
            if (a.data[i] != 0) {
                return false;
            }
        }

        return true;
    }

    public static bool operator ==(Bits a, Bits b) {
        if (a.data is null && b.data is null) {
            return true;
        }

        if (a.data is null || b.data is null) {
            return false;
        }

        // Check if a and b have the same 'width' (ignoring zeroed bits)
        if (a.HighestBitSet() != b.HighestBitSet()) {
            return false;
        }

        for (int i = Math.Min(a.data.Length, b.data.Length) - 1; i >= 0; i--) {
            if (a.data[i] != b.data[i]) {
                return false;
            }
        }

        return true;
    }


    public static bool operator !=(Bits a, Bits b) => !(a == b);

    public static bool operator !=(Bits a, ulong b) => !(a == b);

    public override readonly bool Equals(object? obj) => obj is Bits b && this == b;

    public override readonly int GetHashCode() {
        int hash = 7;
        unchecked {
            foreach (ulong i in data ?? []) {
                hash = (hash * 31) + (int)i;
            }
        }

        return hash;
    }

    public readonly bool IsClear() => HighestBitSet() == -1;

    public readonly int HighestBitSet() {
        int result = -1;
        if (data is null) {
            return result;
        }
        for (int i = 0; i < data.Length; i++) {
            if (data[i] != 0) {
                // data[i] contains a (new) highest bit
                result = (i * 64) + BitOperations.Log2(data[i]);
            }
        }

        return result;
    }

    public readonly int CompareTo(Bits b) {
        if (this == b) {
            return 0;
        }
        return this < b ? -1 : 1;
    }

    public override readonly string ToString() {
        System.Text.StringBuilder bitsString = new System.Text.StringBuilder(8);

        foreach (ulong bits in data ?? []) {
            _ = bitsString.Append(Convert.ToString((long)bits, 2));
        }

        return bitsString.ToString();
    }
}
