using Elemental.Json;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Elemental.JsonResource
{
	static class Culture
	{
		static HashSet<string> cultures;
		static object sync = new object();

		public static bool IsValidCulture(string name)
		{
#if NETSTANDARD1_6
            return name.Length >= 2;
#else
			if (cultures == null)
			{
				lock (sync)
				{
					if (cultures == null)
					{
						cultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
						var tags = CultureInfo.GetCultures(CultureTypes.AllCultures).Select(c => c.IetfLanguageTag);
						foreach (var tag in tags)
						{
							cultures.Add(tag);
						}
					}
				}
			}
			return cultures.Contains(name);
#endif
		}
	}


	public class JsonResourceGenerator : Task
	{
		[Required]
		// The folder where we will write all of our generated code.
		public String OutputPath { get; set; }

		// All of the .resj files in our projct.
		public ITaskItem[] InputFiles { get; set; }

		// Will contain all of the generated coded we create
		[Output]
		public ITaskItem[] OutputCode { get; set; }

		// Will contain all of the resources we generate.
		[Output]
		public ITaskItem[] OutputResources { get; set; }

		enum Section
		{
			None = 0,
			Strings,
			Files
		}

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
				task.Log.LogError(null, "JP" + ((int) error).ToString(), null, file, loc.Line, loc.Column, loc.Line, loc.Column, error.ToString(), null);
				return true;
			}

			public void ParseError(string message, JsonReader reader)
			{
				task.Log.LogError(null, "JS0001", null, file, reader.Start.Line, reader.Start.Column, reader.End.Line, reader.End.Column, message, null);
			}
		}



		// The method that is called to invoke our task.
		public override bool Execute()
		{
			if (InputFiles == null)
				return true;

			var outCodeItems = new List<TaskItem>();
			var outResItems = new List<TaskItem>();


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

					var ns = iFile.GetMetadata("Namespace");

					string className = Path.GetFileNameWithoutExtension(iFile.ItemSpec);
					outCodeItems.Add(new TaskItem(codeFile));
					Directory.CreateDirectory(Path.GetDirectoryName(codeFile));




					using (var oStream = new FileStream(codeFile, FileMode.Create))
					using (var w = new StreamWriter(oStream))
					{
						if (!string.IsNullOrEmpty(ns))
						{
							w.WriteLine("namespace " + ns + " {");
						}

						w.WriteLine("using global::System.Reflection;");
						// very simplistic resource accessor class mostly duplicated from resx output.
						w.WriteLine("public static partial class " + className + " { ");
						w.WriteLine("static global::System.Resources.ResourceManager rm;");
						w.WriteLine("static global::System.Globalization.CultureInfo resourceCulture;");
						w.WriteLine("static global::System.Resources.ResourceManager ResourceManager {");
						w.WriteLine("get {");
						w.WriteLine("if(object.ReferenceEquals(rm, null)) {");
						w.WriteLine("rm = new global::System.Resources.ResourceManager(\"" + resName + "\", typeof(" + className + ").GetTypeInfo().Assembly);");
						w.WriteLine("}");
						w.WriteLine("return rm;");
						w.WriteLine("}");
						w.WriteLine("}");
						w.WriteLine("static global::System.Globalization.CultureInfo Culture {");
						w.WriteLine("get {");
						w.WriteLine("return resourceCulture;");
						w.WriteLine("}");
						w.WriteLine("set {");
						w.WriteLine("resourceCulture = value;");
						w.WriteLine("}");
						w.WriteLine("}");

						if (!json.Read() || json.SyntaxKind != SyntaxKind.ObjectStart)
							Log.LogError(filePath, "Expected root object element.", json);

						Section section = Section.None;

						while (json.Read())
						{
							switch (section)
							{
							case Section.None:
								switch (json.SyntaxKind)
								{
								case SyntaxKind.PropertyName:
									var name = json.PropertyName;
									var ln = name.ToLower();

									switch (ln)
									{
									case "strings":
										section = Section.Strings;
										if (!json.Read())
										{
											logger.ParseError("Expected end of file.", json);
											goto end;
										}
										if (json.SyntaxKind != SyntaxKind.ObjectStart)
										{
											logger.ParseError("Expected object start", json);
											goto end;
										}
										break;
									case "files":
										section = Section.Files;
										if (!json.Read())
										{
											logger.ParseError("Expected end of file.", json);
											goto end;
										}
										if (json.SyntaxKind != SyntaxKind.ObjectStart)
										{
											logger.ParseError("Expected object start", json);
											goto end;
										}
										break;
									default:
										logger.ParseError("Unexpected section " + name, json);
										goto end;
									}
									break;
								case SyntaxKind.ObjectEnd:
									if (json.Read())
									{
										logger.ParseError("Expected end of file.", json);
									}
									break;
								default:
									logger.ParseError("Unexpected syntax " + json.SyntaxKind.ToString(), json);
									goto end;
								}
								break;
							case Section.Strings:
							case Section.Files:
								switch (json.SyntaxKind)
								{
								case SyntaxKind.PropertyName:
									var name = json.PropertyName;
									if (!json.Read())
									{
										logger.ParseError("Unexpected end of file.", json);
									}
									if (json.SyntaxKind == SyntaxKind.StringValue)
									{
										w.WriteLine("public static string " + name + " {");
										w.WriteLine("get {");
										w.WriteLine("return ResourceManager.GetString(\"" + name + "\", resourceCulture);");
										w.WriteLine("}");
										w.WriteLine("}");
									}
									else
									{
										logger.ParseError("Unexpected syntax " + json.SyntaxKind.ToString(), json);
										break;
									}

									break;
								case SyntaxKind.ObjectEnd:
									section = Section.None;
									break;
								default:
									logger.ParseError("Unexpected syntax " + json.SyntaxKind.ToString(), json);
									break;
								}
								break;
							}
						}
						end:

						w.WriteLine("}");

						if (!string.IsNullOrEmpty(ns))
						{
							w.WriteLine("}");
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
				else
				{

				}

				json = new JsonReader(new StringReader(text), logger.JsonError);
				using (var rw = new System.Resources.ResourceWriter(resFile))
				{
					if (!json.Read() || json.SyntaxKind != SyntaxKind.ObjectStart)
						Log.LogError(filePath, "Expected root object element.", json);

					Section section = Section.None;

					while (json.Read())
					{
						switch (section)
						{
						case Section.None:
							switch (json.SyntaxKind)
							{
							case SyntaxKind.PropertyName:
								var name = json.PropertyName;
								var ln = name.ToLower();

								switch (ln)
								{
								case "strings":
									section = Section.Strings;
									if (!json.Read())
									{
										logger.ParseError("Expected end of file.", json);
										goto end;
									}
									if (json.SyntaxKind != SyntaxKind.ObjectStart)
									{
										logger.ParseError("Expected object start", json);
										goto end;
									}
									break;
								case "files":
									section = Section.Files;
									if (!json.Read())
									{
										logger.ParseError("Expected end of file.", json);
										goto end;
									}
									if (json.SyntaxKind != SyntaxKind.ObjectStart)
									{
										logger.ParseError("Expected object start", json);
										goto end;
									}
									break;
								default:
									logger.ParseError("Unexpected section " + name, json);
									goto end;
								}
								break;
							case SyntaxKind.ObjectEnd:
								if (json.Read())
								{
									logger.ParseError("Expected end of file.", json);
								}
								break;
							default:
								logger.ParseError("Unexpected syntax " + json.SyntaxKind.ToString(), json);
								goto end;
							}
							break;
						case Section.Strings:
						case Section.Files:
							switch (json.SyntaxKind)
							{
							case SyntaxKind.PropertyName:
								var name = json.PropertyName;
								if (!json.Read())
								{
									logger.ParseError("Unexpected end of file.", json);
								}
								if (json.SyntaxKind == SyntaxKind.StringValue)
								{
									// loop over all the strings in our resj file.
									if (section == Section.Strings)
									{
										// write our string to the resources binary
										rw.AddResource(name, json.StringValue);
									}
									else
									if (section == Section.Files)
									{

										var textFilePath = Path.Combine(fileDir, name);
										if (!File.Exists(textFilePath))
										{
											logger.ParseError("File not found.", json);
										}

										using (var iStream = File.OpenRead(textFilePath))
										using (var reader = new StreamReader(iStream))
										{
											var txt = reader.ReadToEnd();
											// write our string to the resources binary
											rw.AddResource(name, txt);
										}
									}
								}
								else
								{
									logger.ParseError("Unexpected syntax " + json.SyntaxKind.ToString(), json);
									break;
								}

								break;
							case SyntaxKind.ObjectEnd:
								section = Section.None;
								break;
							default:
								logger.ParseError("Unexpected syntax " + json.SyntaxKind.ToString(), json);
								break;
							}
							break;
						}
					}
					end:;
				}
			}

			// put the artifacts we created in the output properties.
			OutputCode = outCodeItems.ToArray();
			OutputResources = outResItems.ToArray();
			return !hasError;
		}
	}
}

