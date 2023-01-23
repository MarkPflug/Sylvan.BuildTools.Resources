
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Sylvan.BuildTools.Resources
{
	static class Culture
	{
		public static bool IsValidCulture(string name)
		{
			return name.Length >= 2;
		}
	}

	public class JsonResourceGenerator : Task
	{
		[Required]
		// The folder where we will write all of our generated code.
		public string OutputPath { get; set; }

		// All of the .resj files in our projct.
		public ITaskItem[] InputFiles { get; set; }

		// Will contain all of the generated coded we create
		[Output]
		public ITaskItem[] OutputCode { get; set; }

		// Will contain all of the resources we generate.
		[Output]
		public ITaskItem[] OutputResources { get; set; }

		bool hasError = false;

		class FileErrorLogger
		{
			JsonResourceGenerator task;
			string file;

			public FileErrorLogger(JsonResourceGenerator task, string file)
			{
				this.task = task;
				this.file = file;
			}

			public bool JsonError(JsonErrorCode error, Location loc)
			{
				task.hasError = true;
				task.Log.LogError(null, "JP" + ((int)error).ToString(), null, file, loc.Line, loc.Column, loc.Line, loc.Column, error.ToString(), null);
				return true;
			}

			public void ParseError(string message, JsonReader reader)
			{
				ParseError(message, reader.Start, reader.End);
			}

			public void ParseError(string message, Location start, Location end)
			{
				task.Log.LogError(null, "JS0001", null, file, start.Line, start.Column, end.Line, end.Column, message, null);
			}

			public void ParseError(string message, JsonNode node)
			{
				ParseError(message, node.Start, node.End);
			}
		}

		const string AccessPublic = "Public";
		const string AccessInternal = "Internal";
		const string AccessNoCodeGen = "NoCodeGen";

		// The method that is called to invoke our task.
		public override bool Execute()
		{
			if (InputFiles == null)
				return true;

			var outCodeItems = new List<TaskItem>();
			var outResItems = new List<TaskItem>();
			string generatorName = typeof(JsonResourceGenerator).FullName;
			string generatorVersion = typeof(JsonResourceGenerator).GetTypeInfo().Assembly.GetName().Version.ToString();


			// loop over all the .resj files we were given
			foreach (var iFile in InputFiles)
			{
				var fn = Path.GetFileNameWithoutExtension(iFile.ItemSpec);

				var culturePart = Path.GetExtension(fn);
				var hasCulture = false;
				if (culturePart?.Length > 1)
				{
					culturePart = culturePart.Substring(1);
					hasCulture = Culture.IsValidCulture(culturePart);
				}

				var fileDir = Path.GetDirectoryName(iFile.ItemSpec);
				var filePath = iFile.ItemSpec;

				var logger = new FileErrorLogger(this, filePath);

				// load the Json from the file
				var text = File.ReadAllText(filePath);
				var json = new JsonReader(new StringReader(text), logger.JsonError);
				var doc = JsonDocument.Load(json);

				if (doc.RootNode == null)
				{
					logger.ParseError("Failed to parse json text.", doc);
					continue;
				}

				if (doc.RootNode.NodeType != JsonNodeType.Object)
				{
					logger.ParseError("Expected object as root node.", doc.RootNode);
					continue;
				}

				var root = (JsonObject)doc.RootNode;

				var resName = iFile.GetMetadata("ResourceName");

				if (string.IsNullOrEmpty(resName))
				{
					resName = Path.GetFileNameWithoutExtension(iFile.ItemSpec);
				}

				var resourceName = resName + ".resources";

				Directory.CreateDirectory(OutputPath);

				// write the generated C# code and resources file.
				if (!hasCulture)
				{
					var codeFile = Path.Combine(OutputPath, iFile.ItemSpec + ".g.cs");
					var access = iFile.GetMetadata("AccessModifier");

					var noCodeGen = StringComparer.OrdinalIgnoreCase.Equals(access, AccessNoCodeGen);

					if (!noCodeGen)
					{
						var isPublic = StringComparer.OrdinalIgnoreCase.Equals(access, AccessPublic);

						var ns = iFile.GetMetadata("Namespace");

						string className = Path.GetFileNameWithoutExtension(iFile.ItemSpec);
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
							w.Write($@"using global::System.Reflection;

/// <summary>
/// A strongly-typed resource class, for looking up localized strings, etc.
/// </summary>
[global::System.Diagnostics.DebuggerNonUserCode()]
[global::System.CodeDom.Compiler.GeneratedCodeAttribute(""{generatorName}"", ""{generatorVersion}"")]
{(isPublic ? "public " : string.Empty)}static partial class {className}
{{
    static global::System.Resources.ResourceManager rm;
    static global::System.Globalization.CultureInfo resourceCulture;

    static global::System.Resources.ResourceManager ResourceManager
    {{
        get
        {{
            if (object.ReferenceEquals(rm, null))
            {{
                rm = new global::System.Resources.ResourceManager(""{resName}"", typeof({className}).GetTypeInfo().Assembly);
            }}

            return rm;
        }}
    }}

    static global::System.Globalization.CultureInfo Culture
    {{
        get
        {{
            return resourceCulture;
        }}
        set
        {{
            resourceCulture = value;
        }}
    }}
");

							foreach (var section in root)
							{
								var value = section.Value;
								var sectionName = section.Key.Value;

								switch (sectionName)
								{
									case "Strings":
									case "Files":
										if (value.NodeType == JsonNodeType.Object)
										{
											var obj = (JsonObject)value;
											foreach (var item in obj)
											{
												var identifier = item.Key.Value;

												if (!Regex.IsMatch(identifier, @"^\w(\w|\d)*$"))
												{
													logger.ParseError("Resource key must be valid C# identifier.", item.Key);
													// don't emit any broken code.
													continue;
												}

												string comment = null;
												if (sectionName == "Strings")
												{
													// https://stackoverflow.com/a/19498780
													string stringValue = new XText(((JsonString)item.Value).Value).ToString();
													comment = $"Looks up a localized string similar to {stringValue.Substring(0, Math.Min(stringValue.Length, 100))}";
												}
												else if (sectionName == "Files")
												{
													comment = $"Looks up a {item.Key} text file";
												}

												if (comment != null)
												{
													IEnumerable<string> commentLines = comment.Split(new[] { "\r", "\n", "\r\n" }, StringSplitOptions.None);
													w.Write($@"
    /// <summary>
    /// {string.Join(Environment.NewLine + "    /// ", commentLines)}.
    /// </summary>");
												}

												w.Write($@"
    public static string {item.Key.Value}
    {{
        get
        {{
            return ResourceManager.GetString(""{item.Key.Value}"", resourceCulture);
        }}
    }}
");
											}
										}
										else
										{
											//logger.ParseError("Expected Json object.", value);
										}

										break;
									default:
										//logger.ParseError("Unexpected property.", value);
										break;
								}
							}

							w.WriteLine("}");

							if (!string.IsNullOrEmpty(ns))
							{
								w.WriteLine();
								w.WriteLine("}");
							}
						}
					}
				}

				// prepare the generated files we are about to write.
				var resFile = Path.Combine(OutputPath, resourceName);

				var resItem = new TaskItem(resFile);
				resItem.SetMetadata("LogicalName", resName + ".resources");
				resItem.SetMetadata("ManifestResourceName", resourceName);
				resItem.SetMetadata("OutputResource", resourceName);

				resItem.SetMetadata("WithCulture", hasCulture ? "true" : "false");
				resItem.SetMetadata("Type", "Non-Resx");

				outResItems.Add(resItem);
				if (hasCulture)
				{
					resItem.SetMetadata("Culture", culturePart);
				}

				json = new JsonReader(new StringReader(text), logger.JsonError);
				using (var rw = new System.Resources.ResourceWriter(resFile))
				{
					foreach (var section in root)
					{
						var sectionName = section.Key.Value;
						var sectionNode = section.Value;
						var isFile = false;
						switch (sectionName)
						{
							case "Strings":
								isFile = false;
								break;
							case "Files":
								isFile = true;
								break;
							default:
								logger.ParseError("Unexpected section.", sectionNode);
								continue;
						}

						if (sectionNode.NodeType != JsonNodeType.Object)
						{
							logger.ParseError("Expected json object", sectionNode);
							continue;
						}
						var sectionObj = (JsonObject)sectionNode;


						foreach (var item in sectionObj)
						{
							var str = item.Value;

							if (str.NodeType != JsonNodeType.String)
							{
								logger.ParseError("Expected string value", str);
								continue;
							}

							var strVal = (JsonString)str;

							string txt;
							if (isFile)
							{
								var textFilePath = Path.Combine(fileDir, strVal.Value);
								if (!File.Exists(textFilePath))
								{
									logger.ParseError("Resource file '" + strVal.Value + "' not found.", str);
									continue;
								}
								try
								{
									using (var iStream = File.OpenRead(textFilePath))
									using (var reader = new StreamReader(iStream))
									{
										txt = reader.ReadToEnd();
									}
								}
								catch (Exception e)
								{
									logger.ParseError(e.Message, str);
									continue;
								}
							}
							else
							{
								txt = strVal.Value;
							}

							// write our string to the resources binary
							rw.AddResource(item.Key.Value, txt);
						}
					}
				}
			}

			// put the artifacts we created in the output properties.
			OutputCode = outCodeItems.ToArray();
			OutputResources = outResItems.ToArray();
			return !hasError;
		}
	}
}
