using System.Text;

namespace ExistExportToSQL;

public class ScriptGenerator
{
    public string CreateScript { get; private set; } = string.Empty;

    public string DropScript { get; private set; } = string.Empty;

    public string Folder { get; set; } = string.Empty;

    public List<string> TablesAddedToDropScript { get; } = new();

    public void GenerateFromFolder(string inputFolder, string outputFile)
    {
        var create = new StringBuilder(ScriptStart());
        var drop = new StringBuilder();
        var files = Directory.EnumerateFiles(inputFolder, "*.json");

        // ways other than OfType don't seem to work with nullability tracking yet:
        // https://github.com/dotnet/roslyn/issues/37468#issuecomment-515142288
        var jsonFiles = files.Select(x => FileToTableObject(x)).OfType<ExistJsonFile>();

        if (!jsonFiles.Any())
        {
            Console.WriteLine($"No json files to parse in input folder: {inputFolder}");
            return;
        }

        var tables = JsonFilesForTable.FromAllFiles(jsonFiles);

        foreach (var json in jsonFiles)
        {
            AppendScriptsFromTableObject(json, create, drop);
        }

        create.AppendLine(HelperTableCreateScripts());
        drop.AppendLine(DropHelperTables());

        AppendScriptsForViews(tables, create, drop);

        CreateScript = create.ToString().Replace("\r\n", "\n", StringComparison.InvariantCultureIgnoreCase);
        DropScript = drop.ToString().Replace("\r\n", "\n", StringComparison.InvariantCultureIgnoreCase);

        var dropFile = Path.Combine(new FileInfo(outputFile).Directory!.FullName, "DropExistTables.sql");

        File.WriteAllText(outputFile, CreateScript);
        File.WriteAllText(dropFile, DropScript);
    }

    internal static void AppendScriptsForViews(IEnumerable<JsonFilesForTable> tables, StringBuilder create, StringBuilder drop)
    {
        // All custom tags
        var selects = tables.Where(x => x.IsCustomTag).Select(x => $"SELECT '{x.TableName}' AS [name], [date], [value] FROM [{x.TableName}]");
        var query = string.Join("\r\nUNION ALL\r\n", selects) + "\r\nGO\r\n";
        create.AppendLine(CreateView("AllBoolTraits", $"--Union of all custom tags\r\n{query}"));
        drop.AppendLine("DROP VIEW IF EXISTS AllBoolTraits");
        Console.WriteLine($"View AllBoolTraits includes {selects.Count()} tables with boolean data assumed to be custom tags");

        // All custom tags
        selects = tables.Where(x => x.IsIntegerTrait).Select(x => $"SELECT '{x.TableName}' AS [name], [date], [value] FROM [{x.TableName}]");
        query = string.Join("\r\nUNION ALL\r\n", selects) + "\r\nGO\r\n";
        create.AppendLine(CreateView("AllIntTraits", $"--Union of all custom tags and int traits\r\n{query}"));
        drop.AppendLine("DROP VIEW IF EXISTS AllIntTraits");
        Console.WriteLine($"View AllIntTraits includes {selects.Count()} tables that are either integer traits or custom tags");

        // last occurrence of tag
        query = $@"SELECT name, MAX(date) LastOccurrence
FROM AllBoolTraits
WHERE value=1
GROUP BY name
--ORDER BY MAX(date) DESC";
        create.AppendLine(CreateView("LastTag", query));
        drop.AppendLine("DROP VIEW IF EXISTS LastTag");

        // TagUsePast60Days
        query = $@"-- rolling sum of occurrences of a tag
SELECT name, date, value, SUM(CAST(value AS INT)) OVER
  (PARTITION BY name ORDER BY date ASC ROWS 59 PRECEDING) --59 + current row
  AS TagCount
  FROM AllBoolTraits
--ORDER BY date DESC";
        create.AppendLine(CreateView("TagUsePast60Days", query));
        drop.AppendLine("DROP VIEW IF EXISTS TagUsePast60Days");
    }

    internal static string CreateView(string viewName, string query) => $@"GO
DROP VIEW IF EXISTS [{viewName}]
GO
CREATE VIEW [{viewName}]
AS
{query}
";

