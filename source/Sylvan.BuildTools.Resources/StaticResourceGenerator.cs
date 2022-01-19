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

		// Will contain all of the generated coded we create
		[Output]
		public ITaskItem[] OutputCode { get; set; }

		bool hasError = false;

		//class FileErrorLogger
		//{
		//	JsonResourceGenerator task;
		//	string file;

		//	public FileErrorLogger(JsonResourceGenerator task, string file)
		//	{
		//		this.task = task;
		//		this.file = file;
		//	}

		//	public bool JsonError(JsonErrorCode error, Location loc)
		//	{
		//		task.hasError = true;
		//		task.Log.LogError(null, "JP" + ((int) error).ToString(), null, file, loc.Line, loc.Column, loc.Line, loc.Column, error.ToString(), null);
		//		return true;
		//	}

		//	public void ParseError(string message, JsonReader reader)
		//	{
		//		ParseError(message, reader.Start, reader.End);
		//	}

		//	public void ParseError(string message, Location start, Location end)
		//	{
		//		task.Log.LogError(null, "JS0001", null, file, start.Line, start.Column, end.Line, end.Column, message, null);
		//	}

		//	public void ParseError(string message, JsonNode node)
		//	{
		//		ParseError(message, node.Start, node.End);
		//	}
		//}

		const string AccessPublic = "Public";
		const string AccessInternal = "Internal";
		const string AccessNoCodeGen = "NoCodeGen";

		// The method that is called to invoke our task.
		public override bool Execute()
		{
			if (InputFolders == null)
				return true;

			var outCodeItems = new List<TaskItem>();
			string generatorName = typeof(JsonResourceGenerator).FullName;
			string generatorVersion = typeof(JsonResourceGenerator).GetTypeInfo().Assembly.GetName().Version.ToString();


			// loop over all the folders
			foreach (var folder in InputFolders)
			{
				if (!Directory.Exists(folder.ItemSpec))
				{
					//TODO: error

					continue;
				}

				
				var root = Path.GetFileName(folder.ItemSpec);

				var className = root;

				Directory.CreateDirectory(OutputPath);

				var codeFile = Path.Combine(OutputPath, folder.ItemSpec + ".g.cs");
				var access = folder.GetMetadata("AccessModifier");

				var isPublic = StringComparer.OrdinalIgnoreCase.Equals(access, AccessPublic);

				var ns = folder.GetMetadata("Namespace");

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


					// very simplistic resource accessor class mostly duplicated from resx output.
					w.Write($@"

/// <summary>
/// A static resource class, for looking up strings, etc.
/// </summary>
[global::System.Diagnostics.DebuggerNonUserCode()]
[global::System.CodeDom.Compiler.GeneratedCodeAttribute(""{generatorName}"", ""{generatorVersion}"")]
{(isPublic ? "public " : string.Empty)}static partial class {className}
{{
");
					foreach (var file in Directory.EnumerateFiles(folder.ItemSpec, "*", SearchOption.TopDirectoryOnly))
					{
						var name = Path.GetFileNameWithoutExtension(file);


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
    public static readonly string {name} = ""{str}"";
");
					}
					w.WriteLine("}");
				}
			}

			// put the artifacts we created in the output properties.
			OutputCode = outCodeItems.ToArray();
			return !hasError;
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

