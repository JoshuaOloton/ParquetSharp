﻿using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ParquetSharp
{
    /// <summary>
    /// C# types to Parquet physical types write conversion logic.
    /// </summary>
    public static class LogicalWrite<TLogical, TPhysical>
        where TPhysical : unmanaged
    {
        public delegate void Converter(ReadOnlySpan<TLogical> source, Span<short> defLevels, Span<TPhysical> destination, short nullLevel);

        public static Delegate GetConverter(ColumnDescriptor columnDescriptor, ByteBuffer? byteBuffer)
        {
            if (typeof(TLogical) == typeof(bool) ||
                typeof(TLogical) == typeof(int) ||
                typeof(TLogical) == typeof(long) ||
                typeof(TLogical) == typeof(Int96) ||
                typeof(TLogical) == typeof(float) ||
                typeof(TLogical) == typeof(double))
            {
                return LogicalWrite.GetNativeConverter<TPhysical, TPhysical>();
            }

            if (typeof(TLogical) == typeof(bool?) ||
                typeof(TLogical) == typeof(int?) ||
                typeof(TLogical) == typeof(long?) ||
                typeof(TLogical) == typeof(Int96?) ||
                typeof(TLogical) == typeof(float?) ||
                typeof(TLogical) == typeof(double?))
            {
                return LogicalWrite.GetNullableNativeConverter<TPhysical, TPhysical>();
            }

            if (typeof(TLogical) == typeof(sbyte))
            {
                return (LogicalWrite<sbyte, int>.Converter) ((s, _, d, _) => LogicalWrite.ConvertInt8(s, d));
            }

            if (typeof(TLogical) == typeof(sbyte?))
            {
                return (LogicalWrite<sbyte?, int>.Converter) LogicalWrite.ConvertInt8;
            }

            if (typeof(TLogical) == typeof(byte))
            {
                return (LogicalWrite<byte, int>.Converter) ((s, _, d, _) => LogicalWrite.ConvertUInt8(s, d));
            }

            if (typeof(TLogical) == typeof(byte?))
            {
                return (LogicalWrite<byte?, int>.Converter) LogicalWrite.ConvertUInt8;
            }

            if (typeof(TLogical) == typeof(short))
            {
                return (LogicalWrite<short, int>.Converter) ((s, _, d, _) => LogicalWrite.ConvertInt16(s, d));
            }

            if (typeof(TLogical) == typeof(short?))
            {
                return (LogicalWrite<short?, int>.Converter) LogicalWrite.ConvertInt16;
            }

            if (typeof(TLogical) == typeof(ushort))
            {
                return (LogicalWrite<ushort, int>.Converter) ((s, _, d, _) => LogicalWrite.ConvertUInt16(s, d));
            }

            if (typeof(TLogical) == typeof(ushort?))
            {
                return (LogicalWrite<ushort?, int>.Converter) LogicalWrite.ConvertUInt16;
            }

            if (typeof(TLogical) == typeof(uint))
            {
                return LogicalWrite.GetNativeConverter<uint, int>();
            }

            if (typeof(TLogical) == typeof(uint?))
            {
                return LogicalWrite.GetNullableNativeConverter<uint, int>();
            }

            if (typeof(TLogical) == typeof(ulong))
            {
                return LogicalWrite.GetNativeConverter<ulong, long>();
            }

            if (typeof(TLogical) == typeof(ulong?))
            {
                return LogicalWrite.GetNullableNativeConverter<ulong, long>();
            }

            if (typeof(TLogical) == typeof(decimal))
            {
                ValidateDecimalColumn(columnDescriptor);
                if (byteBuffer == null) throw new ArgumentNullException(nameof(byteBuffer));
                var multiplier = Decimal128.GetScaleMultiplier(columnDescriptor.TypeScale);
                return (LogicalWrite<decimal, FixedLenByteArray>.Converter) ((s, _, d, _) => LogicalWrite.ConvertDecimal128(s, d, multiplier, byteBuffer));
            }

            if (typeof(TLogical) == typeof(decimal?))
            {
                ValidateDecimalColumn(columnDescriptor);
                if (byteBuffer == null) throw new ArgumentNullException(nameof(byteBuffer));
                var multiplier = Decimal128.GetScaleMultiplier(columnDescriptor.TypeScale);
                return (LogicalWrite<decimal?, FixedLenByteArray>.Converter) ((s, dl, d, nl) => LogicalWrite.ConvertDecimal128(s, dl, d, multiplier, nl, byteBuffer));
            }

            if (typeof(TLogical) == typeof(Guid))
            {
                if (byteBuffer == null) throw new ArgumentNullException(nameof(byteBuffer));
                return (LogicalWrite<Guid, FixedLenByteArray>.Converter) ((s, _, d, _) => LogicalWrite.ConvertUuid(s, d, byteBuffer));
            }

            if (typeof(TLogical) == typeof(Guid?))
            {
                if (byteBuffer == null) throw new ArgumentNullException(nameof(byteBuffer));
                return (LogicalWrite<Guid?, FixedLenByteArray>.Converter) ((s, dl, d, nl) => LogicalWrite.ConvertUuid(s, dl, d, nl, byteBuffer));
            }

            if (typeof(TLogical) == typeof(Date))
            {
                return LogicalWrite.GetNativeConverter<Date, int>();
            }

            if (typeof(TLogical) == typeof(Date?))
            {
                return LogicalWrite.GetNullableNativeConverter<Date, int>();
            }

            using var logicalType = columnDescriptor.LogicalType;

            if (typeof(TLogical) == typeof(DateTime))
            {
                switch (((TimestampLogicalType) logicalType).TimeUnit)
                {
                    case TimeUnit.Millis:
                        return (LogicalWrite<DateTime, long>.Converter) ((s, _, d, _) => LogicalWrite.ConvertDateTimeMillis(s, d));
                    case TimeUnit.Micros:
                        return (LogicalWrite<DateTime, long>.Converter) ((s, _, d, _) => LogicalWrite.ConvertDateTimeMicros(s, d));
                }
            }

            if (typeof(TLogical) == typeof(DateTimeNanos))
            {
                return LogicalWrite.GetNativeConverter<DateTimeNanos, long>();
            }

            if (typeof(TLogical) == typeof(DateTime?))
            {
                switch (((TimestampLogicalType) logicalType).TimeUnit)
                {
                    case TimeUnit.Millis:
                        return (LogicalWrite<DateTime?, long>.Converter) LogicalWrite.ConvertDateTimeMillis;
                    case TimeUnit.Micros:
                        return (LogicalWrite<DateTime?, long>.Converter) LogicalWrite.ConvertDateTimeMicros;
                }
            }

            if (typeof(TLogical) == typeof(DateTimeNanos?))
            {
                return LogicalWrite.GetNullableNativeConverter<DateTimeNanos, long>();
            }

            if (typeof(TLogical) == typeof(TimeSpan))
            {
                switch (((TimeLogicalType) logicalType).TimeUnit)
                {
                    case TimeUnit.Millis:
                        return (LogicalWrite<TimeSpan, int>.Converter) ((s, _, d, _) => LogicalWrite.ConvertTimeSpanMillis(s, d));
                    case TimeUnit.Micros:
                        return (LogicalWrite<TimeSpan, long>.Converter) ((s, _, d, _) => LogicalWrite.ConvertTimeSpanMicros(s, d));
                }
            }

            if (typeof(TLogical) == typeof(TimeSpanNanos))
            {
                return LogicalWrite.GetNativeConverter<TimeSpanNanos, long>();
            }

            if (typeof(TLogical) == typeof(TimeSpan?))
            {
                switch (((TimeLogicalType) logicalType).TimeUnit)
                {
                    case TimeUnit.Millis:
                        return (LogicalWrite<TimeSpan?, int>.Converter) LogicalWrite.ConvertTimeSpanMillis;
                    case TimeUnit.Micros:
                        return (LogicalWrite<TimeSpan?, long>.Converter) LogicalWrite.ConvertTimeSpanMicros;
                }
            }

            if (typeof(TLogical) == typeof(TimeSpanNanos?))
            {
                return LogicalWrite.GetNullableNativeConverter<TimeSpanNanos, long>();
            }

#if NET6_0_OR_GREATER
            if (typeof(TLogical) == typeof(DateOnly))
            {
                return (LogicalWrite<DateOnly, int>.Converter) ((s, _, d, _) => LogicalWrite.ConvertDateOnly(s, d));
            }

            if (typeof(TLogical) == typeof(DateOnly?))
            {
                return (LogicalWrite<DateOnly?, int>.Converter) LogicalWrite.ConvertDateOnly;
            }

            if (typeof(TLogical) == typeof(TimeOnly))
            {
                switch (((TimeLogicalType) logicalType).TimeUnit)
                {
                    case TimeUnit.Millis:
                        return (LogicalWrite<TimeOnly, int>.Converter) ((s, _, d, _) => LogicalWrite.ConvertTimeOnlyMillis(s, d));
                    case TimeUnit.Micros:
                        return (LogicalWrite<TimeOnly, long>.Converter) ((s, _, d, _) => LogicalWrite.ConvertTimeOnlyMicros(s, d));
                }
            }

            if (typeof(TLogical) == typeof(TimeOnly?))
            {
                switch (((TimeLogicalType) logicalType).TimeUnit)
                {
                    case TimeUnit.Millis:
                        return (LogicalWrite<TimeOnly?, int>.Converter) LogicalWrite.ConvertTimeOnlyMillis;
                    case TimeUnit.Micros:
                        return (LogicalWrite<TimeOnly?, long>.Converter) LogicalWrite.ConvertTimeOnlyMicros;
                }
            }
#endif

            if (typeof(TLogical) == typeof(string))
            {
                if (byteBuffer == null) throw new ArgumentNullException(nameof(byteBuffer));
                return (LogicalWrite<string, ByteArray>.Converter) ((s, dl, d, nl) => LogicalWrite.ConvertString(s, dl, d, nl, byteBuffer));
            }

            if (typeof(TLogical) == typeof(byte[]))
            {
                if (byteBuffer == null) throw new ArgumentNullException(nameof(byteBuffer));
                return (LogicalWrite<byte[], ByteArray>.Converter) ((s, dl, d, nl) => LogicalWrite.ConvertByteArray(s, dl, d, nl, byteBuffer));
            }

#if NET5_0_OR_GREATER
            if (typeof(TLogical) == typeof(Half))
            {
                if (byteBuffer == null) throw new ArgumentNullException(nameof(byteBuffer));
                return (LogicalWrite<Half, FixedLenByteArray>.Converter) ((s, _, d, _) => LogicalWrite.ConvertHalf(s, d, byteBuffer));
            }

            if (typeof(TLogical) == typeof(Half?))
            {
                if (byteBuffer == null) throw new ArgumentNullException(nameof(byteBuffer));
                return (LogicalWrite<Half?, FixedLenByteArray>.Converter) ((s, dl, d, nl) => LogicalWrite.ConvertHalf(s, dl, d, nl, byteBuffer));
            }
#endif

            throw new NotSupportedException($"unsupported logical system type {typeof(TLogical)} with logical type {logicalType}");
        }

        private static unsafe void ValidateDecimalColumn(ColumnDescriptor columnDescriptor)
        {
            // For the moment we only support serializing decimal to Decimal128.
            // This reflects the C# decimal structure with 28-29 digits precision.
            // Will implement 32-bits, 64-bits and other precision later.
            if (typeof(TPhysical) != typeof(FixedLenByteArray))
            {
                throw new NotSupportedException("Writing decimal data is only supported with a fixed-length byte array physical type");
            }
            if (columnDescriptor.TypePrecision != 29)
            {
                throw new NotSupportedException("only 29 digits of precision is currently supported for decimal type");
            }
            if (columnDescriptor.TypeLength != sizeof(Decimal128))
            {
                throw new NotSupportedException("only 16 bytes of length is currently supported for decimal type ");
            }
        }
    }

    /// <summary>
    /// C# types to Parquet physical types write conversion logic.
    /// Separate class for per-element conversion logic.
    /// </summary>
    public static class LogicalWrite
    {
        public static Delegate GetNativeConverter<TTLogical, TTPhysical>()
            where TTLogical : unmanaged
            where TTPhysical : unmanaged
        {
            return (LogicalWrite<TTLogical, TTPhysical>.Converter) ((s, _, d, _) => ConvertNative(s, MemoryMarshal.Cast<TTPhysical, TTLogical>(d)));
        }

        public static Delegate GetNullableNativeConverter<TTLogical, TTPhysical>()
            where TTLogical : unmanaged
            where TTPhysical : unmanaged
        {
            return (LogicalWrite<TTLogical?, TTPhysical>.Converter) ((s, dl, d, nl) => ConvertNative(s, dl, MemoryMarshal.Cast<TTPhysical, TTLogical>(d), nl));
        }

        public static void ConvertNative<TValue>(ReadOnlySpan<TValue> source, Span<TValue> destination) where TValue : unmanaged
        {
            source.CopyTo(destination);
        }

        public static void ConvertNative<TValue>(ReadOnlySpan<TValue?> source, Span<short> defLevels, Span<TValue> destination, short nullLevel) where TValue : struct
        {
            for (int i = 0, dst = 0; i < source.Length; ++i)
            {
                var value = source[i];
                if (value == null)
                {
                    defLevels[i] = nullLevel;
                }
                else
                {
                    destination[dst++] = value.Value;
                    defLevels[i] = (short) (nullLevel + 1);
                }
            }
        }

        public static void ConvertInt8(ReadOnlySpan<sbyte> source, Span<int> destination)
        {
            for (int i = 0; i < source.Length; ++i)
            {
                destination[i] = source[i];
            }
        }

        public static void ConvertInt8(ReadOnlySpan<sbyte?> source, Span<short> defLevels, Span<int> destination, short nullLevel)
        {
            for (int i = 0, dst = 0; i < source.Length; ++i)
            {
                var value = source[i];
                if (value == null)
                {
                    defLevels[i] = nullLevel;
                }
                else
                {
                    destination[dst++] = value.Value;
                    defLevels[i] = (short) (nullLevel + 1);
                }
            }
        }

        public static void ConvertUInt8(ReadOnlySpan<byte> source, Span<int> destination)
        {
            for (int i = 0; i < source.Length; ++i)
            {
                destination[i] = source[i];
            }
        }

        public static void ConvertUInt8(ReadOnlySpan<byte?> source, Span<short> defLevels, Span<int> destination, short nullLevel)
        {
            for (int i = 0, dst = 0; i < source.Length; ++i)
            {
                var value = source[i];
                if (value == null)
                {
                    defLevels[i] = nullLevel;
                }
                else
                {
                    destination[dst++] = value.Value;
                    defLevels[i] = (short) (nullLevel + 1);
                }
            }
        }

        public static void ConvertInt16(ReadOnlySpan<short> source, Span<int> destination)
        {
            for (int i = 0; i < source.Length; ++i)
            {
                destination[i] = source[i];
            }
        }

        public static void ConvertInt16(ReadOnlySpan<short?> source, Span<short> defLevels, Span<int> destination, short nullLevel)
        {
            for (int i = 0, dst = 0; i < source.Length; ++i)
            {
                var value = source[i];
                if (value == null)
                {
                    defLevels[i] = nullLevel;
                }
                else
                {
                    destination[dst++] = value.Value;
                    defLevels[i] = (short) (nullLevel + 1);
                }
            }
        }

        public static void ConvertUInt16(ReadOnlySpan<ushort> source, Span<int> destination)
        {
            for (int i = 0; i < source.Length; ++i)
            {
                destination[i] = source[i];
            }
        }

        public static void ConvertUInt16(ReadOnlySpan<ushort?> source, Span<short> defLevels, Span<int> destination, short nullLevel)
        {
            for (int i = 0, dst = 0; i < source.Length; ++i)
            {
                var value = source[i];
                if (value == null)
                {
                    defLevels[i] = nullLevel;
                }
                else
                {
                    destination[dst++] = value.Value;
                    defLevels[i] = (short) (nullLevel + 1);
                }
            }
        }

        public static void ConvertDecimal128(ReadOnlySpan<decimal> source, Span<FixedLenByteArray> destination, decimal multiplier, ByteBuffer byteBuffer)
        {
            for (int i = 0; i < source.Length; ++i)
            {
                destination[i] = FromDecimal(source[i], multiplier, byteBuffer);
            }
        }

        public static void ConvertDecimal128(ReadOnlySpan<decimal?> source, Span<short> defLevels, Span<FixedLenByteArray> destination, decimal multiplier, short nullLevel, ByteBuffer byteBuffer)
        {
            for (int i = 0, dst = 0; i < source.Length; ++i)
            {
                var value = source[i];
                if (value == null)
                {
                    defLevels[i] = nullLevel;
                }
                else
                {
                    destination[dst++] = LogicalWrite.FromDecimal(value.Value, multiplier, byteBuffer);
                    defLevels[i] = (short) (nullLevel + 1);
                }
            }
        }

        public static void ConvertUuid(ReadOnlySpan<Guid> source, Span<FixedLenByteArray> destination, ByteBuffer byteBuffer)
        {
            for (int i = 0; i < source.Length; ++i)
            {
                destination[i] = FromUuid(source[i], byteBuffer);
            }
        }

        public static void ConvertUuid(ReadOnlySpan<Guid?> source, Span<short> defLevels, Span<FixedLenByteArray> destination, short nullLevel, ByteBuffer byteBuffer)
        {
            for (int i = 0, dst = 0; i < source.Length; ++i)
            {
                var value = source[i];
                if (value == null)
                {
                    defLevels[i] = nullLevel;
                }
                else
                {
                    destination[dst++] = FromUuid(value.Value, byteBuffer);
                    defLevels[i] = (short) (nullLevel + 1);
                }
            }
        }

#if NET5_0_OR_GREATER
        public static void ConvertHalf(ReadOnlySpan<Half> source, Span<FixedLenByteArray> destination, ByteBuffer byteBuffer)
        {
            for (int i = 0; i < source.Length; ++i)
            {
                destination[i] = FromHalf(in source[i], byteBuffer);
            }
        }

        public static void ConvertHalf(ReadOnlySpan<Half?> source, Span<short> defLevels, Span<FixedLenByteArray> destination, short nullLevel, ByteBuffer byteBuffer)
        {
            for (int i = 0, dst = 0; i < source.Length; ++i)
            {
                var value = source[i];
                if (value == null)
                {
                    defLevels[i] = nullLevel;
                }
                else
                {
                    destination[dst++] = FromHalf(value.Value, byteBuffer);
                    defLevels[i] = (short) (nullLevel + 1);
                }
            }
        }
#endif

        public static void ConvertDateTimeMicros(ReadOnlySpan<DateTime> source, Span<long> destination)
        {
            for (int i = 0; i < source.Length; ++i)
            {
                destination[i] = FromDateTimeMicros(source[i]);
            }
        }

        public static void ConvertDateTimeMicros(ReadOnlySpan<DateTime?> source, Span<short> defLevels, Span<long> destination, short nullLevel)
        {
            for (int i = 0, dst = 0; i < source.Length; ++i)
            {
                var value = source[i];
                if (value == null)
                {
                    defLevels[i] = nullLevel;
                }
                else
                {
                    destination[dst++] = FromDateTimeMicros(value.Value);
                    defLevels[i] = (short) (nullLevel + 1);
                }
            }
        }

        public static void ConvertDateTimeMillis(ReadOnlySpan<DateTime> source, Span<long> destination)
        {
            for (int i = 0; i < source.Length; ++i)
            {
                destination[i] = FromDateTimeMillis(source[i]);
            }
        }

        public static void ConvertDateTimeMillis(ReadOnlySpan<DateTime?> source, Span<short> defLevels, Span<long> destination, short nullLevel)
        {
            for (int i = 0, dst = 0; i < source.Length; ++i)
            {
                var value = source[i];
                if (value == null)
                {
                    defLevels[i] = nullLevel;
                }
                else
                {
                    destination[dst++] = FromDateTimeMillis(value.Value);
                    defLevels[i] = (short) (nullLevel + 1);
                }
            }
        }

        public static void ConvertTimeSpanMicros(ReadOnlySpan<TimeSpan> source, Span<long> destination)
        {
            for (int i = 0; i < source.Length; ++i)
            {
                destination[i] = FromTimeSpanMicros(source[i]);
            }
        }

        public static void ConvertTimeSpanMicros(ReadOnlySpan<TimeSpan?> source, Span<short> defLevels, Span<long> destination, short nullLevel)
        {
            for (int i = 0, dst = 0; i < source.Length; ++i)
            {
                var value = source[i];
                if (value == null)
                {
                    defLevels[i] = nullLevel;
                }
                else
                {
                    destination[dst++] = FromTimeSpanMicros(value.Value);
                    defLevels[i] = (short) (nullLevel + 1);
                }
            }
        }

        public static void ConvertTimeSpanMillis(ReadOnlySpan<TimeSpan> source, Span<int> destination)
        {
            for (int i = 0; i < source.Length; ++i)
            {
                destination[i] = FromTimeSpanMillis(source[i]);
            }
        }

        public static void ConvertTimeSpanMillis(ReadOnlySpan<TimeSpan?> source, Span<short> defLevels, Span<int> destination, short nullLevel)
        {
            for (int i = 0, dst = 0; i < source.Length; ++i)
            {
                var value = source[i];
                if (value == null)
                {
                    defLevels[i] = nullLevel;
                }
                else
                {
                    destination[dst++] = FromTimeSpanMillis(value.Value);
                    defLevels[i] = (short) (nullLevel + 1);
                }
            }
        }

#if NET6_0_OR_GREATER
        public static void ConvertDateOnly(ReadOnlySpan<DateOnly> source, Span<int> destination)
        {
            for (int i = 0; i < source.Length; ++i)
            {
                destination[i] = FromDateOnly(source[i]);
            }
        }

        public static void ConvertDateOnly(ReadOnlySpan<DateOnly?> source, Span<short> defLevels, Span<int> destination, short nullLevel)
        {
            for (int i = 0, dst = 0; i < source.Length; ++i)
            {
                var value = source[i];
                if (value == null)
                {
                    defLevels[i] = nullLevel;
                }
                else
                {
                    destination[dst++] = FromDateOnly(value.Value);
                    defLevels[i] = (short) (nullLevel + 1);
                }
            }
        }

        public static void ConvertTimeOnlyMicros(ReadOnlySpan<TimeOnly> source, Span<long> destination)
        {
            for (int i = 0; i < source.Length; ++i)
            {
                destination[i] = FromTimeOnlyMicros(source[i]);
            }
        }

        public static void ConvertTimeOnlyMicros(ReadOnlySpan<TimeOnly?> source, Span<short> defLevels, Span<long> destination, short nullLevel)
        {
            for (int i = 0, dst = 0; i < source.Length; ++i)
            {
                var value = source[i];
                if (value == null)
                {
                    defLevels[i] = nullLevel;
                }
                else
                {
                    destination[dst++] = FromTimeOnlyMicros(value.Value);
                    defLevels[i] = (short) (nullLevel + 1);
                }
            }
        }

        public static void ConvertTimeOnlyMillis(ReadOnlySpan<TimeOnly> source, Span<int> destination)
        {
            for (int i = 0; i < source.Length; ++i)
            {
                destination[i] = FromTimeOnlyMillis(source[i]);
            }
        }

        public static void ConvertTimeOnlyMillis(ReadOnlySpan<TimeOnly?> source, Span<short> defLevels, Span<int> destination, short nullLevel)
        {
            for (int i = 0, dst = 0; i < source.Length; ++i)
            {
                var value = source[i];
                if (value == null)
                {
                    defLevels[i] = nullLevel;
                }
                else
                {
                    destination[dst++] = FromTimeOnlyMillis(value.Value);
                    defLevels[i] = (short) (nullLevel + 1);
                }
            }
        }
#endif

        public static void ConvertString(ReadOnlySpan<string> source, Span<short> defLevels, Span<ByteArray> destination, short nullLevel, ByteBuffer byteBuffer)
        {
            for (int i = 0, dst = 0; i < source.Length; ++i)
            {
                var value = source[i];
                if (value == null)
                {
                    if (defLevels.IsEmpty)
                    {
                        throw new ArgumentException("encountered null value despite column schema node repetition being marked as required");
                    }

                    defLevels[i] = nullLevel;
                }
                else
                {
                    destination[dst++] = FromString(value, byteBuffer);
                    if (!defLevels.IsEmpty)
                    {
                        defLevels[i] = (short) (nullLevel + 1);
                    }
                }
            }
        }

        public static void ConvertByteArray(ReadOnlySpan<byte[]> source, Span<short> defLevels, Span<ByteArray> destination, short nullLevel, ByteBuffer byteBuffer)
        {
            for (int i = 0, dst = 0; i < source.Length; ++i)
            {
                var value = source[i];
                if (value == null)
                {
                    if (defLevels.IsEmpty)
                    {
                        throw new ArgumentException("encountered null value despite column schema node repetition being marked as required");
                    }

                    defLevels[i] = nullLevel;
                }
                else
                {
                    destination[dst++] = FromByteArray(value, byteBuffer);
                    if (!defLevels.IsEmpty)
                    {
                        defLevels[i] = (short) (nullLevel + 1);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FixedLenByteArray FromDecimal(decimal source, decimal multiplier, ByteBuffer byteBuffer)
        {
            var dec = new Decimal128(source, multiplier);
            return FromFixedLength(in dec, byteBuffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe FixedLenByteArray FromUuid(Guid uuid, ByteBuffer byteBuffer)
        {
            Debug.Assert(sizeof(Guid) == 16);

            // The creation of a temporary byte[] via ToByteArray() is a shame, but I can't find a better public interface into Guid.
            // Riskier but faster proposition is to assume that the layout of Guid is consistent. There is no such guarantees!
            // But hopefully any breaking change is going to be caught by our unit test.

            //var array = FromFixedLengthByteArray(uuid.ToByteArray(), byteBuffer); // SLOW
            var array = FromFixedLength(uuid, byteBuffer);
            var p = (byte*) array.Pointer;

            // From parquet-format logical type documentation
            // The value is encoded using big-endian, so that 00112233-4455-6677-8899-aabbccddeeff is encoded
            // as the bytes 00 11 22 33 44 55 66 77 88 99 aa bb cc dd ee ff.
            //
            // But Guid endianess is platform dependent (and ToByteArray() uses a little endian representation).
            if (BitConverter.IsLittleEndian)
            {
                // ReSharper disable once PossibleNullReferenceException
                Swap(ref p[0], ref p[3]);
                Swap(ref p[1], ref p[2]);
                Swap(ref p[4], ref p[5]);
                Swap(ref p[6], ref p[7]);
            }

            return array;
        }

#if NET5_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe FixedLenByteArray FromHalf(in Half value, ByteBuffer byteBuffer)
        {
            if (BitConverter.IsLittleEndian)
            {
                return FromFixedLength(value, byteBuffer);
            }
            else
            {
                // Float-16 values are always stored in little-endian order
                var array = FromFixedLength(value, byteBuffer);
                var p = (byte*) array.Pointer;
                Swap(ref p[0], ref p[1]);
                return array;
            }
        }
#endif

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long FromDateTimeMicros(DateTime source)
        {
            return (source.Ticks - DateTimeOffset) / (TimeSpan.TicksPerMillisecond / 1000);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long FromDateTimeMillis(DateTime source)
        {
            return (source.Ticks - DateTimeOffset) / TimeSpan.TicksPerMillisecond;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long FromTimeSpanMicros(TimeSpan source)
        {
            return source.Ticks / (TimeSpan.TicksPerMillisecond / 1000);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FromTimeSpanMillis(TimeSpan source)
        {
            return (int) (source.Ticks / TimeSpan.TicksPerMillisecond);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ByteArray FromString(string str, ByteBuffer byteBuffer)
        {
            var utf8 = System.Text.Encoding.UTF8;
            var byteCount = utf8.GetByteCount(str);
            var byteArray = byteBuffer.Allocate(byteCount);

            fixed (char* chars = str)
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                utf8.GetBytes(chars, str.Length, (byte*) byteArray.Pointer, byteCount);
            }

            return byteArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe ByteArray FromByteArray(byte[] array, ByteBuffer byteBuffer)
        {
            var byteArray = byteBuffer.Allocate(array.Length);

            fixed (byte* bytes = array)
            {
                Buffer.MemoryCopy(bytes, (byte*) byteArray.Pointer, byteArray.Length, byteArray.Length);
            }

            return byteArray;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe FixedLenByteArray FromFixedLength<TValue>(in TValue value, ByteBuffer byteBuffer)
            where TValue : unmanaged
        {
            var byteArray = byteBuffer.Allocate(sizeof(TValue));
            *(TValue*) byteArray.Pointer = value;

            return new FixedLenByteArray(byteArray.Pointer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Swap<T>(ref T lhs, ref T rhs)
        {
            var tmp = lhs;
            lhs = rhs;
            rhs = tmp;
        }

#if NET6_0_OR_GREATER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FromDateOnly(DateOnly source)
        {
            return source.DayNumber - BaseDateOnlyNumber;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long FromTimeOnlyMicros(TimeOnly source)
        {
            return source.Ticks / (TimeSpan.TicksPerMillisecond / 1000);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FromTimeOnlyMillis(TimeOnly source)
        {
            return (int) (source.Ticks / TimeSpan.TicksPerMillisecond);
        }

        internal static readonly int BaseDateOnlyNumber = new DateOnly(1970, 1, 1).DayNumber;
#endif

        public const long DateTimeOffset = 621355968000000000; // new DateTime(1970, 01, 01).Ticks
    }
}