    internal static ExistJsonFile? FileToTableObject(string file)
    {
        // ignore .net json files
        if (file.Contains(".deps") || file.Contains(".runtimeconfig")) { return null; }

        ExistJsonFile? existTable = null;

        try
        {
            var parts = ExistJsonFile.PartsFromFile(file);

            // special handling for types that don't just have date and value
            if (Enums.IsComplexType(parts.TypeName))
            {
                switch (parts.TypeName)
                {
                case "averages":
                    existTable = new AveragesJson(file);
                    break;

                case "correlations":
                    existTable = new CorrelationsJson(file);
                    break;

                default:
                    Console.WriteLine($"Skipping complex type {parts.FileName}");
                    break;
                }
            }
            else
            {
                existTable = new DataJson(file);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not parse file as exist table ({file}): {ex}");
        }

        return existTable;
    }

    internal void AppendScriptsFromTableObject(ExistJsonFile jsonFile, StringBuilder create, StringBuilder drop)
    {
        if (jsonFile.HasError)
        {
            Console.WriteLine($"Note: Script for {jsonFile.Parts.FileName} was not generated due to an error: {jsonFile.ErrorMessage}");
            return;
        }

        Console.WriteLine($"Created script for {(jsonFile.ValuesLookLikeBool ? "custom tag " : string.Empty)}{jsonFile.Parts.FileName}");

        bool dropTable = false;
        if (!TablesAddedToDropScript.Contains(jsonFile.TableName))
        {
            // the first time a table name is seen, add the drop script
            dropTable = true;
            TablesAddedToDropScript.Add(jsonFile.TableName);
        }

        create.AppendLine(jsonFile.ImportScript(dropTable));

        drop.AppendLine(jsonFile.DropThisTableStatement());
    }

    private static string DropHelperTables()
    {
        var sb = new StringBuilder();
        foreach (var t in new string[] { "location_geo", "sleep_end_ex", "sleep_start_ex" })
        {
            sb.AppendLine(ExistJsonFile.DropTableStatement(t));
        }

        return sb.ToString();
    }

    private static string HelperTableCreateScripts()
    {
        return $@"
{ExistJsonFile.LogMessageInScript("Creating helper tables")}
{ExistJsonFile.LogMessageInScript("Helper table 'location_geo' enhances original table 'location' by storing coordinates in SQL Server's native GEOGRAPHY data type")}
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'location')
BEGIN
    DROP TABLE IF EXISTS location_geo
    SELECT [date],[value], SUBSTRING(value,CHARINDEX(',',value)+1,255) AS lon,  SUBSTRING(value,0,CHARINDEX(',',value)) AS lat,
    Cast('POINT('+ SUBSTRING(value,CHARINDEX(',',value)+1,255) + ' ' + SUBSTRING(value,0,CHARINDEX(',',value)) + ')'  AS GEOGRAPHY) AS geo,
    Cast('POINT('+ SUBSTRING(value,CHARINDEX(',',value)+1,255) + ' ' + SUBSTRING(value,0,CHARINDEX(',',value)) + ')'  AS GEOGRAPHY).STAsText() AS geotext
    INTO location_geo
    FROM [location]
END

{ExistJsonFile.LogMessageInScript("Helper table 'sleep_end_ex' enhances original table 'sleep_end' by normalizing the 'minutes from midnight as integer' data to TIME and DATETIME")}
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'sleep_end')
BEGIN
    DROP TABLE IF EXISTS sleep_end_ex
    SELECT [date],[value], DATEADD(MINUTE,value,CAST('' AS TIME)) AS TimeWake,  DATEADD(MINUTE,value,CAST(date AS DATETIME)) AS DateTimeWake
    INTO sleep_end_ex
    FROM sleep_end
END

{ExistJsonFile.LogMessageInScript("Helper table 'sleep_start_ex' enhances original table 'sleep_start' by normalizing the 'minutes from midday as integer' data to TIME and DATETIME")}
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'sleep_start')
BEGIN
    DROP TABLE IF EXISTS sleep_start_ex
    SELECT [date],[value], DATEADD(MINUTE,value,CAST('12:00' AS TIME)) AS TimeSleep,  DATEADD(MINUTE,value+720,CAST(date AS DATETIME)) AS DateTimeSleep
    INTO sleep_start_ex
    FROM sleep_start
END
";
    }

    private static string ScriptStart()
    {
        return "DECLARE @JSON VARCHAR(MAX)\r\n";
    }
}