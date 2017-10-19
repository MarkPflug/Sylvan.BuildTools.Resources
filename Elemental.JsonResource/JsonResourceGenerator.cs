using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;

namespace Elemental.JsonResource
{

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
                var fileDir = Path.GetDirectoryName(iFile.ItemSpec);
                Log.LogMessage("RelativePath: " + fileDir);
                // load the Json from the file
                var text = File.ReadAllText(iFile.ItemSpec);
                var obj = JObject.Parse(text);

                // prepare the generated files we are about to write.
                var codeFile = Path.Combine(OutputPath, iFile.ItemSpec + ".g.cs");
                var resFile = Path.Combine(OutputPath, iFile.ItemSpec + ".resources");

                outCodeItems.Add(new TaskItem(codeFile));
                
                var resItem = new TaskItem(resFile);
                resItem.SetMetadata("LogicalName", iFile.ItemSpec + ".resources");
                outResItems.Add(resItem);

                Directory.CreateDirectory(Path.GetDirectoryName(codeFile));
                string resName = Path.GetFileName(iFile.ItemSpec);
                string className = Path.GetFileNameWithoutExtension(iFile.ItemSpec);

                // write the generated C# code and resources file.
                using (var w = new StreamWriter(codeFile))
                using (var rw = new System.Resources.ResourceWriter(resFile))
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

                        // write our string to the resources binary
                        rw.AddResource(key, (string)value.Value);

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
            // put the artifacts we created in the output properties.
            OutputCode = outCodeItems.ToArray();
            OutputResources = outResItems.ToArray();
            return true;
        }
    }
}
