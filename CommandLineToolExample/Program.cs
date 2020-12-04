using System;
using System.Globalization;
using System.Threading;
using YAFC;
using YAFC.Model;
using YAFC.Parser;

namespace CommandLineToolExample
{
    // If you wish to embed yafc or make a command-line tool using YAFC, here is an example on how to do that
    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Pass FactorioData path as command-line argument");
                return;
            }
            YafcLib.Init();
            YafcLib.RegisterDefaultAnalysis();
            var factorioPath = args[0];
            var errorCollector = new ErrorCollector();
            Project project;
            try
            {
                project = FactorioDataSource.Parse(factorioPath, "", "", false, new ConsoleProgressReport(), errorCollector, "en");
            }
            catch (Exception ex)
            {
                // Critical errors that make project un-loadable will be thrown as exceptions
                Console.Error.WriteException(ex);
                return;
            }
            if (errorCollector.severity != ErrorSeverity.None)
            {
                // Some non-critical errors were found while loading project, for example missing recipe or analysis warnings
                foreach (var (error, _) in errorCollector.GetArrErrors())
                {
                    Console.Error.WriteLine(error);
                }
                
            }
            
            // To confirm project loading, enumerate all objects
            foreach (var obj in Database.objects.all)
            {
                Console.WriteLine(obj.locName);
            }
        }

        private class ConsoleProgressReport : IProgress<(string, string)>
        {
            public void Report((string, string) value)
            {
                Console.WriteLine(value.Item1 +"  -  " + value.Item2);
            }
        }
    }
}