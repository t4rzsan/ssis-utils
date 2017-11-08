# ssis-utils
A small library that adds useful extensions to the Microsoft.SqlServer.Management.IntegrationServices namespace.

Most importantly it allows you to execute a SSIS package from .NET code with a user specified timeout.  By default you can only execute packages with a timeout of 30 seconds if using Microsoft's own implementation of [PackageInfo.Execute](https://msdn.microsoft.com/en-us/library/hh245662.aspx?f=255&MSPPError=-2147217396).
