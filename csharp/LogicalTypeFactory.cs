﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace ParquetSharp
{
    /// <summary>
    /// Defines the mapping between .NET types and Parquet logical types, allowing for custom type handling.
    /// </summary>
    public class LogicalTypeFactory
    {
        /// <summary>
        /// Create a new LogicalTypeFactory with the default primitive mapping.
        /// </summary>
        public LogicalTypeFactory()
            : this(DefaultPrimitiveMapping)
        {
        }

        /// <summary>
        /// Create a new LogicalTypeFactory with a custom primitive mapping.
        /// </summary>
        /// <param name="primitiveMapping">The mapping from .NET types to Parquet logical and physical types</param>
        public LogicalTypeFactory(IReadOnlyDictionary<Type, (LogicalType? logicalType, Repetition repetition, PhysicalType physicalType)> primitiveMapping)
        {
            _primitiveMapping = primitiveMapping;
        }

#if NET6_0_OR_GREATER
        /// <summary>
        /// Whether the Parquet Date logical type should be mapped to the .NET DateOnly type by default.
        /// If false (the default), then the ParquetSharp.Date type is used.
        /// </summary>
        public bool DateAsDateOnly { get; set; } = false;

        /// <summary>
        /// Whether the Parquet Time logical type should be mapped to the .NET TimeOnly type by default,
        /// for millisecond and microsecond precision.
        /// If false (the default), then the TimeSpan type is used.
        /// For nanosecond precision, ParquetSharp.TimeSpanNanos is always used.
        /// </summary>
        public bool TimeAsTimeOnly { get; set; } = false;
#endif

        /// <summary>
        /// Get the mapping from the C# types to the Parquet logical and physical types.
        /// </summary>
        public virtual bool TryGetParquetTypes(Type logicalSystemType, out (LogicalType? logicalType, Repetition repetition, PhysicalType physicalType) entry)
        {
            return _primitiveMapping.TryGetValue(logicalSystemType, out entry);
        }

        /// <summary>
        /// Get the mapping from a column descriptor to the actual C# physical and logical element types.
        /// If we know the exact column logical type, use that instead (i.e. user custom types).
        /// </summary>
        public virtual (Type physicalType, Type logicalType) GetSystemTypes(ColumnDescriptor descriptor, Type? columnLogicalTypeOverride)
        {
            var types = GetSystemTypes(descriptor);
            return (types.physicalType, columnLogicalTypeOverride ?? types.logicalType);
        }

        /// <summary>
        /// Query whether the given C# type is supported and a schema node can potentially be created.
        /// </summary>
        public virtual bool IsSupported(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            while (true)
            {
                if (_primitiveMapping.ContainsKey(type))
                {
                    return true;
                }

                if (type.IsArray)
                {
                    type = type.GetElementType()!;
                    continue;
                }

                return false;
            }
        }


        /// <summary>
        /// Get the mapping from a column descriptor to the actual C# physical and logical element types.
        /// </summary>
        public virtual unsafe (Type physicalType, Type logicalType) GetSystemTypes(ColumnDescriptor descriptor)
        {
            var physicalType = descriptor.PhysicalType;
            using var logicalType = descriptor.LogicalType;
            using var schemaNode = descriptor.SchemaNode;
            var repetition = schemaNode.Repetition;
            var nullable = repetition == Repetition.Optional;

            // Check for an exact match in the default primitive mapping.
            // Note that there may be multiple matches (eg. TimeSpan or TimeOnly for Time logical type)
            var matches = _primitiveMapping
                .Where(e =>
                    e.Value.physicalType == physicalType &&
                    e.Value.repetition == repetition &&
                    (e.Value.logicalType?.Equals(logicalType) ?? false))
                .ToArray();

            if (matches.Length == 1)
            {
                return (DefaultPhysicalTypeMapping[physicalType], matches[0].Key);
            }

            if (logicalType is NoneLogicalType or NullLogicalType)
            {
                if (!nullable && logicalType is NullLogicalType)
                {
                    throw new ArgumentOutOfRangeException(nameof(logicalType), "The null logical type may only be used with optional columns");
                }
                switch (physicalType)
                {
                    case PhysicalType.Int32:
                        return (typeof(int), nullable ? typeof(int?) : typeof(int));
                    case PhysicalType.Int64:
                        return (typeof(long), nullable ? typeof(long?) : typeof(long));
                    case PhysicalType.Int96:
                        return (typeof(Int96), nullable ? typeof(Int96?) : typeof(Int96));
                    case PhysicalType.Boolean:
                        return (typeof(bool), nullable ? typeof(bool?) : typeof(bool));
                    case PhysicalType.Float:
                        return (typeof(float), nullable ? typeof(float?) : typeof(float));
                    case PhysicalType.Double:
                        return (typeof(double), nullable ? typeof(double?) : typeof(double));
                }
            }

            if (logicalType is DecimalLogicalType)
            {
                switch (physicalType)
                {
                    case PhysicalType.Int32:
                    {
                        if (descriptor.TypePrecision > 9)
                        {
                            throw new NotSupportedException("A maximum of 9 digits of decimal precision is supported with int32 data");
                        }
                        return (typeof(int), nullable ? typeof(decimal?) : typeof(decimal));
                    }
                    case PhysicalType.Int64:
                    {
                        if (descriptor.TypePrecision > 18)
                        {
                            throw new NotSupportedException("A maximum of 18 digits of decimal precision is supported with int64 data");
                        }
                        return (typeof(long), nullable ? typeof(decimal?) : typeof(decimal));
                    }
                    case PhysicalType.FixedLenByteArray:
                    {
                        var maxPrecision = DecimalConverter.MaxPrecision(descriptor.TypeLength);
                        if (descriptor.TypePrecision > maxPrecision)
                        {
                            throw new NotSupportedException(
                                $"A maximum of {maxPrecision} digits of decimal precision is supported with fixed length byte arrays " +
                                $"of length {descriptor.TypeLength} (specified precision is {descriptor.TypePrecision})");
                        }
                        return (typeof(FixedLenByteArray), nullable ? typeof(decimal?) : typeof(decimal));
                    }
                }
            }

            if (logicalType is DateLogicalType)
            {
#if NET6_0_OR_GREATER
                if (DateAsDateOnly)
                {
                    return (typeof(int), nullable ? typeof(DateOnly?) : typeof(DateOnly));
                }
#endif
                return (typeof(int), nullable ? typeof(Date?) : typeof(Date));
            }

            if (logicalType is TimeLogicalType timeLogicalType)
            {
#if NET6_0_OR_GREATER
                if (TimeAsTimeOnly)
                {
                    switch (timeLogicalType.TimeUnit)
                    {
                        case TimeUnit.Millis:
                            return (typeof(int), nullable ? typeof(TimeOnly?) : typeof(TimeOnly));
                        case TimeUnit.Micros:
                            return (typeof(long), nullable ? typeof(TimeOnly?) : typeof(TimeOnly));
                    }
                }
#endif
                switch (timeLogicalType.TimeUnit)
                {
                    case TimeUnit.Millis:
                        return (typeof(int), nullable ? typeof(TimeSpan?) : typeof(TimeSpan));
                    case TimeUnit.Micros:
                        return (typeof(long), nullable ? typeof(TimeSpan?) : typeof(TimeSpan));
                    case TimeUnit.Nanos:
                        return (typeof(long), nullable ? typeof(TimeSpanNanos?) : typeof(TimeSpanNanos));
                }
            }

            if (logicalType is TimestampLogicalType timestampLogicalType)
            {
                switch (timestampLogicalType.TimeUnit)
                {
                    case TimeUnit.Millis:
                    case TimeUnit.Micros:
                        return (typeof(long), nullable ? typeof(DateTime?) : typeof(DateTime));
                    case TimeUnit.Nanos:
                        return (typeof(long), nullable ? typeof(DateTimeNanos?) : typeof(DateTimeNanos));
                }
            }

            if (logicalType.Type == LogicalTypeEnum.String)
            {
                return (typeof(ByteArray), typeof(string));
            }

            if (logicalType.Type == LogicalTypeEnum.Json)
            {
                return (typeof(ByteArray), typeof(string));
            }

            if (logicalType.Type == LogicalTypeEnum.Bson)
            {
                return (typeof(ByteArray), typeof(byte[]));
            }

            if (logicalType.Type == LogicalTypeEnum.None && physicalType == PhysicalType.ByteArray)
            {
                return (typeof(ByteArray), typeof(byte[]));
            }

            throw new ArgumentOutOfRangeException(nameof(logicalType), $"unsupported logical type {logicalType} with physical type {physicalType}");
        }

        /// <summary>
        /// Get a new pair of (LogicalType, PhysicalType) taking into account an optional logical type override.
        /// </summary>
        public virtual (LogicalType logicalType, PhysicalType physicalType) GetTypesOverride(
            LogicalType? logicalTypeOverride, LogicalType? logicalType, PhysicalType physicalType)
        {
            // By default, return the first listed logical type.
            if (logicalTypeOverride is null or NoneLogicalType)
            {
                if (logicalType == null) throw new ArgumentNullException(nameof(logicalType), $"both {nameof(logicalType)} and {nameof(logicalTypeOverride)} are null");
                return (logicalType, physicalType);
            }

            // Milliseconds TimeSpan can be stored on Int32
            if (logicalTypeOverride is TimeLogicalType {TimeUnit: TimeUnit.Millis})
            {
                physicalType = PhysicalType.Int32;
            }

            // Otherwise allow one of the supported override.
            return (logicalTypeOverride, physicalType);
        }

        /// <summary>
        /// List of default mapping for each supported C# type.
        /// </summary>
        public static readonly IReadOnlyDictionary<Type, (LogicalType? logicalType, Repetition repetition, PhysicalType physicalType)>
            DefaultPrimitiveMapping = new Dictionary<Type, (LogicalType?, Repetition, PhysicalType)>
            {
                {typeof(bool), (LogicalType.None(), Repetition.Required, PhysicalType.Boolean)},
                {typeof(bool?), (LogicalType.None(), Repetition.Optional, PhysicalType.Boolean)},
                {typeof(sbyte), (LogicalType.Int(8, isSigned: true), Repetition.Required, PhysicalType.Int32)},
                {typeof(sbyte?), (LogicalType.Int(8, isSigned: true), Repetition.Optional, PhysicalType.Int32)},
                {typeof(byte), (LogicalType.Int(8, isSigned: false), Repetition.Required, PhysicalType.Int32)},
                {typeof(byte?), (LogicalType.Int(8, isSigned: false), Repetition.Optional, PhysicalType.Int32)},
                {typeof(short), (LogicalType.Int(16, isSigned: true), Repetition.Required, PhysicalType.Int32)},
                {typeof(short?), (LogicalType.Int(16, isSigned: true), Repetition.Optional, PhysicalType.Int32)},
                {typeof(ushort), (LogicalType.Int(16, isSigned: false), Repetition.Required, PhysicalType.Int32)},
                {typeof(ushort?), (LogicalType.Int(16, isSigned: false), Repetition.Optional, PhysicalType.Int32)},
                {typeof(int), (LogicalType.Int(32, isSigned: true), Repetition.Required, PhysicalType.Int32)},
                {typeof(int?), (LogicalType.Int(32, isSigned: true), Repetition.Optional, PhysicalType.Int32)},
                {typeof(uint), (LogicalType.Int(32, isSigned: false), Repetition.Required, PhysicalType.Int32)},
                {typeof(uint?), (LogicalType.Int(32, isSigned: false), Repetition.Optional, PhysicalType.Int32)},
                {typeof(long), (LogicalType.Int(64, isSigned: true), Repetition.Required, PhysicalType.Int64)},
                {typeof(long?), (LogicalType.Int(64, isSigned: true), Repetition.Optional, PhysicalType.Int64)},
                {typeof(ulong), (LogicalType.Int(64, isSigned: false), Repetition.Required, PhysicalType.Int64)},
                {typeof(ulong?), (LogicalType.Int(64, isSigned: false), Repetition.Optional, PhysicalType.Int64)},
                {typeof(Int96), (LogicalType.None(), Repetition.Required, PhysicalType.Int96)},
                {typeof(Int96?), (LogicalType.None(), Repetition.Optional, PhysicalType.Int96)},
#if NET5_0_OR_GREATER
                {typeof(Half), (LogicalType.Float16(), Repetition.Required, PhysicalType.FixedLenByteArray)},
                {typeof(Half?), (LogicalType.Float16(), Repetition.Optional, PhysicalType.FixedLenByteArray)},
#endif
                {typeof(float), (LogicalType.None(), Repetition.Required, PhysicalType.Float)},
                {typeof(float?), (LogicalType.None(), Repetition.Optional, PhysicalType.Float)},
                {typeof(double), (LogicalType.None(), Repetition.Required, PhysicalType.Double)},
                {typeof(double?), (LogicalType.None(), Repetition.Optional, PhysicalType.Double)},
                {typeof(decimal), (null, Repetition.Required, PhysicalType.FixedLenByteArray)},
                {typeof(decimal?), (null, Repetition.Optional, PhysicalType.FixedLenByteArray)},
                {typeof(Guid), (LogicalType.Uuid(), Repetition.Required, PhysicalType.FixedLenByteArray)},
                {typeof(Guid?), (LogicalType.Uuid(), Repetition.Optional, PhysicalType.FixedLenByteArray)},
                {typeof(Date), (LogicalType.Date(), Repetition.Required, PhysicalType.Int32)},
                {typeof(Date?), (LogicalType.Date(), Repetition.Optional, PhysicalType.Int32)},
                {typeof(DateTime), (LogicalType.Timestamp(isAdjustedToUtc: true, timeUnit: TimeUnit.Micros), Repetition.Required, PhysicalType.Int64)},
                {typeof(DateTime?), (LogicalType.Timestamp(isAdjustedToUtc: true, timeUnit: TimeUnit.Micros), Repetition.Optional, PhysicalType.Int64)},
                {typeof(DateTimeNanos), (LogicalType.Timestamp(isAdjustedToUtc: true, timeUnit: TimeUnit.Nanos), Repetition.Required, PhysicalType.Int64)},
                {typeof(DateTimeNanos?), (LogicalType.Timestamp(isAdjustedToUtc: true, timeUnit: TimeUnit.Nanos), Repetition.Optional, PhysicalType.Int64)},
                {typeof(TimeSpan), (LogicalType.Time(isAdjustedToUtc: true, timeUnit: TimeUnit.Micros), Repetition.Required, PhysicalType.Int64)},
                {typeof(TimeSpan?), (LogicalType.Time(isAdjustedToUtc: true, timeUnit: TimeUnit.Micros), Repetition.Optional, PhysicalType.Int64)},
                {typeof(TimeSpanNanos), (LogicalType.Time(isAdjustedToUtc: true, timeUnit: TimeUnit.Nanos), Repetition.Required, PhysicalType.Int64)},
                {typeof(TimeSpanNanos?), (LogicalType.Time(isAdjustedToUtc: true, timeUnit: TimeUnit.Nanos), Repetition.Optional, PhysicalType.Int64)},
#if NET6_0_OR_GREATER
                {typeof(TimeOnly), (LogicalType.Time(isAdjustedToUtc: true, timeUnit: TimeUnit.Micros), Repetition.Required, PhysicalType.Int64)},
                {typeof(TimeOnly?), (LogicalType.Time(isAdjustedToUtc: true, timeUnit: TimeUnit.Micros), Repetition.Optional, PhysicalType.Int64)},
                {typeof(DateOnly), (LogicalType.Date(), Repetition.Required, PhysicalType.Int32)},
                {typeof(DateOnly?), (LogicalType.Date(), Repetition.Optional, PhysicalType.Int32)},
#endif
                {typeof(string), (LogicalType.String(), Repetition.Optional, PhysicalType.ByteArray)},
                {typeof(byte[]), (LogicalType.None(), Repetition.Optional, PhysicalType.ByteArray)}
            };

        public static readonly IReadOnlyDictionary<PhysicalType, Type>
            DefaultPhysicalTypeMapping = new Dictionary<PhysicalType, Type>
            {
                {PhysicalType.Boolean, typeof(bool)},
                {PhysicalType.Int32, typeof(int)},
                {PhysicalType.Int64, typeof(long)},
                {PhysicalType.Int96, typeof(Int96)},
                {PhysicalType.Float, typeof(float)},
                {PhysicalType.Double, typeof(double)},
                {PhysicalType.ByteArray, typeof(ByteArray)},
                {PhysicalType.FixedLenByteArray, typeof(FixedLenByteArray)},
            };

        /// <summary>
        /// The default LogicalTypeFactory instance.
        /// </summary>
        public static readonly LogicalTypeFactory Default = new();

        private readonly IReadOnlyDictionary<Type, (LogicalType? logicalType, Repetition repetition, PhysicalType physicalType)> _primitiveMapping;
    }
}
