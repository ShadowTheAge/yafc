# Contributor's Guide

Here are a couple of things to make your experience in the community more enjoyable. 

## Set up environment
* Please inspect and run [set-up-git-hooks.sh](/set-up-git-hooks.sh) once. It sets up a formatting check to be run before `git push`.

## Coding
* For the conventions that we use, please refer to the [Code Style](/Docs/CodeStyle.md).
* In Visual Studio, you can check some of the rules by running Code Cleanup, or Format Document with "Ctrl+K, Ctrl+D".

## Pull Request
* In the [changelog](https://github.com/have-fun-was-taken/yafc-ce/blob/master/changelog.txt), please provide a short description of your change.
* In the PR, please provide a short description of the issue, and how your PR solves that issue.
* It would be appreciated if you reorganize your commits before the merge -- separate and squash them into logical steps, so they are easier to review and understand. For instance, the fixes to the commits can be squashed into them. The reordering of the commits can be done with the interactive rebase: `git rebase -i head~n`, but please read beforehand on how to do it, so you don't accidentally delete your efforts. If you want to go godlike, feel free to read on `git commit --fixup=` and `git rebase -i --autosquash` ([example](https://stackoverflow.com/questions/3103589/how-can-i-easily-fixup-a-past-commit)). However, it can be the case that the reordering requires very tricky merges, so it's okay to leave the commits as-is in this case.
