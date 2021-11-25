using System.Text;

namespace ExistExportToSQL
{
    public class ScriptGenerator
    {
        public string CreateScript { get; private set; } = string.Empty;

        public string DropScript { get; private set; } = string.Empty;

        public string Folder { get; set; } = string.Empty;

        public List<string> TablesAddedToDropScript { get; } = new();

        public void GenerateFromFolder()
        {
            var create = new StringBuilder(ScriptStart());
            var drop = new StringBuilder();
            var files = Directory.EnumerateFiles(Folder, "*.json");

            // ways other than OfType don't seem to work with nullability tracking yet:
            // https://github.com/dotnet/roslyn/issues/37468#issuecomment-515142288
            var tables = files.Select(x => FileToTableObject(x)).OfType<ExistTable>();

            foreach (var table in tables)
            {
                AppendScriptsFromTableObject(table, create, drop);
            }

            create.AppendLine(ScriptEnd());
            drop.AppendLine(DropHelperTables());

            AppendScriptsForViews(tables, create, drop);

            CreateScript = create.ToString().Replace("\r\n", "\n", StringComparison.InvariantCultureIgnoreCase);
            DropScript = drop.ToString().Replace("\r\n", "\n", StringComparison.InvariantCultureIgnoreCase);
        }

        internal static void AppendScriptsForViews(IEnumerable<ExistTable> tables, StringBuilder create, StringBuilder drop)
        {
            // All custom tags
            var selects = tables.DistinctBy(x => x.TableName).Where(x => x.IsCustomTag).Select(x => $"SELECT '{x.TableName}' AS [name], [date], [value] FROM [{x.TableName}]");
            var query = string.Join("\r\nUNION ALL\r\n", selects) + "\r\nGO\r\n";
            create.AppendLine(CreateView("AllCustomTags", $"--Union of all custom tags\r\n{query}"));
            drop.AppendLine("DROP VIEW IF EXISTS AllCustomTags");

            // last occurrence of tag
            query = $@"SELECT name, MAX(date) LastOccurrence
FROM AllCustomTags
WHERE value=1
GROUP BY name
--ORDER BY MAX(date) DESC";
            create.AppendLine(CreateView("LastTag", query));
            drop.AppendLine("DROP VIEW IF EXISTS LastTag");

            // TagUsePast60Days
            query = $@"-- rolling sum of occurrences of a tag
SELECT name, date, value, SUM(CAST(value AS INT)) OVER
  (PARTITION BY name ORDER BY date ASC ROWS 60 PRECEDING)
  AS TagCount
  FROM AllCustomTags
--ORDER BY date DESC";
            create.AppendLine(CreateView("TagUsePast60Days", query));
            drop.AppendLine("DROP VIEW IF EXISTS TagUsePast60Days");
        }

        internal static string CreateView(string viewName, string query) => $@"GO
DROP VIEW IF EXISTS {viewName}
GO
CREATE VIEW {viewName}
AS
{query}
";

        internal static ExistTable? FileToTableObject(string file)
        {
            // ignore .net json files
            if (file.Contains(".deps") || file.Contains(".runtimeconfig")) { return null; }

            ExistTable? existTable = null;

            try
            {
                var parts = ExistTable.PartsFromFile(file);

                // special handling for types that don't just have date and value
                if (Enums.IsComplexType(parts.TypeName))
                {
                    switch (parts.TypeName)
                    {
                    case "averages":
                        existTable = new AveragesExistTable(file);
                        break;

                    case "correlations":
                        existTable = new CorrelationsExistTable(file);
                        break;

                    default:
                        Console.WriteLine($"Skipping complex type {parts.FileName}");
                        break;
                    }
                }
                else
                {
                    existTable = new SimpleExistTable(file);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not parse file as exist table ({file}): {ex}");
            }

            return existTable;
        }

        internal void AppendScriptsFromTableObject(ExistTable existTable, StringBuilder create, StringBuilder drop)
        {
            if (existTable.HasError)
            {
                Console.WriteLine($"Note: Script for {existTable.Parts.FileName} was not generated due to an error: {existTable.ErrorMessage}");
                return;
            }

            Console.WriteLine($"Created script for {(existTable.IsCustomTag ? "custom tag " : string.Empty)}{existTable.Parts.FileName}");

            bool dropTable = false;
            if (!TablesAddedToDropScript.Contains(existTable.TableName))
            {
                // the first time a table name is seen, add the drop script
                dropTable = true;
                TablesAddedToDropScript.Add(existTable.TableName);
            }

            create.AppendLine(existTable.ImportScript(dropTable));

            drop.AppendLine(existTable.DropThisTableStatement());
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
DROP TABLE IF EXISTS sleep_end_ex
SELECT [date],[value], DATEADD(MINUTE,value,CAST('' AS TIME)) AS TimeWake,  DATEADD(MINUTE,value,CAST(date AS DATETIME)) AS DateTimeWake
INTO sleep_end_ex
FROM sleep_end

{ExistTable.LogMessageInScript("Helper table 'sleep_start_ex' enhances original table 'sleep_start' by normalizing the 'minutes from midday as integer' data to TIME and DATETIME")}
DROP TABLE IF EXISTS sleep_start_ex
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