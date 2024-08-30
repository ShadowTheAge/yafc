using System;
using System.IO;
using System.Linq;

namespace Yafc {
    public static class CommandLineParser {
        public static string lastError { get; private set; } = string.Empty;
        public static bool helpRequested { get; private set; }

        public static bool errorOccured => !string.IsNullOrEmpty(lastError);

        public static ProjectDefinition? Parse(string[] args) {
            ProjectDefinition projectDefinition = new ProjectDefinition();

            if (args.Length == 0) {
                return projectDefinition;
            }

            if (args.Length == 1 && !args[0].StartsWith("--")) {
                return LoadProjectFromPath(Path.GetFullPath(args[0]));
            }

            if (!args[0].StartsWith("--")) {
                projectDefinition.dataPath = args[0];
                if (!Directory.Exists(projectDefinition.dataPath)) {
                    lastError = $"Data path '{projectDefinition.dataPath}' does not exist.";
                    return null;
                }
            }

            for (int i = string.IsNullOrEmpty(projectDefinition.dataPath) ? 0 : 1; i < args.Length; i++) {
                switch (args[i]) {
                    case "--mods-path":
                        if (i + 1 < args.Length && !IsKnownParameter(args[i + 1])) {
                            projectDefinition.modsPath = args[++i];

                            if (!Directory.Exists(projectDefinition.modsPath)) {
                                lastError = $"Mods path '{projectDefinition.modsPath}' does not exist.";
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
                            projectDefinition.path = args[++i];

                            if (!File.Exists(projectDefinition.path)) {
                                lastError = $"Project file '{projectDefinition.path}' does not exist.";
                                return null;
                            }
                        }
                        else {
                            lastError = "Missing argument for --project-file.";
                            return null;
                        }
                        break;

                    case "--expensive":
                        projectDefinition.expensive = true;
                        break;

                    case "--help":
                        helpRequested = true;
                        break;

                    default:
                        lastError = $"Unknown argument '{args[i]}'.";
                        return null;
                }
            }

            return projectDefinition;
        }

        public static void PrintHelp() => Console.WriteLine(@"Usage:
Yafc [<data-path> [--mods-path <path>] [--project-file <path>] [--expensive]] [--help]

Description:
    Yafc can be started without any arguments. However, if arguments are supplied, it is
    mandatory that the first argument is the path to the data directory of Factorio. The
    other arguments are optional in any case.

Options:
    <data-path>
        Path of the data directory (mandatory if other arguments are supplied)

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

    2. Starting Yafc with a project path:
       $ ./Yafc path/to/my/project.yafc
       Skips the welcome screen and loads the project. If the project has not been
       opened before, then uses the start-settings of the most-recently-opened project.

    3. Starting Yafc with the path to the data directory of Factorio:
       $ ./Yafc Factorio/data
       This opens a fresh project and loads the game data from the supplied directory.
       Fails if the directory does not exist.

    4. Starting Yafc with the paths to the data directory and a project file:
       $ ./Yafc Factorio/data --project-file my-project.yafc
       This opens the supplied project and loads the game data from the supplied data
       directory. Fails if the directory and/or the project file do not exist.

    5. Starting Yafc with the paths to the data & mods directories and a project file:
       $ ./Yafc Factorio/data --mods-path Factorio/mods --project-file my-project.yafc
       This opens the supplied project and loads the game data and mods from the supplied
       data and mods directories. Fails if any of the directories and/or the project file
       do not exist.");

        /// <summary>
        /// Loads the project from the given path. <br/>
        /// If the project has not been opened before, then fetches other settings (like mods-folder) from the most-recently opened project.
        /// </summary>
        private static ProjectDefinition? LoadProjectFromPath(string fullPathToProject) {
            // Putting this part as a separate method makes reading the parent method easier.

            bool previouslyOpenedProjectMatcher(ProjectDefinition candidate) {
                bool pathIsNotEmpty = !string.IsNullOrEmpty(candidate.path);

                return pathIsNotEmpty && string.Equals(fullPathToProject, Path.GetFullPath(candidate.path!), StringComparison.OrdinalIgnoreCase);
            }

            ProjectDefinition[] recentProjects = Preferences.Instance.recentProjects;
            ProjectDefinition? projectToOpen = recentProjects.FirstOrDefault(previouslyOpenedProjectMatcher);

            if (projectToOpen == null && recentProjects.Length > 0) {
                ProjectDefinition donor = recentProjects[0];
                projectToOpen = new ProjectDefinition(fullPathToProject, donor.dataPath, donor.modsPath, donor.expensive, donor.netProduction);
            }

            return projectToOpen;
        }

        private static bool IsKnownParameter(string arg) => arg is "--mods-path" or "--project-file" or "--expensive" or "--help";
    }
}
