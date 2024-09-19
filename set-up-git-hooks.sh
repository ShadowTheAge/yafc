# This file sets up a part of the environment that cannot
# be set up automatically due to security concerns.

# This line sets up a git-hooks directory.
# Hooks are the commands that run when you do certain
# things in the repository.
# See: https://git-scm.com/book/en/v2/Customizing-Git-Git-Hooks
# Feel free to check the hooks before running this command.
git config core.hooksPath .githooks
