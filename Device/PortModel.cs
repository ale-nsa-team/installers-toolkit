﻿using PoEWizard.Data;
using System;
using System.Collections.Generic;
using static PoEWizard.Data.Constants;

namespace PoEWizard.Device
{
    [Serializable]
    public class PortModel
    {
        public string Number { get; set; }
        public bool HasPoe { get; set; }
        public PoeStatus Poe { get; set; }
        public string Power { get; set; }
        public string MaxPower { get; set; }
        public PortStatus Status { get; set; }
        public PriorityLevelType PriorityLevel { get; set; }
        public bool IsUplink { get; set; } = false;
        public bool IsLldp { get; set; } = false;
        public bool IsVfLink { get; set; } = false;
        public bool Is4Pair { get; set; } = true;
        public bool IsPowerOverHdmi { get; set; } = true;
        public bool IsCapacitorDetection { get; set; } = true;
        public ConfigType Protocol8023bt { get; set; } = ConfigType.Unavailable;
        public bool IsEnabled { get; set; } = false;
        public string Class { get; set; }
        public string Type { get; set; }
        public List<string> MacList { get; set; } = new List<string>();

        public PortModel(Dictionary<string, string> dict)
        {

            Number = GetPortId(dict.TryGetValue(CHAS_SLOT_PORT, out string s) ? s : "");
            UpdatePortStatus(dict);
            HasPoe = false;
            Power = "0 mw";
            Poe = PoeStatus.NoPoe;
            MaxPower = "0 mw";
            PriorityLevel = PriorityLevelType.Low;
            IsUplink = false;
            IsLldp = false;
            IsVfLink = false;
            Is4Pair = false;
        }

        public void LoadPoEData(Dictionary<string, string> dict)
        {
            MaxPower = dict.TryGetValue(MAXIMUM, out string s) ? s : "";
            Power = dict.TryGetValue(USED, out s) ? s : "";
            HasPoe = true;
            switch (dict.TryGetValue(STATUS, out s) ? s : "")
            {
                case POWERED_ON:
                case SEARCHING:
                    Poe = PoeStatus.On;
                    break;
                case POWERED_OFF:
                    Poe = PoeStatus.Off;
                    break;
                case FAULT:
                    Poe = PoeStatus.Fault;
                    break;
                case BAD_VOLTAGE_INJECTION:
                    Poe = PoeStatus.Conflict;
                    break;
                case DENY:
                    Poe = PoeStatus.Deny;
                    break;
            }
            string level = dict.TryGetValue(PRIORITY, out s) ? s : "";
            PriorityLevel = Enum.TryParse<PriorityLevelType>(level, true, out PriorityLevelType res) ? res : PriorityLevelType.Unknown;
            Class = dict.TryGetValue(CLASS, out s) ? s : "";
            Type = dict.TryGetValue(TYPE, out s) ? s : "";
        }

        public void LoadPoEConfig(Dictionary<string, string> dict) 
        {
            Is4Pair = (dict.TryGetValue(POWER_4PAIR, out string s) ? s : "") == "enabled";
            IsPowerOverHdmi = (dict.TryGetValue(POWER_OVER_HDMI, out s) ? s : "") == "enabled";
            IsCapacitorDetection = (dict.TryGetValue(POWER_CAPACITOR_DETECTION, out s) ? s : "") == "enabled";
            dict.TryGetValue(POWER_823BT, out s);
            switch (s)
            {
                case "NA":
                    Protocol8023bt = ConfigType.Unavailable;
                    break;

                case "enabled":
                    Protocol8023bt = ConfigType.Enabled;
                    break;

                case "disabled":
                    Protocol8023bt = ConfigType.Disabled;
                    break;
            }
        }

        public void UpdatePortStatus(Dictionary<string, string> dict)
        {
            IsEnabled = (dict.TryGetValue(ADMIN_STATUS, out string s) ? s : "") == "enable";
            string sValStatus = Utils.FirstChToUpper(dict.TryGetValue(LINK_STATUS, out s) ? s : "");
            if (!string.IsNullOrEmpty(sValStatus) && Enum.TryParse(sValStatus, out PortStatus portStatus)) Status = portStatus; else Status = PortStatus.Unknown;
        }

        public void UpdateMacList(List<Dictionary<string, string>> dictList)
        {
            int nbMac = 1;
            foreach (Dictionary<string, string> dict in dictList)
            {
                MacList.Add(dict.TryGetValue(PORT_MAC_LIST, out string s) ? s : "");
                nbMac++;
                if (nbMac > 10) break;
            }
            IsUplink = (nbMac > 2);
        }

        private string GetPortId(string chas)
        {
            string[] split = chas.Split('/');
            if (split.Length < 1) return "0";
            return split[split.Length - 1];
        }

    }
}
