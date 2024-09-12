﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static PoEWizard.Data.Constants;

namespace PoEWizard.Data
{
    public static class CliParseUtils
    {
        private static readonly Regex vtableRegex = new Regex(MATCH_COLON);
        private static readonly Regex etableRegex = new Regex(MATCH_EQUALS);
        private static readonly Regex htableRegex = new Regex(MATCH_TABLE_SEP);
        private static readonly Regex chassisRegex = new Regex(MATCH_CHASSIS);
        private static readonly Regex cmmRegex = new Regex(MATCH_CMM);
        private static readonly Regex userRegex = new Regex(MATCH_USER);

        public static List<Dictionary<string, string>> ParseListFromDictionary(Dictionary<string, string> inputDict, string match = null)
        {
            List<Dictionary<string, string>> dictList = new List<Dictionary<string, string>>();
            int idx = 0;
            string lastIdx = "";
            dictList.Add(new Dictionary<string, string>());
            foreach (var keyVal in inputDict)
            {
                string key = keyVal.Key;
                if (!string.IsNullOrEmpty(match) && !key.Contains(match)) continue;
                if (key.Contains($"_"))
                {
                    string id = Utils.ExtractNumber(key);
                    if (lastIdx != $"_{id}")
                    {
                        dictList.Add(new Dictionary<string, string>());
                        lastIdx = $"_{id}";
                        idx++;
                    }
                    if (!string.IsNullOrEmpty(id)) key = key.Replace($"_{id}", "");
                }
                dictList[idx][key] = keyVal.Value;
            }
            return dictList;
        }

        public static Dictionary<string, string> ParseXmlToDictionary(string xml)
        {
            XDocument doc = XDocument.Parse(xml);
            Dictionary<string, string> dict = new Dictionary<string, string>();
            foreach (XElement element in doc.Descendants().Where(p => p.HasElements == false))
            {
                int keyInt = 0;
                string keyName = element.Name.LocalName;
                while (dict.ContainsKey(keyName))
                {
                    keyName = element.Name.LocalName + "_" + keyInt++;
                }
                dict.Add(keyName, element.Value);
                if (element.Parent?.FirstAttribute?.Value != null)
                {
                    string nodeName = element.Parent.FirstAttribute.Value;
                    if (!dict.ContainsKey("node_" + nodeName)) dict.Add("node_" + nodeName, nodeName);
                }
            }
            return dict;
        }

        public static Dictionary<string, string> ParseVTable(string data)
        {
            return ParseTable(data, vtableRegex);
        }

        public static Dictionary<string, string> ParseETable(string data)
        {
            return ParseTable(data, etableRegex);
        }

        public static List<Dictionary<string, string>> ParseHTable(string data, int nbHeaders = 1)
        {
            List<Dictionary<string, string>> table = new List<Dictionary<string, string>>();
            string[] lines = Regex.Split(data, @"\r\n\r|\n");
            Match match = htableRegex.Match(data);
            if (match.Success)
            {
                int line = LineNumberFromPosition(data, match.Index);
                string[] header = new string[match.Value.Count(c => c == '+') + 1];
                for (int i = 1; i <= nbHeaders; i++)
                {
                    string[] hd = GetValues(lines[line], lines[line - i]);
                    header = header.Zip(hd, (a, b) => b.EndsWith("/") ? $"{b}{a}" : $"{b} {a}").ToArray();
                }

                for (int i = line + 1; i < lines.Length; i++)
                {
                    if (lines[i] == string.Empty) break;
                    Dictionary<string, string> dict = new Dictionary<string, string>();
                    string[] values = GetValues(lines[line], lines[i]);
                    for (int j = 0; j < header.Length; j++)
                    {
                        dict.Add(header[j].Replace("|", "").Trim(), values?.Skip(j).FirstOrDefault());
                    }
                    table.Add(dict);
                }
            }

            return table;
        }

