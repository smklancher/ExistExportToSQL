﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

        public override string ImportScript()
        {
            return BasicImportStart() + $"WITH(date DATE, value {ValueSqlTypeName})\r\n";
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

        private void Load()
        {
            try
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

                //never found out what type value was due to all null
                HasError = true;
                ErrorMessage = "Data is all null";
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
            }
        }
    }
}