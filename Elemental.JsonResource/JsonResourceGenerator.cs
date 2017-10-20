using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json.Linq;
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
            return true;
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

                // load the Json from the file
                var text = File.ReadAllText(iFile.ItemSpec);
                var obj = JObject.Parse(text);

                string resName = Path.GetFileNameWithoutExtension(iFile.ItemSpec);
                var resourceName = resName + ".resources";

                Directory.CreateDirectory(OutputPath);

                

                // write the generated C# code and resources file.
                if (!hasCulture)
                {
                    var codeFile = Path.Combine(OutputPath, iFile.ItemSpec + ".g.cs");

                    string className = Path.GetFileNameWithoutExtension(iFile.ItemSpec);
                    outCodeItems.Add(new TaskItem(codeFile));
                    using(var oStream = new FileStream(codeFile, FileMode.Create))
                    using (var w = new StreamWriter(oStream))
                    {
                        // very simplistic resource accessor class mostly duplicated from resx output.
                        w.WriteLine("public static partial class " + className + " { ");
                        w.WriteLine("static global::System.Resources.ResourceManager rm;");
                        w.WriteLine("static global::System.Globalization.CultureInfo resourceCulture;");
                        w.WriteLine("static global::System.Resources.ResourceManager ResourceManager {");
                        w.WriteLine("get {");
                        w.WriteLine("if(object.ReferenceEquals(rm, null)) {");
                        w.WriteLine("rm = new global::System.Resources.ResourceManager(\"" + resName + "\", typeof(" + className + ").Assembly);");
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

                        // loop over all the strings in our resj file.
                        foreach (var kvp in (JObject)obj["Strings"])
                        {
                            var key = kvp.Key;
                            var value = (JValue)kvp.Value;

                            // generate a C# property to access the string by name.
                            w.WriteLine("public static string " + key + " {");
                            w.WriteLine("get {");
                            w.WriteLine("return ResourceManager.GetString(\"" + key + "\", resourceCulture);");
                            w.WriteLine("}");
                            w.WriteLine("}");
                        }

                        var textFiles = (JObject)obj["TextFiles"];
                        // loop over all the strings in our resj file.
                        if (textFiles != null)
                        {
                            foreach (var kvp in textFiles)
                            {
                                var key = kvp.Key;
                                var fileName = (string)((JValue)kvp.Value).Value;
                                Path.Combine(fileDir, fileName);

                                // generate a C# property to access the string by name.
                                w.WriteLine("public static string " + key + " {");
                                w.WriteLine("get {");
                                w.WriteLine("return ResourceManager.GetString(\"" + key + "\", resourceCulture);");
                                w.WriteLine("}");
                                w.WriteLine("}");
                            }
                        }

                        w.WriteLine("}");
                    }
                }


                // prepare the generated files we are about to write.
                var resFile = Path.Combine(OutputPath, resourceName);

                var resItem = new TaskItem(resFile);
                resItem.SetMetadata("LogicalName", resourceName);
                resItem.SetMetadata("ManifestResourceName", resourceName);
                resItem.SetMetadata("OutputResource", resourceName);

                resItem.SetMetadata("WithCulture", hasCulture ? "true" : "false");
                resItem.SetMetadata("Type", "Non-Resx");
                if (hasCulture)
                {
                    resItem.SetMetadata("Culture", culturePart);
                }
                else
                {

                }


                using (var rw = new System.Resources.ResourceWriter(resFile))
                {
                    // loop over all the strings in our resj file.
                    foreach (var kvp in (JObject)obj["Strings"])
                    {
                        var key = kvp.Key;
                        var value = (JValue)kvp.Value;

                        // write our string to the resources binary
                        rw.AddResource(key, (string)value.Value);
                    }

                    var textFiles = (JObject)obj["TextFiles"];
                    // loop over all the strings in our resj file.
                    if (textFiles != null)
                    {
                        foreach (var kvp in textFiles)
                        {
                            var key = kvp.Key;
                            var fileName = (string)((JValue)kvp.Value).Value;
                            Path.Combine(fileDir, fileName);
                            if (!File.Exists(fileName))
                            {
                                Log.LogError("Resource file not found: " + fileName);
                            }

                            using (var iStream = File.OpenRead(fileName))
                            using (var reader = new StreamReader(iStream))
                            {
                                var txt = reader.ReadToEnd();
                                // write our string to the resources binary
                                rw.AddResource(key, txt);
                            }
                        }
                    }

                    if (hasCulture)
                    {

                    }
                    outResItems.Add(resItem);
                }
            }
            // put the artifacts we created in the output properties.
            OutputCode = outCodeItems.ToArray();
            OutputResources = outResItems.ToArray();
            return true;
        }
    }
}
