using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace ExistExportToSQL
{
    public class ScriptGenerator
    {
        public string CreateScript { get; private set; } = string.Empty;

        public string DropScript { get; private set; } = string.Empty;

        public string Folder { get; set; } = string.Empty;

        public void GenerateFromFolder()
        {
            var create = new StringBuilder(ScriptStart());
            var drop = new StringBuilder();
            var files = Directory.EnumerateFiles(Folder, "*.json");
            foreach (var file in files)
            {
                var table = FileToTableObject(file);
                AppendScriptsFromTableObject(table, create, drop);
            }

            create.Append(ScriptEnd());
            drop.Append(DropHelperTables());

            CreateScript = create.ToString();
            DropScript = drop.ToString();
        }

        internal static void AppendScriptsFromTableObject(ExistTable? existTable, StringBuilder create, StringBuilder drop)
        {
            if (existTable == null)
            {
                return;
            }

            if (existTable.HasError)
            {
                Console.WriteLine($"Note: Script for {existTable.TableName} was not generated due to an error: {existTable.ErrorMessage}");
                return;
            }

            Console.WriteLine($"Created script for {existTable.TableName}");

            create.AppendLine(existTable.ImportScript());
            drop.AppendLine(existTable.DropThisTableStatement());
        }

        internal static ExistTable? FileToTableObject(string file)
        {
            var name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
            ExistTable? existTable = null;

            // special handling for types that don't just have date and value
            if (Enums.IsComplexType(name))
            {
                switch (name)
                {
                case "averages":
                    existTable = new AveragesExistTable(file);
                    break;

                case "correlations":
                    existTable = new CorrelationsExistTable(file);
                    break;

                default:
                    Console.WriteLine($"Skipping complex type {name}");
                    break;
                }
            }
            else
            {
                existTable = new SimpleExistTable(file);
            }

            return existTable;
        }

        private static string DropHelperTables()
        {
            var sb = new StringBuilder();
            foreach (var t in new string[] { "location_geo", "sleep_end_ex", "sleep_start_ex" })
            {
                sb.AppendLine(ExistTable.DropTableStatement(t));
            }

            return sb.ToString();
        }

        private static string ScriptEnd()
        {
            return $@"
{ExistTable.LogMessageInScript("Creating helper tables")}
{ExistTable.LogMessageInScript("Helper table 'location_geo' enhances original table 'location' by storing coordinates in SQL Server's native GEOGRAPHY data type")}
DROP TABLE IF EXISTS location_geo
SELECT [date],[value], SUBSTRING(value,CHARINDEX(',',value)+1,255) AS lon,  SUBSTRING(value,0,CHARINDEX(',',value)) AS lat,
Cast('POINT('+ SUBSTRING(value,CHARINDEX(',',value)+1,255) + ' ' + SUBSTRING(value,0,CHARINDEX(',',value)) + ')'  AS GEOGRAPHY) AS geo,
Cast('POINT('+ SUBSTRING(value,CHARINDEX(',',value)+1,255) + ' ' + SUBSTRING(value,0,CHARINDEX(',',value)) + ')'  AS GEOGRAPHY).STAsText() AS geotext
INTO location_geo
FROM [location]

{ExistTable.LogMessageInScript("Helper table 'sleep_end_ex' enhances original table 'sleep_end' by normalizing the 'minutes from midnight as integer' data to TIME and DATETIME")}
SELECT [date],[value], DATEADD(MINUTE,value,CAST('' AS TIME)) AS TimeWake,  DATEADD(MINUTE,value,CAST(date AS DATETIME)) AS DateTimeWake
INTO sleep_end_ex
FROM sleep_end

{ExistTable.LogMessageInScript("Helper table 'sleep_start_ex' enhances original table 'sleep_start' by normalizing the 'minutes from midday as integer' data to TIME and DATETIME")}
SELECT [date],[value], DATEADD(MINUTE,value,CAST('12:00' AS TIME)) AS TimeSleep,  DATEADD(MINUTE,value+720,CAST(date AS DATETIME)) AS DateTimeSleep
INTO sleep_start_ex
FROM sleep_start
";
        }

        private static string ScriptStart()
        {
            return "DECLARE @JSON VARCHAR(MAX)\r\n";
        }
    }
}