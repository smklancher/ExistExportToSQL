namespace ExistExportToSQL;

internal record FileNameParts(string FileName, string FilePath, string TypeName, string TableName, string Year);

internal abstract class ExistTable
{
    public ExistTable(string jsonFile)
    {
        FullFilePath = jsonFile;
        Parts = PartsFromFile(jsonFile);
    }

    public ExistTable(FileNameParts parts)
    {
        Parts = parts;
        FullFilePath = parts.FilePath;
    }

    public string ErrorMessage { get; protected set; } = string.Empty;

    public string FullFilePath { get; protected set; }

    public bool HasError { get; protected set; }

    public bool IsCustomTag { get; protected set; } = false;

    public FileNameParts Parts { get; private set; }

    public virtual string TableName => Parts.TableName;

    public static string DropTableStatement(string tableName)
    {
        return $"DROP TABLE IF EXISTS {tableName}";
    }

    public static string LogMessageInScript(string message)
    {
        return $"RAISERROR('{message.Replace("'", "''", StringComparison.InvariantCulture)}', 0, 1) WITH NOWAIT";
    }

    public static FileNameParts PartsFromFile(string filepath)
    {
        var name = Path.GetFileNameWithoutExtension(filepath).ToLowerInvariant();

        var parts = name.Split('_');
        var typename = parts[0];
        var year = parts[parts.Length - 1];
        var nameparts = new ArraySegment<string>(parts, 1, parts.Length - 2).ToArray();
        var tablename = string.Join('_', nameparts);

        return new FileNameParts(name, filepath, typename, tablename, year);
    }

    public string BasicImportStart(bool dropTableFirst)
    {
        if (dropTableFirst)
        {
            return @$"

{LogMessageInScript($"Importing {FullFilePath}")}
SELECT @JSON = BulkColumn
FROM OPENROWSET(BULK '{FullFilePath}', SINGLE_CLOB) AS j

{DropTableStatement(TableName)}
SELECT *
INTO {TableName}
FROM OPENJSON(@JSON)
";
        }
        else
        {
            return @$"

{LogMessageInScript($"Importing {FullFilePath}")}
SELECT @JSON = BulkColumn
FROM OPENROWSET(BULK '{FullFilePath}', SINGLE_CLOB) AS j

INSERT INTO {TableName}
SELECT *
FROM OPENJSON(@JSON)
";
        }
    }

    public string DropThisTableStatement() => DropTableStatement(TableName);

    public void ErrorMsg(string message)
    {
        ErrorMessage = message;
        Console.Error.WriteLine($"Error from {TableName}: {message}");
    }

    public abstract string ImportScript(bool dropTableFirst);
}