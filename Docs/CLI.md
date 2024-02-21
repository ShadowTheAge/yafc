# Command line interface

YAFC can be invoked via command line:

`yafc PROJECT_FILE DATA_DIR MODS_DIR [expensive]`

## Description
- `PROJECT_FILE`: The path to the project file that should be opened.

- `DATA_DIR`: The path to Factorio's data directory containing game data.

- `MODS_DIR`: The path to Factorio's mods directory containing additional game modifications.

- `expensive`: An optional parameter to use expensive variants of recipes. If provided, the program will use expensive recipes. If not provided, the normal recipe cost is used.

## Examples
These examples assume that Factorio is installed in `C:\Factorio`.

To open a project file called my_project.yafc located in the current directory:
`yafc my_project.yafc "C:\Factorio\data" "C:\Factorio\mods"`

To open the same project file with expensive recipes:
`yafc my_project.yafc "C:\Factorio\data" "C:\Factorio\mods" expensive`
