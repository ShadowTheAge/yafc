# Contributor's Guide

Here are a couple of things to make your experience in the community more enjoyable. 

## Coding
* Please use the same code style as the rest of the codebase. Visual Studio should pick up most of it from `.editorconfig`. You can autoformat the file with the sequence Ctrl+K, Ctrl+D.
* Please prioritize maintainability. Aim for understandable code without dirty hacks.
* Please separate the refactoring and the change of behavior into different commits, so it is easier to review the PR.
* Please document the code. If you can also document the existing code, that would be awesome. More documentation helps others to understand the code faster and make the work on YAFC more enjoyable.
* Feel free to put [prefixes](https://www.conventionalcommits.org/en/v1.0.0-beta.2/#summary) in the subjects of your commits. They might help to browse the commits later.
* If you add a TODO, then please describe the details in the commit message or, ideally, in a github issue. That increases the chances of the TODOs being addressed.
* Please keep the lines of code shorter than 120 characters -- it simplifies PR-reviews on popular monitors.
* In the programmers-haven, there is always a free spot for those who write tests.

## Pull Request
* Please provide a short description of your change in the [changelog](https://github.com/have-fun-was-taken/yafc-ce/blob/master/changelog.txt).
* Please provide context in the PR. For instance, if it solves an issue, then you can put a short description of the issue in it.
* Make meaningful commit messages. The easier it is to understand your PR, the faster it will be merged.
* It would be appreciated if you reorganize your commits before the merge -- separate and squash them into logical steps, so they are easier to review and understand. For instance, the fixes to the commits can be squashed into them. The reordering of the commits can be done with the interactive rebase: `git rebase -i head~n`, but please read beforehand on how to do it, so you don't accidentally delete your efforts. If you want to go godlike, feel free to read on `git commit --fixup=` and `git rebase -i --autosquash` ([example](https://stackoverflow.com/questions/3103589/how-can-i-easily-fixup-a-past-commit)). However, it can be the case that the reordering requires very tricky merges, so it's okay to leave them as-is in this case.
