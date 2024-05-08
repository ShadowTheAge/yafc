using System;
using System.IO;

namespace Yafc {
    public static class CommandLineParser {
        public static string lastError { get; private set; } = string.Empty;
        public static bool helpRequested { get; private set; }

        public static bool errorOccured => !string.IsNullOrEmpty(lastError);

        public static ProjectDefinition Parse(string[] args) {
            ProjectDefinition options = new ProjectDefinition();

            if (args.Length == 0) {
                return options;
            }

            if (!args[0].StartsWith("--")) {
                options.dataPath = args[0];
                if (!Directory.Exists(options.dataPath)) {
                    lastError = $"Data path '{options.dataPath}' does not exist.";
                    return null;
                }
            }

            for (int i = string.IsNullOrEmpty(options.dataPath) ? 0 : 1; i < args.Length; i++) {
                switch (args[i]) {
                    case "--mods-path":
                        if (i + 1 < args.Length && !IsKnownParameter(args[i + 1])) {
                            options.modsPath = args[++i];

                            if (!Directory.Exists(options.modsPath)) {
                                lastError = $"Mods path '{options.modsPath}' does not exist.";
                                return null;
                            }
                        }
                        else {
                            lastError = "Missing argument for --mods-path.";
                            return null;
                        }
                        break;

                    case "--project-file":
                        if (i + 1 < args.Length && !IsKnownParameter(args[i + 1])) {
                            options.path = args[++i];

                            if (!File.Exists(options.path)) {
                                lastError = $"Project file '{options.path}' does not exist.";
                                return null;
                            }
                        }
                        else {
                            lastError = "Missing argument for --project-file.";
                            return null;
                        }
                        break;

                    case "--expensive":
                        options.expensive = true;
                        break;

                    case "--help":
                        helpRequested = true;
                        break;

                    default:
                        lastError = $"Unknown argument '{args[i]}'.";
                        return null;
                }
            }

            return options;
        }

        public static void PrintHelp() {
            Console.WriteLine(@"Usage:
Yafc [<data-path> [--mods-path <path>] [--project-file <path>] [--expensive]] [--help]

Description:
    Yafc can be started without any arguments. However, if arguments are supplied, it is
    mandatory that the first argument is the path to the data directory of Factorio. The
    other arguments are optional in any case.

Options:
    <data-path>
        Path of the data directory (mandatory, if other arguments are supplied)

    --mods-path <path>
        Path of the mods directory (optional)

    --project-file <path>
        Path of the project file (optional)

    --expensive
        Enable expensive mode (optional)

    --help
        Display this help message and exit

Examples:
    1. Starting Yafc without any arguments:
       $ ./Yafc
       This opens the welcome screen.

    2. Starting Yafc with the path to the data directory of Factorio:
       $ ./Yafc Factorio/data
       This opens a fresh project and loads the game data from the supplied directory.
       Fails if the directory does not exist.

    3. Starting Yafc with the paths to the data directory and a project file:
       $ ./Yafc Factorio/data --project-file my-project.yafc
       This opens the supplied project and loads the game data from the supplied data
       directory. Fails if the directory and/or the project file do not exist.

    4. Starting Yafc with the paths to the data & mods directories and a project file:
       $ ./Yafc Factorio/data --mods-path Factorio/mods --project-file my-project.yafc
       This opens the supplied project and loads the game data and mods from the supplied
       data and mods directories. Fails if any of the directories and/or the project file
       do not exist.");
        }

        private static bool IsKnownParameter(string arg) {
            return arg is "--mods-path" or "--project-file" or "--expensive" or "--help";
        }
    }
}
