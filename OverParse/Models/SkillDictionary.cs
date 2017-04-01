using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace OverParse.Models
{
    public sealed class SkillDictionary
    {
        public enum LanguageEnum
        {
            EN,
            JA,
        }

        public static readonly string SkillCSVName = "skills.csv";

        private static readonly SkillDictionary instance = new SkillDictionary();

        private readonly IDictionary<string, string> dic = new Dictionary<string, string>();
        private bool initialized = false;

        public static SkillDictionary GetInstance() {
            return instance;
        }

        private SkillDictionary() { }

        public bool Initialize(LanguageEnum lang) {
            Console.WriteLine($"Updating {SkillCSVName}");
            string[] lines;
            var errorOccurred = false;
            try {
                var client = new WebClient();
                var stream = client.OpenRead(SkillCSVUrl(lang));
                var webreader = new StreamReader(stream);
                String content = webreader.ReadToEnd();
                File.WriteAllText(SkillCSVName, content);
                lines = content.Split('\n');
            } catch (Exception e) {
                Console.WriteLine($"{SkillCSVName} update failed: {e.ToString()}");
                errorOccurred = true;
                if (File.Exists(SkillCSVName)) {
                    lines = File.ReadAllLines(SkillCSVName);
                } else {
                    lines = new string[0];
                }
            }

            Console.WriteLine($"Parsing {SkillCSVName}");
            foreach (string line in lines) {
                string[] fields = line.Split(',');
                if (fields.Length > 1) {
                    dic.Add(/* ID */ fields[1], /* Type */ fields[0]);
                }
            }
            Console.WriteLine("Keys in skill dict: " + dic.Count());

            initialized = true;
            return !errorOccurred;
        }

        public string Find(string id, string defValue = "Unknown") {
            CheckInitialized();
            if (dic.ContainsKey(id)) {
                return dic[id];
            } else {
                return defValue;
            }
        }

        public bool ContainsKey(string id) {
            CheckInitialized();
            return dic.ContainsKey(id);
        }

        private void CheckInitialized() {
            if (!initialized) {
                throw new InvalidOperationException("SkillDictionary is not initialized.");
            }
        }

        private static string SkillCSVUrl(LanguageEnum lang) {
            switch (lang) {
                case LanguageEnum.JA:
                    return "https://raw.githubusercontent.com/nemomomo/PSO2ACT/master/PSO2ACT/skills.csv";
                default:
                    return "https://raw.githubusercontent.com/nemomomo/PSO2ACT/master/PSO2ACT/skills.csv";
            }
        }
    }
}
