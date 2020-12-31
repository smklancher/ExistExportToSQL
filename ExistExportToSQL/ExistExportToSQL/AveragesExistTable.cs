using System;
using System.Collections.Generic;
using System.Text;

namespace ExistExportToSQL
{
    internal class AveragesExistTable : ExistTable
    {
        public AveragesExistTable(string jsonFile) : base(jsonFile)
        {
        }

        public override string TableName => $"{Parts.TypeName}_{base.TableName}";

        public override string ImportScript(bool dropTableFirst)
        {
            return BasicImportStart(dropTableFirst) + $"WITH (attribute NVARCHAR(255), date DATE, overall FLOAT, monday FLOAT, tuesday FLOAT, wednesday FLOAT, thursday FLOAT, friday FLOAT, saturday FLOAT, sunday FLOAT)\r\n";
        }
    }
}