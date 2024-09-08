# Command Line Interface

Yafc can be invoked via command line:

`./Yafc [arguments]`

For the list of arguments and their purpose, run `./Yafc --help`.

Yafc can be started without any arguments.
However, if arguments other than `--help` are supplied, it is mandatory that the first argument is the path to the data-directory of Factorio.
The rest of the arguments are optional in this case.

Providing paths that do not exist will result in the program printing an error and exiting, regardless of whether the argument is optional.
