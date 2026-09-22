using System;
using MadVoxel.Content;

namespace MadVoxel.Headless
{
    /// <summary>
    /// Runs MadVoxel's engine-agnostic core outside Unity. See README.md in this folder
    /// for what is and is not covered.
    /// </summary>
    public static class Program
    {
        public static int Main()
        {
            Console.WriteLine("MadVoxel headless checks");
            Console.WriteLine(new string('=', 62));

            ContentTests.Run();
            var db = ContentTests.Database;

            if (db == null)
            {
                Console.WriteLine("Content failed to build; skipping the rest.");
                return Harness.Report();
            }

            VoxelTests.Run(db);
            InventoryTests.Run(db);
            SaveTests.Run(db);

            if (UnityEngine.Debug.Warnings.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Warnings logged during the run:");
                foreach (var warning in UnityEngine.Debug.Warnings) Console.WriteLine("  ! " + warning);
            }

            return Harness.Report();
        }
    }
}
