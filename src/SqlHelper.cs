namespace SsisUtils
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Globalization;
    using System.Text;
    using Microsoft.SqlServer.Management.Sdk.Sfc;

    internal class SqlHelper
    {
        public const int DefaultCommandTimeout = 30;

        public static SqlConnection GetSqlConnection(SqlStoreConnection storeConnection)
        {
            if (storeConnection == null || storeConnection.ServerConnection == null)
            {
                throw new ArgumentNullException("storeConnection");
            }

            SqlConnection sqlConnectionObject = storeConnection.ServerConnection.SqlConnectionObject;
            if (sqlConnectionObject.State == ConnectionState.Closed)
            {
                sqlConnectionObject.Open();
            }

            return sqlConnectionObject;
        }

        public static object ExecuteSQLCommand(SqlStoreConnection storeConnection, CommandType cmdType, string cmdText, SqlParameter[] parameters, ExecuteType execType)
        {
            return SqlHelper.ExecuteSQLCommand(storeConnection, cmdType, cmdText, parameters, execType, 30);
        }

        public static object ExecuteSQLCommand(SqlStoreConnection storeConnection, CommandType cmdType, string cmdText, SqlParameter[] parameters, ExecuteType execType, int commandTimeout)
        {
            SqlConnection sqlConnection = SqlHelper.GetSqlConnection(storeConnection);
            object result;
            using (SqlCommand sqlCommand = new SqlCommand())
            {
                sqlCommand.Connection = sqlConnection;
                sqlCommand.CommandType = cmdType;
                sqlCommand.CommandText = cmdText;
                sqlCommand.CommandTimeout = commandTimeout;
                if (parameters != null && parameters.Length > 0)
                {
                    sqlCommand.Parameters.AddRange(parameters);
                }

                switch (execType)
                {
                    case ExecuteType.ExecuteNonQuery:
                        result = sqlCommand.ExecuteNonQuery();
                        break;
                    case ExecuteType.ExecuteScalar:
                        result = sqlCommand.ExecuteScalar();
                        break;
                    case ExecuteType.ExecuteReader:
                        result = sqlCommand.ExecuteReader();
                        break;
                    default:
                        result = null;
                        break;
                }
            }

            return result;
        }

        public static string EscapeString(string s, char cEsc)
        {
            if (s == null)
            {
                return null;
            }

            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                stringBuilder.Append(c);
                if (cEsc == c)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString();
        }

        public static string EscapeString(string s)
        {
            return SqlHelper.EscapeString(s, '\'');
        }

        public static string MakeSqlString(string s)
        {
            return string.Format(CultureInfo.InvariantCulture, "N'{0}'", new object[]
            {
                SqlHelper.EscapeString(s, '\'')
            });
        }
    }
}
