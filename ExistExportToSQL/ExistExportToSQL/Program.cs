using System;
using System.IO;

namespace ExistExportToSQL
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var gen = new ScriptGenerator();

            gen.Folder = Directory.GetCurrentDirectory();
            gen.GenerateFromFolder();

            var output = Path.Combine(gen.Folder, "ImportExistJson.sql");
            File.WriteAllText(output, gen.Script);
        }
    }
}