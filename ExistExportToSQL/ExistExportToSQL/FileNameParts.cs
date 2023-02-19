using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExistExportToSQL
{
    internal class FileNameParts
    {
        public FileNameParts(string filepath)
        {
            FilePath = filepath;
            FileName = Path.GetFileNameWithoutExtension(FilePath).ToLowerInvariant();

            var parts = FileName.Split('_');
            TypeName = parts[0];

            var endsWithYear = false;
            if (int.TryParse(parts[^1], out int possibleYear))
            {
                if (possibleYear > 1900 && possibleYear < 2100)
                {
                    endsWithYear = true;
                    Year = possibleYear;
                    YearString = possibleYear.ToString();
                }
            }

            var endIndex = endsWithYear ? 1 : 0;
            var nameParts = parts[1..^endIndex];

            TableName = string.Join('_', nameParts);
        }

        public string FileName { get; init; }

        public string FilePath { get; init; }

        public string TableName { get; init; }

        public string TypeName { get; init; }

        public int Year { get; init; }

        public string YearString { get; init; } = string.Empty;
    }
}