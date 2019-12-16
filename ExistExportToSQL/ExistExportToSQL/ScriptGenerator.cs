using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace ExistExportToSQL
{
    public class ScriptGenerator
    {
        public string Folder { get; set; } = @"C:\Temp\Exist";

        public string Script { get; private set; } = string.Empty;

        public void GenerateFromFolder()
        {
            var sb = new StringBuilder(ScriptStart());
            var files = Directory.EnumerateFiles(Folder, "*.json");
            foreach (var file in files)
            {
                sb.AppendLine(FileToScript(file));
            }

            Script = sb.ToString();
        }

        private static string FileToScript(string file)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!Enums.IsComplexType(name))
            {
                var simple = new SimpleAttribute(file);
                simple.Load();
                if (simple.HasError)
                {
                    LogMessageInScript($"Note: Script for {name} was not generated due to an error: {simple.ErrorMessage}");
                }
                else
                {
                    Console.WriteLine($"Created script for {name}");
                    return ScriptFromSimpleAttribute(simple);
                }
            }
            else
            {
                Console.WriteLine($"Skipping complex type {name}");
            }

            return string.Empty;
        }

        private static string LogMessageInScript(string message)
        {
            return $"RAISERROR('{message.Replace("'", "''", StringComparison.InvariantCulture)}', 0, 1) WITH NOWAIT";
        }

        private static string ScriptFromSimpleAttribute(SimpleAttribute sa)
        {
            var msg = LogMessageInScript($"Importing {sa.FileName}");
            return @$"

{msg}
SELECT @JSON = BulkColumn
FROM OPENROWSET(BULK '{sa.FileName}', SINGLE_CLOB) AS j

DROP TABLE IF EXISTS {sa.TableName}
SELECT *
INTO {sa.TableName}
FROM OPENJSON(@JSON)
WITH(date DATE, value {sa.ValueSqlTypeName})
";
        }

        private static string ScriptStart()
        {
            return "DECLARE @JSON VARCHAR(MAX)\r\n";
        }
    }
}