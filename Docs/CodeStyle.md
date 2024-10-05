# Yafc Code Style

The Code Style describes the conventions that we use in the Yafc project.  
For the ease of collective development, we ask you to follow the Code Style when contributing to Yafc.

### General guidelines

The main idea is to keep the code maintainable and readable.

* Aim for not-complicated code without dirty hacks.
* Please document the code. 
* If you also can document the existing code, that is even better. The ease of understanding makes reading the code more enjoyable.

### Commits
* Please separate the change of behavior from the refactoring, into different commits. That helps to understand your commits easier.
* Please update your branch by rebasing, not merging. Merges are expected only when a PR merges into master. In all other cases, they complicate the commit history, so please rebase instead.
* Please make meaningful commit messages.
* Commit-prefixes help to browse the commits later. We encourage you to use the following prefixes in commit subjects:
    * `docs` for documentation changes,
    * `feature` or `feat` for new features,
    * `fix` for bugfixes,
    * `refactor` for refactors,
    * `style` for the application of the code style,
    * `style-change` for the change of the code style,
    * `chore` is only for the changes that do not belong to any other prefix.

### Code
* In the programmers' haven, there is always a free spot for those who write tests.
* If you add a TODO, then please describe the details in the commit message, or ideally in a github issue. That increases the chances of the TODOs being addressed.

#### Blank lines
Blank lines help to separate code into thematic chunks. 
We suggest to leave blank lines around code blocks like methods and keywords.  
For instance,
```
public int funcName() {
    if() { // No blank line because there's nothing to separate from
        for (;;) { // No blank line because there's not much to separate from
            <more calls and assignments>
        }

        <more calls and assignments> // A blank line to clearly separate the code block from the rest of the code
    }

    <more calls and assignments> // A blank line to clearly separate the code block from the rest of the code
}

private void Foo() => Baz(); // A blank line between functions

private void One(<a long description
    that goes on for
    several lines) {

    <more calls and assignments> // A blank line to clearly separate the definition and the start of the function
}
```

### Line wrap
Please try to keep the lines shorter than 190 characters, so they fit on most monitors.

When wrapping a line, you can use the following example as a guideline for which wrapping is preferred:
```
if (recipe.subgroup == null
    // (1) Add breaks at operators before adding them within the expressions passed to those operators
    && imgui.BuildRedButton("Delete recipe")
        // (2) Add breaks before .MethodName before adding breaks between method parameters
        .WithTooltip(imgui,
            // (3) Add breaks between method parameters before adding breaks within method parameters
            "Shortcut: right-click")
    // (1)
    && imgui.CloseDropdown()) {
```

Most of the operators like `.` `+` `&&` go to the next line.  
The notable operators that stay on the same line are `=>`, `=> {`, and `,`.

The wrapping of arguments in constructors and method-definitions is up to you.
