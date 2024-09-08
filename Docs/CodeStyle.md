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
* Please make meaningful commit messages. We encourage you to use the prefixes from the [conventional commits](https://www.conventionalcommits.org/en/v1.0.0-beta.2/#summary). They help to browse the commits later.

### Code
* In the programmers' haven, there is always a free spot for those who write tests.
* Please try to keep the lines shorter than 120 characters. It simplifies the review of the changes on popular monitors.
* If you add a TODO, then please describe the details in the commit message, or ideally in a github issue. That increases the chances of the TODOs being addressed.

#### Difference with C# conventions
Our conventions differ from the [Microsoft C# conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names)
due to legacy serialization and personal preferences.  
The most notable difference that is not controlled by [.editorconfig](/.editorconfig) is that class properties are named in camelCase.

#### Blank lines
Blank lines help to separate code into thematic chunks. 
We suggest to leave blank lines around code blocks like methods and keywords.  
For instance,
```
public int funcName() {
    if() { // No blank line because there's nothing to separate from
        for (;;) { // No empty line because there's not much to separate from
            <more calls and assignments>
        }

        <more calls and assignments> // An empty line to clearly separate the code block from the rest of the code
    }

    <more calls and assignments> // An empty line to clearly separate the code block from the rest of the code
}

private void Foo() => Baz(); // An empty line between functions

private void One(<a long description
    that goes on for
    several lines) {

    <more calls and assignments> // An empty line to clearly separate the definition and the start of the function
}
```
