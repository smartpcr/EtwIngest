//-------------------------------------------------------------------------------
// <copyright file="KustoExtension.cs" company="Microsoft Corp.">
//     Copyright (c) Microsoft Corp. All rights reserved.
// </copyright>
//-------------------------------------------------------------------------------

namespace EtwIngest.Libs
{
    using System.Data.SqlTypes;
    using System.Text;
    using Kusto.Data.Common;

    public static class KustoExtension
    {
        public static string ToKustoColumnType(this Type type)
        {
            var typeToKustoColumnType = new Dictionary<Type, string>
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

            if (type.IsEnum)
            {
                return "string";
            }

            if (!type.IsScalar())
            {
                return "dynamic";
            }

            return typeToKustoColumnType.GetValueOrDefault(type, "string");
        }

        public static bool IsScalar(this Type type)
        {
            return type.IsPrimitive || type == typeof(string) || type.IsEnum || type == typeof(DateTime) || type == typeof(TimeSpan) || type == typeof(SqlDecimal);
        }

        public static bool IsTableExist(this ICslAdminProvider adminClient, string kustoTableName)
        {
            var showDatabasesCommand = ".show tables";
            using var result = adminClient.ExecuteControlCommand(showDatabasesCommand);
            while (result.Read())
            {
                if (result.GetString(0) == kustoTableName)
                {
                    return true;
                }
            }

            return false;
        }

        public static string GenerateCreateTableCommand(string tableName, List<(string fieldName, Type fieldType)> fields)
        {
            var createTableCmd = new StringBuilder($".create table ['{tableName}'] ({Environment.NewLine}");
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields.ElementAt(i);
                if (i < fields.Count - 1)
                {
                    createTableCmd.Append($"  {field.fieldName} : {field.fieldType.ToKustoColumnType()},{Environment.NewLine}");
                }
                else
                {
                    createTableCmd.Append($"  {field.fieldName} : {field.fieldType.ToKustoColumnType()}{Environment.NewLine}");
                }
            }
            createTableCmd.Append(")");
            return createTableCmd.ToString();
        }

        public static string GenerateCsvIngestionMapping(string tableName, string mappingName,
            List<(string fieldName, Type fieldType)> fields)
        {
            var csvMappingCmd = new StringBuilder(@$".create-or-alter table ['{tableName}'] ingestion csv mapping '{mappingName}' '[");
            for (var ordinal = 0; ordinal < fields.Count; ordinal++)
            {
                var eventField = fields.ElementAt(ordinal);
                csvMappingCmd.Append($"{{\"column\":\"{eventField.fieldName}\",\"datatype\":\"{eventField.fieldType.ToKustoColumnType()}\",\"Ordinal\":{ordinal}}}");
                if (ordinal < fields.Count - 1)
                {
                    csvMappingCmd.Append(",");
                }
            }
            csvMappingCmd.Append("]'");
            return csvMappingCmd.ToString();
        }
    }
}
