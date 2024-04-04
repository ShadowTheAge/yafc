# Command line interface

YAFC can be invoked via command line:

`YAFC [<data-path>] [--mods-path <path>] [--project-file <path>] [--expensive] [--help]`

YAFC can be started without any arguments. However, if arguments other than `--help` are supplied, it is mandatory that the first argument is the path to the data directory of Factorio. The other arguments are optional in any case.

Regardless of whether an option is optional or not, providing paths that do not exist will result in the program printing an error message and exiting.


## Description
`<data-path>`: The path of the Factorio's data directory containing the game data (mandatory, if any arguments other than `--help` are supplied)

`--mods-path <path>`: The path to Factorio's mods directory containing additional game modifications (optional)

`--project-file <path>`: The path to the project file that should be opened (optional)

`--expensive`: Enable expensive recipes (optional)

`--help`: Display a help message and exit


## Examples
These examples assume that Factorio is installed in `/home/user/Factorio` and the current working directory is `/home/user/YAFC`. A project file is placed at `/home/user/YAFC/my-project.yafc`

#### Starting YAFC without any arguments:
`$ ./YAFC`

This opens the welcome screen, where the user can choose any paths.

#### Starting YAFC with the path to the data directory of Factorio:
`$ ./YAFC ../Factorio/data`

This opens a fresh project and loads the game data from the supplied directory.
Fails if the directory does not exist.

#### Starting YAFC with the paths to the data directory and a project file:
`$ ./YAFC ../Factorio/data --project-file my-project.yafc`

This opens the supplied project and loads the game data from the supplied data directory.
Fails if the directory and/or the project file do not exist.

#### Starting YAFC with the paths to the data & mods directories and a project file:
`$ ./YAFC ../Factorio/data --mods-path Factorio/mods --project-file my-project.yafc`

This opens the supplied project and loads the game data and mods from the supplied data and mods directories. Fails if any of the directories and/or the project file do not exist.
