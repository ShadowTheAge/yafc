# Contributor's Guide

Here are a couple of things to make your experience in the community more enjoyable. 

## Coding
* Please use the same code style as the rest of the codebase. Visual Studio should pick up most of it from `.editorconfig`. 
* Please prioritize maintainability. Aim for understandable code without dirty hacks.
* Please separate refactoring and the change of behavior into different commits, so it is easier to review the PR.
* Please document the code. If you also can document the existing code, that would be awesome. More documentation helps others to understand the code faster and make the work on YAFC more enjoyable.
* Feel free to put [prefixes](https://www.conventionalcommits.org/en/v1.0.0-beta.2/#summary) in the subjects of your commits -- they might help to browse them later.
* In the programmers-haven, there is always a free spot for those who write tests.

## Pull Request
* When making a Pull Request, please give some context. For instance, if the PR solves an issue, then you can put a short description of the issue in the PR.
* If there is no corresponding issue, then describe what the problem was and how your PR fixes it. The context, the state without the fix, and with the fix.
* Please provide a short description of your change in the [changelog](https://github.com/have-fun-was-taken/yafc-ce/blob/master/changelog.txt).
* Make meaningful commit messages. The easier it is to understand your PR, the faster it will be merged.
* It would be appreciated if you reorganize your commits before the merge -- separate and squash them into logical steps, so they are easier to review and understand. For instance, the fixes to the commits can be squashed into them. The reordering of the commits can be done with the interactive rebase: `git rebase -i head~n`, but please read beforehand on how to do it, so you don't accidentally delete your efforts. If you want to go godlike, feel free to read on `git commit --fixup=` and `git rebase -i --autosquash` ([example](https://stackoverflow.com/questions/3103589/how-can-i-easily-fixup-a-past-commit)). However, it can be the case that the reordering requires very tricky merges, so it's okay to leave them as-is in this case.
