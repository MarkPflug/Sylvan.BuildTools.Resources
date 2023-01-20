using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Sylvan.BuildTools.Resources
{
	public class StaticResourceGenerator : Task
	{
		[Required]
		// The folder where we will write all of our generated code.
		public string OutputPath { get; set; }

		// The folders to generate types from.
		public ITaskItem[] InputFolders { get; set; }

		public string Namespace { get; set; }

		// Will contain all of the generated coded we create
		[Output]
		public ITaskItem[] OutputCode { get; set; }

		bool hasError = false;

		const string AccessPublic = "Public";

		// The method that is called to invoke our task.
		public override bool Execute()
		{
			if (InputFolders == null)
				return true;

			var outCodeItems = new List<TaskItem>();
			var generatorType = this.GetType();
			string generatorName = generatorType.FullName;
			string generatorVersion = generatorType.GetTypeInfo().Assembly.GetName().Version.ToString();

			// loop over all the folders
			foreach (var folder in InputFolders)
			{
				if (!Directory.Exists(folder.ItemSpec))
				{
					this.Log.LogError($"StaticResourceFolder {folder.ItemSpec} does not exist.");
					continue;
				}

				var root = Path.GetFileName(folder.ItemSpec);


				Directory.CreateDirectory(OutputPath);

				var codeFile = Path.Combine(OutputPath, folder.ItemSpec + ".g.cs");
				var access = folder.GetMetadata("AccessModifier");

				var isPublic = StringComparer.OrdinalIgnoreCase.Equals(access, AccessPublic);

				var ns = folder.GetMetadata("Namespace");
				ns = string.IsNullOrWhiteSpace(ns) ? Namespace : ns;

				outCodeItems.Add(new TaskItem(codeFile));
				Directory.CreateDirectory(Path.GetDirectoryName(codeFile));

				using (var oStream = new FileStream(codeFile, FileMode.Create))
				using (var w = new StreamWriter(oStream))
				{
					if (!string.IsNullOrEmpty(ns))
					{
						w.WriteLine($"namespace {ns} {{");
						w.WriteLine();
					}

					w.WriteLine($@"[global::System.CodeDom.Compiler.GeneratedCodeAttribute(""{generatorName}"", ""{generatorVersion}"")]");
					WriteClass(w, folder.ItemSpec, isPublic);

					if (!string.IsNullOrEmpty(ns))
					{
						w.WriteLine("}");
						w.WriteLine();
					}
				}
			}

			// put the artifacts we created in the output properties.
			OutputCode = outCodeItems.ToArray();
			return !hasError;
		}

		void WriteClass(TextWriter w, string folder, bool isPublic, int depth = 0)
		{

			var className = Path.GetFileNameWithoutExtension(folder);
			className = IdentifierStyle.PascalCase.Convert(className);

			// very simplistic resource accessor class mostly duplicated from resx output.
			w.Write($@"

/// <summary>
/// A static resource class, for looking up strings, etc.
/// </summary>
[global::System.Diagnostics.DebuggerNonUserCode()]

{((isPublic || depth > 0) ? "public " : string.Empty)}static partial class {className}
{{
");

			w.Write($@"
    /// <summary>
	/// Allow some custom string processing.
    /// </summary>
    static partial void PreProcess(ref string s);

    static string Process(string str)
    {{
        PreProcess(ref str);
        return str;
    }}
");

			foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly))
			{
				var name = Path.GetFileNameWithoutExtension(file);

				name = IdentifierStyle.PascalCase.Convert(name);


				var comment = $"Gets the string contents of {Path.GetFileName(file)}.";
				if (comment != null)
				{
					IEnumerable<string> commentLines = comment.Split(new[] { "\r", "\n", "\r\n" }, StringSplitOptions.None);
					w.Write($@"
    /// <summary>
    /// {string.Join(Environment.NewLine + "    /// ", commentLines)}.
    /// </summary>");
				}

				var str = CSharpStringEscape(File.ReadAllText(file));
				w.Write($@"
    public static readonly string {name} = Process(""{str}"");
");

				foreach(var child in Directory.EnumerateDirectories(folder))
				{
					WriteClass(w, child, isPublic, depth + 1);
				}
			}

			w.WriteLine("}");

		}

		static string CSharpStringEscape(string str)
		{
			var sb = new StringBuilder();
			foreach (char c in str)
			{
				switch (c)
				{
					case '\n':
						sb.Append("\\n");
						break;
					case '\r':
						sb.Append("\\r");
						break;
					case '\'':
						sb.Append("\\'");
						break;
					case '\"':
						sb.Append("\\\"");
						break;
					case '\\':
						sb.Append("\\\\");
						break;
					case '\a':
						sb.Append("\\a");
						break;
					case '\b':
						sb.Append("\\b");
						break;
					case '\f':
						sb.Append("\\f");
						break;
					case '\t':
						sb.Append("\\t");
						break;
					case '\v':
						sb.Append("\\v");
						break;
					case '\0':
						sb.Append("\\0");
						break;
					default:
						// if it is a printable ASCII,
						// write it verbatim
						if (c >= ' ' && c <= 127)
						{
							sb.Append(c);
						}
						else
						{
							sb.Append("\\u");
							sb.Append(((int)c).ToString("X4"));
						}
						break;
				}
			}

			return sb.ToString();
		}
	}
}

