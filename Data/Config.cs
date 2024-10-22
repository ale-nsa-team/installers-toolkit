using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PoEWizard.Data
{
    public class Config
    {
        private Dictionary<string, string> dict;
        private readonly string filepath;
        private bool hasChanges = false;

        public Config(string filepath)
        {
            this.filepath = filepath;
            LoadData();
        }

        public string Get(string field, string defaultValue)
        {
            return Get(field) ?? defaultValue;
        }

        public string Get(string field)
        {
            return dict.ContainsKey(field) ? dict[field] : null;
        }

        public bool GetBool(string field, bool defaultValue)
        {
            string val = Get(field, defaultValue.ToString());
            return bool.TryParse(val, out bool b) ? b : defaultValue;
        }

        public int GetInt(string field, int defaultValue)
        {
            string val = Get(field, defaultValue.ToString());
            return int.TryParse(val, out int i) ? i : defaultValue;
        }

        public void Set(string field, object value)
        {
            dict[field] = value.ToString();
            hasChanges = true;
        }

        public Dictionary<string, string> GetAll()
        {
            return dict;
        }

        public void Save()
        {
            if (!hasChanges) return;

            StreamWriter file = null;
            try
            {
                if (!File.Exists(filepath))
                {
                    File.Create(filepath);
                }

                file = new StreamWriter(filepath, false);

                foreach (string prop in dict.Keys.ToArray())
                {
                    if (!string.IsNullOrWhiteSpace(dict[prop]))
                    {
                        file.WriteLine($"{prop}={dict[prop]}");
                    }
                    else
                    {
                        file.WriteLine($"{prop}={string.Empty}");
                    }
                }
                hasChanges = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not save properties file\n{ex.StackTrace}");
            }
            finally
            {
                file?.Close();
            }
        }

        public void LogParameters()
        {
            if (dict.Count > 0)
            {
                StringBuilder sb = new StringBuilder("Configuration parameters:");
                foreach (KeyValuePair<string, string> entry in dict)
                {
                    sb.Append("\n\t").Append(entry.Key).Append(": ").Append(entry.Value);
                }
                Logger.Info(sb.ToString());
            }
        }

        private void LoadData()
        {
            dict = new Dictionary<string, string>();

            {
                if (File.Exists(filepath))
                {
                    LoadFromFile(filepath);
                }
                else
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(filepath);
                        Directory.CreateDirectory(dir);
                        FileStream file = File.Create(filepath);
                        file.Close();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not create properties file\n{ex.StackTrace}");
                    }
                }
            }
        }

        private void LoadFromFile(string file)
        {
            string[] lines;

            try
            {
                using (StreamReader reader = File.OpenText(file))
                {
                    string text = reader.ReadToEnd();
                    lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                }
            }
            catch
            {
                lines = Array.Empty<string>();
            }
            foreach (string line in lines)
            {
                if (!string.IsNullOrEmpty(line) && !line.StartsWith(";") && !line.StartsWith("#") && !line.StartsWith("'") && line.Contains('='))
                {
                    int index = line.IndexOf('=');
                    string key = line.Substring(0, index).Trim();
                    string value = line.Substring(index + 1).Trim();

                    if ((value.StartsWith("\"") && value.EndsWith("\"")) || (value.StartsWith("'") && value.EndsWith("'")))
                    {
                        value = value.Substring(1, value.Length - 2);
                    }
                    dict[key] = value;
                }
            }
        }
    }
}
