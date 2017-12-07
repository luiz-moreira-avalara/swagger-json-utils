using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Swagger.Json.Utils
{
    class Program
    {
        static void Main(string[] args)
        {
            if (!args.Any())
            {
                Console.WriteLine("Couldn't find a file. Ensure a file exists");
                Console.Read();
                return;
            }

            var filepath = Path.Combine(Environment.CurrentDirectory, args.First());
            Directory.GetParent(filepath).CreateSubdirectory("paths");

            TransformJson(filepath, Directory.GetParent(filepath));

            Console.WriteLine("Complete");
        }

        private static void TransformJson(string filepath, DirectoryInfo directory)
        {
            void ChangeSummary(JObject node)
            {
                JToken summary = node["summary"];
                JToken operationId = node["operationId"];
                if (summary != null && summary.Type == JTokenType.String && operationId != null &&
                    operationId.Type == JTokenType.String)
                {
                    node["summary"] = $"{operationId.Value<string>()} - {summary.Value<string>()}";
                }
            }

            void SplitPaths(JObject node, string changedFilename)
            {
                var paths = node["paths"];

                if (paths == null)
                    return;

                var baseFilePath= $@"{directory.FullName}\paths";
                var childs = paths.Children<JProperty>();

                foreach (var child in childs)
                {
                    foreach (var verb in child.Children<JObject>())
                    {
                        ChangeSummary(verb);
                    }

                    var filename = $"{ child.Name.Split("/").Last() }.json";
                    var content = child.Value.ToString(Formatting.None).Replace("#", $@"../{changedFilename}#");
                    File.WriteAllText(Path.Combine(baseFilePath, filename) , content, Encoding.UTF8);

                    child.Value = JToken.Parse("{ \"$ref\": \"paths/" + filename +"\" }");
                }
            }


            using (var fileStream = File.OpenText(filepath))
            {
                var jsonText = fileStream.ReadToEndAsync().Result;
                var jsonObject = JObject.Parse(jsonText);
                var path = filepath.Replace(".json", $"-changed.json");
                var changedFileName = path.Split(@"\").Last();
                WalkNode(jsonObject, node =>
                {
                    ChangeSummary(node);
                    SplitPaths(node, changedFileName);
                });

                using (StreamWriter outputFile = new StreamWriter(path))
                {
                    outputFile.WriteLine(JsonConvert.SerializeObject(jsonObject, new JsonSerializerSettings
                    {
                        //NullValueHandling = NullValueHandling.Ignore
                    }));
                }
            }
        }

        static void WalkNode(JToken node, Action<JObject> action)
        {
            if (node.Type == JTokenType.Object)
            {
                action((JObject)node);

                foreach (JProperty child in node.Children<JProperty>())
                {
                    WalkNode(child.Value, action);
                }
            }
            else if (node.Type == JTokenType.Array)
            {
                foreach (JToken child in node.Children())
                {
                    WalkNode(child, action);
                }
            }
        }
    }

}
