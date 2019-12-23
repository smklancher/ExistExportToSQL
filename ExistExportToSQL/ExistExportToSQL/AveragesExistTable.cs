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

        public override string ImportScript()
        {
            return BasicImportStart() + $"WITH (attribute NVARCHAR(255), date DATE, overall FLOAT, monday FLOAT, tuesday FLOAT, wednesday FLOAT, thursday FLOAT, friday FLOAT, saturday FLOAT, sunday FLOAT)\r\n";
        }
    }
}