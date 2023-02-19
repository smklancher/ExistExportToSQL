using Microsoft.VisualBasic;

namespace ExistExportToSQL;

internal abstract class ExistJsonFile
{
    public ExistJsonFile(string jsonFile)
    {
        FullFilePath = jsonFile;
        Parts = PartsFromFile(jsonFile);
    }

    public ExistJsonFile(FileNameParts parts)
    {
        Parts = parts;
        FullFilePath = parts.FilePath;
    }

    public bool AllZeros { get; protected set; } = false;

    public string ErrorMessage { get; protected set; } = string.Empty;

    public string FullFilePath { get; protected set; }

    public bool HasError { get; protected set; }

    public bool IsIntegerTrait { get; protected set; } = false;

    public FileNameParts Parts { get; private set; }

    public virtual string TableName => Parts.TableName;

    public virtual string TableNameInBrackets => $"[{TableName}]";

    public bool ValuesLookLikeBool { get; protected set; } = false;

    public static string DropTableStatement(string tableName)
    {
        return $"DROP TABLE IF EXISTS {InBrackets(tableName)}";
    }

    /// <summary>
    /// Take table name with or without brackets and return it with brackets
    /// </summary>
    /// <param name="tableName"></param>
    /// <returns></returns>
    public static string InBrackets(string tableName)
    {
        return "[" + tableName.Replace("[", string.Empty).Replace("]", string.Empty) + "]";
    }

    public static string LogMessageInScript(string message)
    {
        return $"RAISERROR('{message.Replace("'", "''", StringComparison.InvariantCulture)}', 0, 1) WITH NOWAIT";
    }

    public static FileNameParts PartsFromFile(string filepath) => new FileNameParts(filepath);

    public string BasicImportStart(bool dropTableFirst)
    {
        var logstmt = LogMessageInScript($"Importing {FullFilePath}");
        var dropstmt = dropTableFirst ? DropTableStatement(TableNameInBrackets) : string.Empty;

        var dropAndCreate =
            $"""
            {dropstmt}
            SELECT *
            INTO {TableNameInBrackets}
            FROM OPENJSON(@JSON)
            """;

        var justInsert =
            $"""
            INSERT INTO {TableNameInBrackets}
            SELECT * FROM OPENJSON(@JSON)
            """;

        var insertStmt = dropTableFirst ? dropAndCreate : justInsert;

        var sql =
            $"""

            {logstmt}
            SELECT @JSON = BulkColumn
            FROM OPENROWSET(BULK '{FullFilePath}', SINGLE_CLOB) AS j

            {insertStmt}
            """;

        return sql;
    }

    public string DropThisTableStatement() => DropTableStatement(TableNameInBrackets);

    public void ErrorMsg(string message)
    {
        ErrorMessage = message;
        Console.Error.WriteLine($"Error from {TableName}: {message}");
    }

    public abstract string ImportScript(bool dropTableFirst);
}