# Sylvan.BuildTools.Resources

Provides resource file support and code generation in C# projects.
Referencing the `Sylvan.BuildTools.Resources` package will *not* 
add a rumtine dependency to your project, nor
create a transitive package dependency for your nuget package. 
The package operates at build time and will embed resources in your output assembly, 
and includes compiled code files containing resource accessors.

## Localized Resource .resj Files

JSON resource files provides an alternative to using resx XML resource files to define resources in C# projects.

The benefits over resx are:

- human authorable file format (resx is human readable, but diffcult to autor without documentation or tooling support)
- generated C# code doesn't get included in project/source control (unlike designer.cs files)
- Doesn't require modifying the .csproj (adding a single resx file will add ~12 lines to your csproj file)
- Implemented with build-time code-gen and doesn't require Visual Studio to function. (resx files require Visual Studio design time code gen)
- Still get Intellisense in Visual Studio for the generated code.

JSON resource files use the ".resj" file extension, and a very simple json document to specify resources.
Currently supports strings and text files. Text file path should be specified relative to the resj file location. 
Supports creating localized satellite assemblies using the same naming convention as resx.

Example files:

`[Resources.resj]`
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

`[Resources.de-DE.resj]`
```json
{
  "Strings": {
    "Greeting": "Hallo, Resj",
    "Farewell": "Auf Wiedersehen, Resx"
  }
}
```

You can control the resource generation specifying a custom namespace, and the visibility of the generated class:

```xml
  <ItemGroup>
    <JsonResource Update="Path/To/Resource.resj">
      <AccessModifier>Public</AccessModifier>
      <Namespace>CustomNS</Namespace>
      <ResourceName>CustomResourceName</ResourceName>
    </JsonResource>
  </ItemGroup>
```

## Static String Code Generation

The static string code generation feature allows adding string constants to your project where each string constant
is defined in a separate file in a folder structure. 
This is intended to support scenarios where you need to include long string constants that contain structured language
that would be better maintained in a separate file, instead of inlining the code in a C# file. 
This allows text like long SQL queries, chunks of HTML or JavaScript to be edited in a file with appropriate syntax highlighting,
then accessed from C# code as if it were defined as a native string constant.

Static resources are added to a project by defining a `StaticResourceFolder` item in the .csproj file, 
which names a folder that contains static resource files.

```xml
	<ItemGroup>
		<StaticResourceFolder Include="Sql"/>
	</ItemGroup>
```

If the `Sql` folder contains a file named `select_user_data.sql` it would generate code roughly equivalent to the following:

```
namespace ProjectRootNamespace {
    class Sql {
        public static readonly string SelectUserData = "...";
    }
}
```

The generated code is not included in the project source code, and the generated types/members will still be visible in intellisense.

## Release Notes:

_0.6.0_
 - Adds support for generating static code from a folder holding static resources (non-localizable).

_0.4.0_ 
 - Adds code comments to generated code, allowing projects that use WarningsAsErrors and DocumentationFile to compile without error.
 - Big thanks to [@teddybeermaniac](https://github.com/teddybeermaniac) for his contribution here.


## Running Sylvan.BuildTools.Resources.Tests on Unix

If you'd like to develop Sylvan.BuildTools.Resources under Mono, and encounter issues with reference assemblies not being found while running tests, you might need to run something similar beforehand ([source1](https://stackoverflow.com/a/55070707), [source2](https://github.com/Microsoft/msbuild/issues/2728#issuecomment-345381357)):
```sh
export FrameworkPathOverride=/lib/mono/4.6-api
```