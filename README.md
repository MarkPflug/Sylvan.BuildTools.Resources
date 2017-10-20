# Elemental.JsonResource
Json Resource file support in C# projects.

This provides an alternative to using resx files to defined resources in C# projects.
The benefits over resx are:
- human readable file format (try writing resx xml from scratch without documentation)
- generated C# code doesn't get included in project/source control
- Doesn't require modifying the .csproj (adding a single resx file will add ~12 lines to your csproj file)
- Doesn't require Visual Studio to function. (resx files don't work in VS Code for example)

Json resource files use the ".resj" file extension, and a very simple json document to specify resources.
Currently supports strings and text files. Text file path should be specified relative to the resj file location. Supports creating localized satellite assemblies using the same naming convention as resx.

Example files:


`[Resources.json]`
```json
{
  "Strings": {
    "Greeting": "Hello, Resj",
    "Farewell": "Goodbye, Resx"
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

Currently, only works with full .net framework builds, does not work with .net core builds. Working on that next...
