using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ExistExportToSQL
{
    internal abstract class ExistTable
    {
        public ExistTable(string jsonFile)
        {
            FileName = jsonFile;
        }

        public string ErrorMessage { get; protected set; } = string.Empty;

        public string FileName { get; protected set; }

        public bool HasError { get; protected set; }

        public string TableName => Path.GetFileNameWithoutExtension(FileName);

        public static string DropTableStatement(string tableName)
        {
            return $"DROP TABLE IF EXISTS {tableName}";
        }

        public static string LogMessageInScript(string message)
        {
            return $"RAISERROR('{message.Replace("'", "''", StringComparison.InvariantCulture)}', 0, 1) WITH NOWAIT";
        }

        public string BasicImportStart()
        {
            return @$"

{LogMessageInScript($"Importing {FileName}")}
SELECT @JSON = BulkColumn
FROM OPENROWSET(BULK '{FileName}', SINGLE_CLOB) AS j

{DropTableStatement(TableName)}
SELECT *
INTO {TableName}
FROM OPENJSON(@JSON)
";
        }

        public string DropThisTableStatement() => DropTableStatement(TableName);

        public void ErrorMsg(string message)
        {
            ErrorMessage = message;
            Console.Error.WriteLine($"Error from {TableName}: {message}");
        }

        public abstract string ImportScript();
    }
}