        public static List<Dictionary<string, string>> ParseMultipleTables(string data, DictionaryType dtype)
        {
            List<Dictionary<string, string>> table = new List<Dictionary<string, string>>();
            Regex headerRegex;
            Regex bodyRegex;
            string[] headerKeys; 
            switch (dtype)
            {
                case DictionaryType.Cmm:
                    headerRegex = cmmRegex;
                    bodyRegex = vtableRegex;
                    headerKeys = new string[] { "ID" };
                    break;

                case DictionaryType.Chassis:
                    headerRegex = chassisRegex;
                    bodyRegex = vtableRegex;
                    headerKeys = new string[] { "ID", "Role" };
                    break;

                case DictionaryType.User:
                    headerRegex = userRegex;
                    bodyRegex = etableRegex;
                    headerKeys = new string[] { "User name" };
                    break; ;

                default:
                    return table;
            }
            using (StringReader reader = new StringReader(data))
            {
                string line;
                Dictionary<string, string> dict = new Dictionary<string, string>();

                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Trim().Length == 0) continue;
                    Match match = headerRegex.Match(line);
                    if (match.Success)
                    {
                        if (dict.Count > 0) table.Add(dict);
                        dict = new Dictionary<string, string>();
                        int i = 2;
                        foreach (string key in headerKeys)
                        {
                            dict[key] = match.Groups[i++].Value;
                        }
                    }
                    else if ((match = bodyRegex.Match(line)).Success)
                    {
                        string key = match.Groups[1].Value.Trim();
                        if (key.StartsWith(FPGA)) key = FPGA; else if (key.StartsWith(CPLD)) key = CPLD;
                        string value = match.Groups[2].Value.Trim();
                        value = value.EndsWith(",") ? value.Substring(0, value.Length - 1) : value;
                        dict[key] = value;
                    }
                }
                table.Add(dict);
            }
            return table;
        }

        public static Dictionary<string, List<Dictionary<string, string>>> ParseLldpRemoteTable(string data)
        {
            Dictionary<string, List<Dictionary<string, string>>> dictList = new Dictionary<string, List<Dictionary<string, string>>>();
            string[] split;
            string currPort = string.Empty;
            using (StringReader reader = new StringReader(data))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Trim().Length == 0) continue;
                    if (line.Contains("Local Port "))
                    {
                        currPort = Utils.ExtractSubString(line.Trim(), "Local Port ", ":");
                        dictList[currPort] = new List<Dictionary<string, string>>();
                    }
                    else if (line.Contains("Chassis ") && !line.Contains("Subtype") && line.Contains(","))
                    {
                        split = line.Trim().Split(',');
                        if (split.Length == 2)
                        {
                            dictList[currPort].Add(new Dictionary<string, string>());
                            dictList[currPort][dictList[currPort].Count - 1] = new Dictionary<string, string>
                            {
                                ["Local Port"] = currPort,
                                [CHASSIS_MAC_ADDRESS] = Utils.ExtractSubString(split[0].Trim(), "Chassis "),
                                [REMOTE_PORT] = Utils.ExtractSubString(split[1].Trim(), "Port ")
                            };
                        }
                    }
                    else
                    {
                        split = line.Trim().Split('=');
                        if (split.Length == 2)
                        {
                            dictList[currPort][dictList[currPort].Count - 1][split[0].Trim()] = split[1].Replace(",", "").Replace("\"", "").Trim();
                        }
                        else if (line.Contains(REMOTE_ID))
                        {
                            split = line.Trim().Split(' ');
                            if (split.Length > 3) dictList[currPort][dictList[currPort].Count - 1][REMOTE_ID] = split[3].Trim().Replace(":", "");
                        }
                    }
                }
            }
            return dictList;
        }

        public static List<Dictionary<string, string>> ParseSwitchDebugAppTable(Dictionary<string, string> dataList, string[] appNameList)
        {
            List<Dictionary<string, string>> dictList = ParseListFromDictionary(dataList);
            List<Dictionary<string, string>> appList = new List<Dictionary<string, string>>();
            if (dictList?.Count > 0)
            {
                string appIndex = null;
                string appId = null;
                string appName = null;
                int appCnt = 0;
                foreach (Dictionary<string, string> dict in dictList)
                {
                    foreach (KeyValuePair<string, string> keyVal in dict)
                    {
                        if (keyVal.Key != DEBUG_APP_ID && keyVal.Key != DEBUG_APP_NAME && !keyVal.Key.Contains("node_")) continue;
                        string val = Utils.GetDictValue(dict, DEBUG_APP_ID);
                        if (string.IsNullOrEmpty(appId) && !string.IsNullOrEmpty(val)) appId = val;
                        val = Utils.GetDictValue(dict, DEBUG_APP_NAME);
                        if (string.IsNullOrEmpty(appName) && !string.IsNullOrEmpty(val)) appName = val;
                        if (string.IsNullOrEmpty(appIndex) && keyVal.Key.Contains("node_")) appIndex = keyVal.Value;
                        if (!string.IsNullOrEmpty(appId) && !string.IsNullOrEmpty(appName) && !string.IsNullOrEmpty(appIndex))
                        {
                            bool appFound = true;
                            if (appNameList?.Length > 0)
                            {
                                appFound = false;
                                foreach (string name in appNameList)
                                {
                                    if (appName == name)
                                    {
                                        appFound = true;
                                        break;
                                    }
                                }
                            }
                            if (appFound)
                            {
                                Dictionary<string, string> app = new Dictionary<string, string> { [DEBUG_APP_ID] = appId, [DEBUG_APP_NAME] = appName, [DEBUG_APP_INDEX] = appIndex };
                                appList.Add(app);
                                appCnt++;
                            }
                            appIndex = null;
                            appId = null;
                            appName = null;
                            break;
                        }
                        if (appCnt >= appNameList.Length) break;
                    }
                }
            }
            return appList;
        }

        public static List<Dictionary<string, string>> ParseCliSwitchDebugLevel(string data)
        {
            List<Dictionary<string, string>> table = new List<Dictionary<string, string>>();
            using (StringReader reader = new StringReader(data))
            {
                string appName = string.Empty;
                string appId = string.Empty;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Trim().Length == 0) continue;
                    if (line.Contains(DEBUG_CLI_APP_NAME))
                    {
                        string[] split = line.Trim().Split(':');
                        if (split.Length == 2)
                        {
                            appName = split[1].Trim().Replace(",", "");
                            split = appName.Split('(');
                            if (split.Length == 2)
                            {
                                appName = split[0].Trim();
                                appId = split[1].Replace(")", "").Trim();
                            }
                        }
                        break;
                    }
                }
                List<Dictionary<string, string>> subAppList = ParseHTable(data, 1);
                if (subAppList?.Count > 0)
                {
                    foreach (Dictionary<string, string> dict in subAppList)
                    {
                        Dictionary<string, string> dbgDict = new Dictionary<string, string> { [DEBUG_APP_NAME] = appName, [DEBUG_APP_ID] = appId };
                        if (dict.ContainsKey(DEBUG_CLI_SUB_APP_NAME)) dbgDict[DEBUG_SUB_APP_NAME] = dict[DEBUG_CLI_SUB_APP_NAME];
                        if (dict.ContainsKey(DEBUG_CLI_SUB_APP_ID)) dbgDict[DEBUG_SUB_APP_ID] = dict[DEBUG_CLI_SUB_APP_ID];
                        if (dict.ContainsKey(DEBUG_CLI_SUB_APP_LEVEL))
                        {
                            int iLogLevel = (int)Utils.StringToSwitchDebugLevel(Utils.ToPascalCase(dict[DEBUG_CLI_SUB_APP_LEVEL]));
                            dbgDict[DEBUG_SUB_APP_LEVEL] = iLogLevel.ToString();
                        }
                        table.Add(dbgDict);
                    }
                }
            }
            return table;
        }

        public static List<Dictionary<string, string>> ParseTrafficTable(string inputData)
        {
            List<Dictionary<string, string>> table = new List<Dictionary<string, string>>();
            string data = inputData.Replace(", ", "\n");
            string[] split;
            Dictionary<string, string> dict = new Dictionary<string, string>();
            using (StringReader reader = new StringReader(data))
            {
                string line;
                string keyId = string.Empty;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Trim().Length == 0) continue;
                    if (line.Contains(TRAF_SLOT_PORT))
                    {
                        keyId = string.Empty;
                        split = line.Trim().Split(':');
                        if (split.Length == 2) dict = new Dictionary<string, string> { [PORT] = split[1].Trim() };
                    }
                    while ((line = reader.ReadLine()) != null)
                    {
                        Match match = vtableRegex.Match(line);
                        string value = null;
                        string key = null;
                        if (match.Success)
                        {
                            key = $"{keyId}{match.Groups[1].Value.Trim()}";
                            if (key.StartsWith(FPGA)) key = FPGA;
                            value = match.Groups[2].Value.Trim();
                            value = value.EndsWith(",") ? value.Substring(0, value.Length - 1) : value;
                            if (value.Contains(":"))
                            {
                                split = value.Split(':');
                                if (split.Length == 2)
                                {
                                    key = $"{keyId}{split[0].Trim()}";
                                    value = split[1].Trim();
                                }
                            }
                        }
                        else if (line.Contains(":"))
                        {
                            split = line.Split(':');
                            if (split.Length == 2 && string.IsNullOrEmpty(split[1]))
                            {
                                if (split[0].Contains("Rx") || split[0].Contains("Tx"))
                                {
                                    keyId = split[0].Trim();
                                    continue;
                                }
                            }
                        }
                        if (line.Contains(TRAF_SLOT_PORT))
                        {
                            keyId = string.Empty;
                            table.Add(dict);
                            split = line.Trim().Split(':');
                            if (split.Length == 2) dict = new Dictionary<string, string> { [PORT] = split[1].Trim() };
                        }
                        if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(key) && !dict.ContainsKey(key)) dict.Add(key, value);
                    }
                }
                if (dict?.Count > 0 && dict.ContainsKey(PORT) && !string.IsNullOrEmpty(dict[PORT])) table.Add(dict);
            }
            return table;
        }

        public static Dictionary<string, List<string>> ParseSwitchConfigChanges(string data)
        {
            Dictionary<string, List<string>> table = new Dictionary<string, List<string>>();
            using (StringReader reader = new StringReader(data))
            {
                string line;
                string key = null;
                List<string> features = new List<string>();
                while ((line = reader.ReadLine()) != null)
                {
                    if(string.IsNullOrEmpty(line)) continue;
                    if (line.StartsWith("!"))
                    {
                        key = line.Replace("!", "").Replace(":", "").Trim();
                        table[key] = new List<string>();
                        continue;
                    }
                    if (string.IsNullOrEmpty(key) || !table.ContainsKey(key)) continue;
                    table[key].Add(line);
                }
            }
            return table;
        }

        public static List<Dictionary<string, string>> ParseFreeSpace(string inputData)
        {
            string data = inputData.Replace(", ", "\n");
            string[] split;
            List<Dictionary<string, string>> table = new List<Dictionary<string, string>>();
            using (StringReader reader = new StringReader(data))
            {
                string line;
                string keyId = string.Empty;
                while ((line = reader.ReadLine()) != null)
                {
                    string sLine = line.Trim();
                    if (sLine.Length == 0) continue;
                    string subStr = new Regex(" {2,}").Replace(sLine, " ");
                    split = subStr.Trim().Split('/');
                    if (split.Length >= 2)
                    {
                        Dictionary<string, string> dict = new Dictionary<string, string> { [P_CHASSIS] = Utils.StringToInt(split[0]).ToString() };
                        split = split[1].Split(' ');
                        if (split.Length >= 2) dict[FLASH] = split[1];
                        table.Add(dict);
                    }
                }
            }
            return table;
        }

        public static Dictionary<string, string> ParseInfoSize(string inputData, string filter)
        {
            string data = inputData.Replace(", ", "\n");
            string[] split;
            Dictionary<string, string> dict = new Dictionary<string, string>();
            using (StringReader reader = new StringReader(data))
            {
                string line;
                string keyId = string.Empty;
                while ((line = reader.ReadLine()) != null)
                {
                    string sLine = line.Trim();
                    if (sLine.Length == 0) continue;
                    if (sLine.Contains(filter))
                    {
                        string subStr = new Regex(" {2,}").Replace(sLine, " ");
                        split = subStr.Trim().Split(' ');
                        foreach(string sVal in split)
                        {
                            string sValue = sVal.Trim();
                            if (string.IsNullOrEmpty(sValue)) continue;
                            if (!dict.ContainsKey("Filesystem"))
                            {
                                dict["Filesystem"] = sValue;
                                continue;
                            }
                            else if (!dict.ContainsKey("Size"))
                            {
                                dict["Size"] = sValue;
                                continue;
                            }
                            else if (!dict.ContainsKey("Used"))
                            {
                                dict["Used"] = sValue;
                                continue;
                            }
                            else if (!dict.ContainsKey("Available"))
                            {
                                dict["Available"] = sValue;
                                continue;
                            }
                            else if (!dict.ContainsKey("Usage"))
                            {
                                dict["Usage"] = sValue;
                                continue;
                            }
                            if (dict.Count >= 5) break;
                        }
                    }
                }
            }
            return dict;
        }

        private static Dictionary<string, string> ParseTable(string data, Regex regex)
        {
            Dictionary<string, string> table = new Dictionary<string, string>();
            using (StringReader reader = new StringReader(data))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Match match = regex.Match(line);
                    if (match.Success)
                    {
                        string key = match.Groups[1].Value.Trim();
                        if (key.StartsWith(FPGA)) key = FPGA;
                        string value = match.Groups[2].Value.Trim();
                        value = value.EndsWith(",") ? value.Substring(0, value.Length - 1) : value;
                        if (!table.ContainsKey(key))
                            table.Add(key, value);
                    }
                }
            }
            return table;
        }

        private static int LineNumberFromPosition(string text, int matchIndex)
        {
            int line = 0;
            for (int i = 0; i < matchIndex; i++)
            {
                if (text[i] == '\n') line++;
            }
            return line;
        }

        private static string[] GetValues(string sep, string line)
        {
            int idx = 0;
            List<string> vals = new List<string>();
            string[] split = sep.Split('+');
            for (int i = 0; i < split.Length - 1; i++)
            {
                string s = split[i];
                int len = idx + s.Length < line.Length ? s.Length + 1 : line.Length - idx;
                if (len > 0)
                {
                    vals.Add(line.Substring(idx, len).Trim());
                    idx = sep.IndexOf('+', idx + 1);
                }
                else
                {
                    vals.Add("");
                }
            }
            if (idx < line.Length) vals.Add(line.Substring(idx).Trim());
            else vals.Add("");

            return vals.ToArray();
        }
    }
}
