using System;
using System.IO;

namespace ExistExportToSQL
{
    public class Program
    {
        /// <summary>
        /// Generates a SQL import script from a folder of a full json export from Exist.io
        /// </summary>
        /// <param name="inputFolder">Folder containing Exist.io json files.  Defaults to current directory.</param>
        /// <param name="outputFile">Path to output SQL script.  Defaults to inputFolder\ImportExistJson.sql</param>
        private static void Main(DirectoryInfo? inputFolder = null, FileInfo? outputFile = null)
        {
            if (inputFolder == null)
            {
                inputFolder = new DirectoryInfo(Directory.GetCurrentDirectory());
            }

            if (outputFile == null)
            {
                outputFile = new FileInfo(Path.Combine(inputFolder.FullName, "ImportExistJson.sql"));
            }

            var dropFile = Path.Combine(outputFile.Directory.FullName, "DropExistTables.sql");

            var gen = new ScriptGenerator();

            gen.Folder = inputFolder.FullName;
            gen.GenerateFromFolder();

            File.WriteAllText(outputFile.FullName, gen.CreateScript);
            File.WriteAllText(dropFile, gen.DropScript);
        }
    }
}