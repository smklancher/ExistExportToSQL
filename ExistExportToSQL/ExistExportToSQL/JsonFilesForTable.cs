using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExistExportToSQL
{
    internal class JsonFilesForTable
    {
        public JsonFilesForTable(IGrouping<string, ExistJsonFile> fileGroup)
        {
            TableName = fileGroup.Key;
            Files = fileGroup.ToList();
            IsCustomTag = Files.All(x => x.ValuesLookLikeBool) && !Files.All(x => x.AllZeros);
            IsIntegerTrait = Files.All(x => x.IsIntegerTrait) && !Files.All(x => x.AllZeros);
        }

        public List<ExistJsonFile> Files { get; }

        public bool IsCustomTag { get; }

        public bool IsIntegerTrait { get; }

        public string TableName { get; }

        public static IEnumerable<JsonFilesForTable> FromAllFiles(IEnumerable<ExistJsonFile> files)
        {
            var filesByTableName = files.ToLookup(s => s.TableName);
            var tables = filesByTableName.Select(x => new JsonFilesForTable(x));
            return tables;
        }
    }
}