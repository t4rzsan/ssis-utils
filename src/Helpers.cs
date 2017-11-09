namespace SsisUtils
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Data.SqlTypes;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using Microsoft.SqlServer.Management.IntegrationServices;

    internal static class Helpers
    {
        internal static bool IsValidSid(string sid)
        {
            if (string.IsNullOrEmpty(sid))
            {
                return false;
            }

            char[] array = sid.ToCharArray();
            if (array.Length < 2)
            {
                return false;
            }

            if (array[0] != '0' || (array[1] != 'x' && array[1] != 'X'))
            {
                return false;
            }

            for (int i = 2; i < array.Length; i++)
            {
                if (!Helpers.IsHexDigit(array[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsHexDigit(char c)
        {
            return char.IsNumber(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        internal static string GetEscapedName(string name)
        {
            if (name == null)
            {
                return null;
            }

            return name.Replace("]", "]]");
        }

        internal static void CheckPropertyString(string value, string propertyName, bool isAName, int maxLength)
        {
            if (isAName)
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new IntegrationServicesException($"PropertyNullOrEmpty: {propertyName}");
                }

                char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
                if (value.IndexOfAny(invalidFileNameChars) >= 0)
                {
                    throw new IntegrationServicesException($"PropertyContainsInvalidCharsAsName: {propertyName}");
                }
            }

            if (value != null && value.Length > maxLength)
            {
                throw new IntegrationServicesException($"PropertyTooLong: {propertyName}, {maxLength}");
            }
        }

        internal static bool IsSysAdmin(IntegrationServices store)
        {
            string cmdText = "SELECT ISNULL(IS_SRVROLEMEMBER ('sysadmin'), 0)";
            object value = SqlHelper.ExecuteSQLCommand(store.Connection, CommandType.Text, cmdText, null, ExecuteType.ExecuteScalar);
            int num = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            return num == 1;
        }

        internal static string GetPlatform(IntegrationServices store)
        {
            if (store == null)
            {
                return null;
            }

            string cmdText = "EXEC xp_msver platform";
            object obj = SqlHelper.ExecuteSQLCommand(store.Connection, CommandType.Text, cmdText, null, ExecuteType.ExecuteReader);
            using (SqlDataReader sqlDataReader = (SqlDataReader)obj)
            {
                if (sqlDataReader.Read())
                {
                    return sqlDataReader.GetString(3);
                }
            }

            return null;
        }

        internal static void ExecuteStoredProcedure(IntegrationServices store, string schema, string sprocName, SqlParameter[] parameters, string databaseName)
        {
            try
            {
                SqlConnection sqlConnection = SqlHelper.GetSqlConnection(store.Connection);
                SqlCommand sqlCommand = sqlConnection.CreateCommand();
                sqlCommand.CommandType = CommandType.StoredProcedure;
                sqlCommand.CommandTimeout = 600000;
                sqlCommand.CommandText = string.Format(CultureInfo.InvariantCulture, "[{0}].[{1}].[{2}]", new object[]
                {
                    Helpers.GetEscapedName(databaseName),
                    string.IsNullOrEmpty(schema) ? "dbo" : schema,
                    sprocName
                });
                if (parameters != null && parameters.Length > 0)
                {
                    sqlCommand.Parameters.AddRange(parameters);
                }

                sqlCommand.ExecuteNonQuery();
            }
            catch (SqlException ex)
            {
                throw new IntegrationServicesException($"StoredProcedureException: {sprocName}, {ex.Message}");
            }
        }

        internal static object GetPrincipalIdByName(IntegrationServices store, string databaseName, string principalName)
        {
            SqlParameter[] parameters = new SqlParameter[]
            {
                new SqlParameter("@name", principalName)
            };
            string cmdText = string.Format(CultureInfo.InvariantCulture, "SELECT [principal_id] from [{0}].[internal].[get_database_principals]() where name = @name", new object[]
            {
                databaseName
            });
            return SqlHelper.ExecuteSQLCommand(store.Connection, CommandType.Text, cmdText, parameters, ExecuteType.ExecuteScalar);
        }

        internal static string TypeCodeToSqlTypeString(TypeCode code)
        {
            switch (code)
            {
                case TypeCode.Boolean:
                    return "bit";
                case TypeCode.SByte:
                    return "smallint";
                case TypeCode.Byte:
                    return "tinyint";
                case TypeCode.Int16:
                    return "smallint";
                case TypeCode.Int32:
                    return "int";
                case TypeCode.UInt32:
                    return "bigint";
                case TypeCode.Int64:
                    return "bigint";
                case TypeCode.UInt64:
                    return "bigint";
                case TypeCode.Single:
                    return "float";
                case TypeCode.Double:
                    return "float";
                case TypeCode.Decimal:
                    return "decimal(38,18)";
                case TypeCode.DateTime:
                    return "datetime";
                case TypeCode.String:
                    return "sql_variant";
            }

            throw new IntegrationServicesException($"UnsupportedType: {code.ToString()}");
        }

        internal static string FormatSqlVariant(object sqlVariant)
        {
            if (sqlVariant == null || sqlVariant is DBNull)
            {
                return "NULL";
            }

            Type type = sqlVariant.GetType();
            if (type == typeof(int))
            {
                return ((int)sqlVariant).ToString(CultureInfo.InvariantCulture);
            }

            if (type == typeof(byte))
            {
                return Helpers.ByteArrayToString(new byte[]
                {
                    (byte)sqlVariant
                });
            }

            if (type == typeof(decimal))
            {
                return ((decimal)sqlVariant).ToString(CultureInfo.InvariantCulture);
            }

            if (type == typeof(string))
            {
                return string.Format(CultureInfo.InvariantCulture, "N'{0}'", new object[]
                {
                    SqlHelper.EscapeString((string)sqlVariant)
                });
            }

            if (type == typeof(short))
            {
                return ((short)sqlVariant).ToString(CultureInfo.InvariantCulture);
            }

            if (type == typeof(long))
            {
                return ((long)sqlVariant).ToString(CultureInfo.InvariantCulture);
            }

            if (type == typeof(ulong))
            {
                object obj = Convert.ToInt64(sqlVariant, CultureInfo.InvariantCulture);
                return ((long)obj).ToString(CultureInfo.InvariantCulture);
            }

            if (type == typeof(double))
            {
                return ((double)sqlVariant).ToString("R", CultureInfo.InvariantCulture);
            }

            if (type == typeof(float))
            {
                return ((float)sqlVariant).ToString("R", CultureInfo.InvariantCulture);
            }

            if (type == typeof(DateTime))
            {
                return string.Format(CultureInfo.InvariantCulture, "N'{0}'", new object[]
                {
                    Helpers.SqlDateString((DateTime)sqlVariant, "yyyy-MM-ddTHH:mm:ss.fff")
                });
            }

            if (type == typeof(SqlDateTime))
            {
                if (((SqlDateTime)sqlVariant).IsNull)
                {
                    return "NULL";
                }

                return string.Format(CultureInfo.InvariantCulture, "N'{0}'", new object[]
                {
                    Helpers.SqlDateString(((SqlDateTime)sqlVariant).Value, "yyyy-MM-ddTHH:mm:ss.fff")
                });
            }
            else
            {
                if (type == typeof(DateTimeOffset))
                {
                    return string.Format(CultureInfo.InvariantCulture, "N'{0}'", new object[]
                    {
                        Helpers.SqlDateString((DateTimeOffset)sqlVariant, "yyyy-MM-ddTHH:mm:ss.fff")
                    });
                }

                if (type == typeof(byte[]))
                {
                    return Helpers.ByteArrayToString((byte[])sqlVariant);
                }

                if (type == typeof(SqlBinary))
                {
                    if (((SqlBinary)sqlVariant).IsNull)
                    {
                        return "NULL";
                    }

                    return Helpers.ByteArrayToString(((SqlBinary)sqlVariant).Value);
                }
                else
                {
                    if (type == typeof(bool))
                    {
                        return string.Format(CultureInfo.InvariantCulture, ((bool)sqlVariant) ? "1" : "0", new object[0]);
                    }

                    if (type == typeof(Guid))
                    {
                        return string.Format(CultureInfo.InvariantCulture, "{0}", new object[]
                        {
                            SqlHelper.MakeSqlString(sqlVariant.ToString())
                        });
                    }

                    return sqlVariant.ToString();
                }
            }
        }

        internal static string SqlDateString(DateTime date)
        {
            return Helpers.SqlDateString(date, "s");
        }

        internal static string SqlDateString(DateTime date, string format)
        {
            return date.ToString(format, CultureInfo.InvariantCulture);
        }

        internal static string SqlDateString(DateTimeOffset date, string format)
        {
            return date.ToString(format, CultureInfo.InvariantCulture);
        }

        private static string ByteArrayToString(byte[] bytes)
        {
            if (bytes.Length == 0)
            {
                return "NULL";
            }

            int num = bytes.Length;
            StringBuilder stringBuilder = new StringBuilder("0x", 2 * (num + 1));
            for (int i = 0; i < num; i++)
            {
                stringBuilder.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
            }

            return stringBuilder.ToString();
        }

        internal static bool IsTypeSupported(TypeCode type)
        {
            return !type.Equals(TypeCode.Char) && !type.Equals(TypeCode.DBNull) && !type.Equals(TypeCode.Empty) && !type.Equals(TypeCode.Object) && !type.Equals(TypeCode.UInt16);
        }

        internal static DateTimeOffset DateTime2Offset(DateTime time)
        {
            return new DateTimeOffset(time, TimeSpan.Zero);
        }
    }
}
