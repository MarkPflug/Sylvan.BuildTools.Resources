# Elemental.JsonResource
Json Resource file support in C# projects.

This provides an alternative to using resx files to defined resources in C# projects.
The benefits over resx are:
- human readable file format (try writing resx xml from scratch without documentation)
- generated C# code doesn't get included in project/source control (unlike designer.cs files)
- Doesn't require modifying the .csproj (adding a single resx file will add ~12 lines to your csproj file)
- Doesn't require Visual Studio to function. (resx files don't work in VS Code for example)

Referencing the Elemental.JsonResource package will *not* add any dependency to your project. 
The package operates at build time and will embed resources in your output assembly, and includes compiled code files containing resource accessors.

Json resource files use the ".resj" file extension, and a very simple json document to specify resources.
Currently supports strings and text files. Text file path should be specified relative to the resj file location. Supports creating localized satellite assemblies using the same naming convention as resx.

Example files:

`[Resources.json]`
```json
{
  "Strings": {
    "Greeting": "Hello, Resj",
    "Farewell": "Goodbye, Resx"
  },
  "Files": {
    "ComplexQuery": "sql/query.sql"
  }
}
```

`[Resources.de-DE.json]`
```json
{
  "Strings": {
    "Greeting": "Hallo, Resj",
    "Farewell": "Auf Wiedersehen, Resx"
  }
}
```

## Running Elemental.JsonResource.Tests on Unix
If you'd like to develop Elemental.JsonResource under Mono, and encounter issues with reference assemblies not being found while running tests, you might need to run something similar beforehand ([source1](https://stackoverflow.com/a/55070707), [source2](https://github.com/Microsoft/msbuild/issues/2728#issuecomment-345381357)):
```sh
export FrameworkPathOverride=/lib/mono/4.6-api
```
