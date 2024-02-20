using System;
using YAFC;
using YAFC.Model;
using YAFC.Parser;

namespace CommandLineToolExample {
    // If you wish to embed yafc or make a command-line tool using YAFC, here is an example on how to do that
    // However, I can't make any promises about not changing signatures
    public static class Program {
        public static void Main(string[] args) {
            if (args.Length == 0) {
                Console.WriteLine("Pass FactorioData path as command-line argument");
                return;
            }
            YafcLib.Init();
            YafcLib.RegisterDefaultAnalysis(); // Register analysis to get cost, milestones, accessibility, etc information. Skip if you just need data. 
            var factorioPath = args[0];
            var errorCollector = new ErrorCollector();
            Project project;
            try {
                // Load YAFC project.
                // Empty project path loads default project (with one empty page).
                // Project is irrelevant if you just need data, but you need it to perform sheet calculations
                // Set to not render any icons
                project = FactorioDataSource.Parse(factorioPath, "", "", false, new ConsoleProgressReport(), errorCollector, "en", false);
            }
            catch (Exception ex) {
                // Critical errors that make project un-loadable will be thrown as exceptions
                Console.Error.WriteException(ex);
                return;
            }
            if (errorCollector.severity != ErrorSeverity.None) {
                // Some non-critical errors were found while loading project, for example missing recipe or analysis warnings
                foreach (var (error, _) in errorCollector.GetArrErrors()) {
                    Console.Error.WriteLine(error);
                }

            }

            // To confirm project loading, enumerate all objects
            foreach (var obj in Database.objects.all) {
                Console.WriteLine(obj.locName);
            }
        }

        private class ConsoleProgressReport : IProgress<(string, string)> {
            public void Report((string, string) value) {
                Console.WriteLine(value.Item1 + "  -  " + value.Item2);
            }
        }
    }
}