using System.Text.Json;

namespace ExistExportToSQL
{
    internal class SimpleExistTable : ExistTable
    {
        public SimpleExistTable(string jsonFile) : base(jsonFile)
        {
            Load();
        }

        public string ValueSqlTypeName { get; private set; } = "NVARCHAR(MAX)";

        public override string ImportScript(bool dropTableFirst)
        {
            return BasicImportStart(dropTableFirst) + $"WITH(date DATE, value {ValueSqlTypeName})\r\n";
        }

        private void Load()
        {
            // document cannot be disposed while still using the JsonElements, so can't read/dispose in another function
            using var fs = File.OpenRead(FullFilePath);
            using JsonDocument document = JsonDocument.Parse(fs);

            var valuesIncludingNulls = Enumerable.Empty<JsonElement>();
            try
            {
                // ToList to read everything immediately and catch any exception
                valuesIncludingNulls = document.RootElement.EnumerateArray().Select(x => x.GetProperty("value")).ToList();
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Exception reading values from json: {ex.Message}";
                return;
            }

            // If any values have object or array, this is unexpected
            if (valuesIncludingNulls.Any(x => x.ValueKind == JsonValueKind.Array || x.ValueKind == JsonValueKind.Object))
            {
                HasError = true;
                ErrorMessage = "Json contains unexpected array/object values";
                return;
            }

            // If all values are null, then we don't know a data type and there is no point in importing it anyway
            if (valuesIncludingNulls.All(x => x.ValueKind == JsonValueKind.Null || x.ValueKind == JsonValueKind.Undefined))
            {
                HasError = true;
                ErrorMessage = "Data is all null";
                return;
            }

            // get only the non null values
            var values = valuesIncludingNulls.Where(x => x.ValueKind != JsonValueKind.Null && x.ValueKind != JsonValueKind.Undefined);

            // if everything is true or false then we can use bit type (have not observed this in exist)
            if (values.All(x => x.ValueKind == JsonValueKind.False || x.ValueKind == JsonValueKind.True))
            {
                ValueSqlTypeName = "BIT";
                return;
            }

            // if everything is a number
            if (values.All(x => x.ValueKind == JsonValueKind.Number))
            {
                // ... and everything parses as an integer
                if (values.All(x => int.TryParse(x.GetRawText(), out _)))
                {
                    // and everything is 0 or 1, then we will assume this is a custom tag
                    if (values.All(x => x.GetInt64() == 0 || x.GetInt64() == 1))
                    {
                        IsCustomTag = true;
                        ValueSqlTypeName = "BIT";
                        return;
                    }
                    else
                    {
                        // otherwise, int
                        ValueSqlTypeName = "INT";
                        return;
                    }
                }
                else
                {
                    // numbers that don't parse as integer will be float
                    ValueSqlTypeName = "FLOAT";
                    return;
                }
            }

            // anything left is a string
            ValueSqlTypeName = "NVARCHAR(MAX)";
            return;
        }
    }
}