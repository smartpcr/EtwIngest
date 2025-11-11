//-------------------------------------------------------------------------------
// <copyright file="TypeMapper.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace Common.Diagnostics.EtwParser.Schema
{
    using System.Data.SqlTypes;

    /// <summary>
    /// Maps .NET CLR types to various target type systems
    /// </summary>
    public static class TypeMapper
    {
        private static readonly Dictionary<Type, string> KustoTypeMap = new()
        {
            { typeof(string), "string" },
            { typeof(bool), "bool" },
            { typeof(bool?), "bool" },
            { typeof(DateTime), "datetime" },
            { typeof(DateTime?), "datetime" },
            { typeof(DateTimeOffset), "datetime" },
            { typeof(DateTimeOffset?), "datetime" },
            { typeof(Guid), "guid" },
            { typeof(Guid?), "guid" },
            { typeof(int), "int" },
            { typeof(int?), "int" },
            { typeof(long), "long" },
            { typeof(long?), "long" },
            { typeof(decimal), "real" },
            { typeof(decimal?), "real" },
            { typeof(float), "real" },
            { typeof(float?), "real" },
            { typeof(double), "real" },
            { typeof(double?), "real" },
            { typeof(byte), "int" },
            { typeof(byte?), "int" },
            { typeof(short), "int" },
            { typeof(short?), "int" },
            { typeof(TimeSpan), "timespan" }
        };

        /// <summary>
        /// Maps a .NET type to Kusto type string
        /// </summary>
        public static string ToKustoType(Type type)
        {
            if (type.IsEnum)
            {
                return "string";
            }

            if (!IsScalar(type))
            {
                return "dynamic";
            }

            return KustoTypeMap.GetValueOrDefault(type, "string");
        }

        /// <summary>
        /// Maps a .NET type to SQL Server type string
        /// </summary>
        public static string ToSqlType(Type type)
        {
            if (type == typeof(string)) return "NVARCHAR(MAX)";
            if (type == typeof(bool) || type == typeof(bool?)) return "BIT";
            if (type == typeof(DateTime) || type == typeof(DateTime?) || type == typeof(DateTimeOffset) || type == typeof(DateTimeOffset?)) return "DATETIME2";
            if (type == typeof(Guid) || type == typeof(Guid?)) return "UNIQUEIDENTIFIER";
            if (type == typeof(int) || type == typeof(int?) || type == typeof(byte) || type == typeof(byte?) || type == typeof(short) || type == typeof(short?)) return "INT";
            if (type == typeof(long) || type == typeof(long?)) return "BIGINT";
            if (type == typeof(decimal) || type == typeof(decimal?)) return "DECIMAL(18,2)";
            if (type == typeof(float) || type == typeof(float?)) return "REAL";
            if (type == typeof(double) || type == typeof(double?)) return "FLOAT";
            if (type == typeof(TimeSpan)) return "TIME";
            if (type.IsEnum) return "NVARCHAR(100)";

            return "NVARCHAR(MAX)";
        }

        /// <summary>
        /// Checks if a type is scalar (primitive or simple value type)
        /// </summary>
        public static bool IsScalar(Type type)
        {
            return type.IsPrimitive
                || type == typeof(string)
                || type.IsEnum
                || type == typeof(DateTime)
                || type == typeof(TimeSpan)
                || type == typeof(SqlDecimal)
                || type == typeof(Guid);
        }
    }
}
