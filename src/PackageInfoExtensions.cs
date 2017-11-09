namespace SsisUtils
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Data;
    using System.Data.SqlClient;
    using System.Data.SqlTypes;
    using System.Globalization;
    using System.Text;
    using Microsoft.SqlServer.Management.Common;
    using Microsoft.SqlServer.Management.IntegrationServices;
    using Microsoft.SqlServer.Management.Sdk.Sfc;

    public static class PackageInfoExtensions
    {
        public static long Execute(this PackageInfo @this, bool use32RuntimeOn64, EnvironmentReference reference, Collection<PackageInfo.ExecutionValueParameterSet> setValueParameters, int commandTimeout)
        {
            object obj = SqlHelper.ExecuteSQLCommand(((IntegrationServices)@this.GetDomain()).Connection, CommandType.Text, @this.ScriptCreateExecution(use32RuntimeOn64, reference), null, ExecuteType.ExecuteScalar, commandTimeout);
            if (setValueParameters != null && setValueParameters.Count > 0)
            {
                var param = new SqlParameter("execution_id", SqlDbType.Int);
                param.Value = obj;
                SqlParameter[] array = new SqlParameter[]
                {
                    param
                };

                SqlHelper.ExecuteSQLCommand(((IntegrationServices)@this.GetDomain()).Connection, CommandType.Text, @this.ScriptSetExecutionValues(setValueParameters), array, ExecuteType.ExecuteNonQuery, commandTimeout);
            }

            var param3 = new SqlParameter("execution_id", SqlDbType.Int);
            param3.Value = obj;
            SqlParameter[] array3 = new SqlParameter[]
            {
                param3
            };

            SqlHelper.ExecuteSQLCommand(((IntegrationServices)@this.GetDomain()).Connection, CommandType.Text, @this.ScriptStartExecution(), array3, ExecuteType.ExecuteNonQuery, commandTimeout);
            return (long)obj;
        }

        private static ISfcDomain GetDomain(this PackageInfo @this)
        {
            SfcInstance sfcInstance = @this;
            while (sfcInstance.Parent != null)
            {
                sfcInstance = sfcInstance.Parent;
            }

            ISfcDomain sfcDomain = sfcInstance as ISfcDomain;
            if (sfcDomain == null)
            {
                throw new SfcMissingParentException("Missing SFC parent.");
            }

            return sfcDomain;
        }

        private static string ScriptStartExecution(this PackageInfo @this)
        {
            return new SfcTsqlProcFormatter
            {
                Procedure = string.Format(CultureInfo.InvariantCulture, "[{0}].[catalog].[start_execution] @execution_id", new object[]
                {
            Helpers.GetEscapedName(@this.Parent.Parent.Parent.Name)
                })
            }.GenerateScript(@this, null);
        }

        private static string ScriptCreateExecution(this PackageInfo @this, bool use32RuntimeOn64, EnvironmentReference reference)
        {
            if (use32RuntimeOn64)
            {
                string platform = Helpers.GetPlatform(@this.GetDomain() as IntegrationServices);
                if (platform != null && !platform.Contains("64"))
                {
                    throw new InvalidArgumentException($"Use32RuntimeOn64_Not64: {use32RuntimeOn64}");
                }
            }

            SfcTsqlProcFormatter sfcTsqlProcFormatter = new SfcTsqlProcFormatter();
            sfcTsqlProcFormatter.Procedure = string.Format(CultureInfo.InvariantCulture, "[{0}].[catalog].[create_execution]", new object[]
            {
                Helpers.GetEscapedName(@this.Parent.Parent.Parent.Name)
            });

            sfcTsqlProcFormatter.Arguments.Add(new SfcTsqlProcFormatter.SprocArg("package_name", "Name", true, false));
            List<SfcTsqlProcFormatter.RuntimeArg> list = new List<SfcTsqlProcFormatter.RuntimeArg>();
            long num = 0L;
            SfcTsqlProcFormatter.RuntimeArg item = new SfcTsqlProcFormatter.RuntimeArg(typeof(long), num);
            list.Add(item);
            sfcTsqlProcFormatter.Arguments.Add(new SfcTsqlProcFormatter.SprocArg("execution_id", true, true));
            item = new SfcTsqlProcFormatter.RuntimeArg(typeof(string), @this.Parent.Parent.Name);
            list.Add(item);
            sfcTsqlProcFormatter.Arguments.Add(new SfcTsqlProcFormatter.SprocArg("folder_name", true));
            item = new SfcTsqlProcFormatter.RuntimeArg(typeof(string), @this.Parent.Name);
            list.Add(item);
            sfcTsqlProcFormatter.Arguments.Add(new SfcTsqlProcFormatter.SprocArg("project_name", true));
            item = new SfcTsqlProcFormatter.RuntimeArg(typeof(bool), use32RuntimeOn64);
            list.Add(item);
            sfcTsqlProcFormatter.Arguments.Add(new SfcTsqlProcFormatter.SprocArg("use32bitruntime", true));
            SqlInt64 sqlInt = (reference != null) ? reference.ReferenceId : SqlInt64.Null;
            item = new SfcTsqlProcFormatter.RuntimeArg(typeof(SqlInt64), sqlInt);
            list.Add(item);
            sfcTsqlProcFormatter.Arguments.Add(new SfcTsqlProcFormatter.SprocArg("reference_id", true));
            Version serverVersion = ((IntegrationServices)@this.GetDomain()).Connection.ServerVersion;
            if (serverVersion.Major >= 14)
            {
                if (serverVersion >= new Version(14, 0, 700))
                {
                    item = new SfcTsqlProcFormatter.RuntimeArg(typeof(bool), false);
                    list.Add(item);
                    sfcTsqlProcFormatter.Arguments.Add(new SfcTsqlProcFormatter.SprocArg("runinscaleout", true));
                }
                else
                {
                    item = new SfcTsqlProcFormatter.RuntimeArg(typeof(bool), false);
                    list.Add(item);
                    sfcTsqlProcFormatter.Arguments.Add(new SfcTsqlProcFormatter.SprocArg("runincluster", true));
                }
            }

            return sfcTsqlProcFormatter.GenerateScript(@this, list);
        }

        private static string ScriptSetExecutionValues(this PackageInfo @this, Collection<PackageInfo.ExecutionValueParameterSet> setValueParameters)
        {
            StringBuilder stringBuilder = new StringBuilder();
            if (setValueParameters != null && setValueParameters.Count >= 1)
            {
                int num = 0;
                foreach (PackageInfo.ExecutionValueParameterSet current in setValueParameters)
                {
                    stringBuilder.AppendLine(@this.ScriptSetParameterValue(current, num++));
                }
            }

            return stringBuilder.ToString();
        }

        private static string ScriptSetParameterValue(this PackageInfo @this, PackageInfo.ExecutionValueParameterSet parameterSet, int counter)
        {
            StringBuilder stringBuilder = new StringBuilder();
            Type type = parameterSet.ParameterValue.GetType();
            TypeCode typeCode = Type.GetTypeCode(type);
            string arg = Helpers.TypeCodeToSqlTypeString(typeCode);
            stringBuilder.AppendFormat("DECLARE @var{0} {1} = {2}\n", counter, arg, Helpers.FormatSqlVariant(parameterSet.ParameterValue));
            SfcTsqlProcFormatter sfcTsqlProcFormatter = new SfcTsqlProcFormatter();
            sfcTsqlProcFormatter.Procedure = string.Format(CultureInfo.InvariantCulture, "[{0}].[catalog].[set_execution_parameter_value] @execution_id, ", new object[]
            {
                @this.Parent.Parent.Parent.Name
            });

            List<SfcTsqlProcFormatter.RuntimeArg> list = new List<SfcTsqlProcFormatter.RuntimeArg>();
            SfcTsqlProcFormatter.RuntimeArg item = new SfcTsqlProcFormatter.RuntimeArg(typeof(short), parameterSet.ObjectType);
            list.Add(item);
            sfcTsqlProcFormatter.Arguments.Add(new SfcTsqlProcFormatter.SprocArg("object_type", true));
            item = new SfcTsqlProcFormatter.RuntimeArg(typeof(string), parameterSet.ParameterName);
            list.Add(item);
            sfcTsqlProcFormatter.Arguments.Add(new SfcTsqlProcFormatter.SprocArg("parameter_name", true));
            item = new SfcTsqlProcFormatter.RuntimeArg(typeof(object), "@var" + counter.ToString(CultureInfo.InvariantCulture));
            list.Add(item);
            sfcTsqlProcFormatter.Arguments.Add(new SfcTsqlProcFormatter.SprocArg("parameter_value", true));
            stringBuilder.Append(sfcTsqlProcFormatter.GenerateScript(@this, list));
            return stringBuilder.ToString();
        }
    }
}
