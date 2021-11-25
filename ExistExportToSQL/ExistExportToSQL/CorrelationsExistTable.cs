namespace ExistExportToSQL;

internal class CorrelationsExistTable : ExistTable
{
    public CorrelationsExistTable(string jsonFile) : base(jsonFile)
    {
    }

    public override string TableName => $"{Parts.TypeName}_{base.TableName}";

    public override string ImportScript(bool dropTableFirst)
    {
        return BasicImportStart(dropTableFirst) + "WITH (date DATE, period INT, offset INT, attribute NVARCHAR(255), attribute2 NVARCHAR(255), " +
            "value FLOAT, p FLOAT, percentage FLOAT, stars INT, second_person NVARCHAR(255)," +
            "second_person_first NVARCHAR(255) '$.second_person_elements[0]', second_person_link NVARCHAR(255) '$.second_person_elements[1]', " +
            "second_person_second NVARCHAR(255) '$.second_person_elements[2]', attribute_category NVARCHAR(255), " +
            "strength_description NVARCHAR(255), stars_description NVARCHAR(255), description NVARCHAR(MAX), occurrence NVARCHAR(255), rating NVARCHAR(255))";
    }
}