using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace jsonparser
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

            TransformJson(filepath);

            Console.WriteLine("Complete");
        }

        private static void TransformJson(string filepath)
        {
            using (var fileStream = File.OpenText(filepath))
            {
                var jsonText = fileStream.ReadToEndAsync().Result;
                var jsonObject = JObject.Parse(jsonText);
                WalkNode(jsonObject, node =>
                {
                    JToken summary = node["summary"];
                    JToken operationId = node["operationId"];
                    if (summary != null && summary.Type == JTokenType.String && operationId != null &&
                        operationId.Type == JTokenType.String)
                    {
                        node["summary"] = $"{operationId.Value<string>()} - {summary.Value<string>()}";
                    }
                });

                using (StreamWriter outputFile = new StreamWriter(filepath.Replace(".json", $"-changed.json")))
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
