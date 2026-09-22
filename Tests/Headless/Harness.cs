using System;
using System.Collections.Generic;

namespace MadVoxel.Headless
{
    /// <summary>A deliberately tiny assert harness - no test framework dependency.</summary>
    public static class Harness
    {
        public static int Passed;
        public static readonly List<string> Failures = new List<string>();

        static string _section = "";

        public static void Section(string name)
        {
            _section = name;
            Console.WriteLine();
            Console.WriteLine(name);
        }

        public static void Check(bool condition, string message)
        {
            if (condition)
            {
                Passed++;
                Console.WriteLine("  ok    " + message);
                return;
            }
            Failures.Add(_section + " / " + message);
            Console.WriteLine("  FAIL  " + message);
        }

        public static void Equal<T>(T actual, T expected, string message)
        {
            bool ok = EqualityComparer<T>.Default.Equals(actual, expected);
            Check(ok, message + (ok ? "" : string.Format(" (expected {0}, got {1})", expected, actual)));
        }

        public static int Report()
        {
            Console.WriteLine();
            Console.WriteLine(new string('-', 62));
            if (Failures.Count == 0)
            {
                Console.WriteLine(string.Format("ALL {0} CHECKS PASSED", Passed));
                return 0;
            }

            Console.WriteLine(string.Format("{0} passed, {1} FAILED:", Passed, Failures.Count));
            for (int i = 0; i < Failures.Count; i++) Console.WriteLine("  - " + Failures[i]);
            return 1;
        }
    }
}
