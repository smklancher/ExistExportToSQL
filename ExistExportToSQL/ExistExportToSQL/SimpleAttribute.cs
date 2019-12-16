using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ExistExportToSQL
{
    public class SimpleAttribute
    {
        public SimpleAttribute(string jsonFile)
        {
            FileName = jsonFile;
        }

        public string ErrorMessage { get; private set; } = string.Empty;

        public string FileName { get; private set; }

        public bool HasError { get; private set; }

        public string TableName => Path.GetFileNameWithoutExtension(FileName);

        public string ValueSqlTypeName { get; private set; } = "NVARCHAR(MAX)";

        public void Load()
        {
            using var fs = File.OpenRead(FileName);
            using JsonDocument document = JsonDocument.Parse(fs);

            JsonElement root = document.RootElement;
            foreach (JsonElement point in root.EnumerateArray())
            {
                if (point.TryGetProperty("value", out JsonElement value))
                {
                    if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
                    {
                        //don't know what type value is yet, so keep going
                        continue;
                    }
                    else
                    {
                        ValueSqlTypeName = SqlTypeFromJsonElement(value);
                        return;
                    }
                }
                else
                {
                    ErrorMsg("Expecting 'value' property");
                    HasError = true;
                    return;
                }
            }

            //never found out what type value was
            HasError = true;
        }

        private static string SqlTypeFromJsonElement(JsonElement jsonElement)
        {
            var text = jsonElement.GetRawText();
            return jsonElement.ValueKind switch
            {
                JsonValueKind.False => "BIT",
                JsonValueKind.True => "BIT",
                JsonValueKind.Number when int.TryParse(text, out var _) => "INT",
                JsonValueKind.Number when !int.TryParse(text, out var _) => "FLOAT",
                _ => "NVARCHAR(MAX)" // treat anything else is string
            };
        }

        private void ErrorMsg(string message)
        {
            ErrorMessage = message;
            Console.Error.WriteLine($"Error from {TableName}: {message}");
        }
    }
}