﻿using PoEWizard.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using static PoEWizard.Data.Constants;

namespace PoEWizard.Device
{
    [Serializable]
    public class PortModel
    {
        public string Number { get; set; }
        public string Name { get; set; }
        public PoeStatus Poe { get; set; } = PoeStatus.NoPoe;
        public double Power { get; set; }
        public double MaxPower { get; set; }
        public PortStatus Status { get; set; }
        public PriorityLevelType PriorityLevel { get; set; }
        public bool IsUplink { get; set; } = false;
        public bool IsLldpMdi { get; set; } = false;
        public bool IsLldpExtMdi { get; set; } = false;
        public bool IsVfLink { get; set; } = false;
        public bool Is4Pair { get; set; } = true;
        public bool IsPowerOverHdmi { get; set; }
        public bool IsCapacitorDetection { get; set; }
        public ConfigType Protocol8023bt { get; set; }
        public bool IsEnabled { get; set; } = false;
        public string Class { get; set; }
        public string Type { get; set; }
        public List<string> MacList { get; set; }
        public EndPointDeviceModel EndPointDevice { get; set; }
        public List<PriorityLevelType> Priorities => Enum.GetValues(typeof(PriorityLevelType)).Cast<PriorityLevelType>().ToList();

        public PortModel(Dictionary<string, string> dict)
        {
            Name = Utils.GetDictValue(dict, CHAS_SLOT_PORT);
            Number = GetPortId(Name);
            UpdatePortStatus(dict);
            Power = 0;
            Poe = PoeStatus.NoPoe;
            MaxPower = 0;
            PriorityLevel = PriorityLevelType.Low;
            IsUplink = false;
            IsLldpMdi = false;
            IsLldpExtMdi = false;
            IsVfLink = false;
            Is4Pair = false;
            IsPowerOverHdmi = true;
            IsCapacitorDetection = true;
            Protocol8023bt = ConfigType.Unavailable;
            MacList = new List<string>();
            EndPointDevice = new EndPointDeviceModel();
        }

        public void LoadPoEData(Dictionary<string, string> dict)
        {
            MaxPower = ParseNumber(Utils.GetDictValue(dict, MAXIMUM))/1000;
            Power = ParseNumber(Utils.GetDictValue(dict, USED))/1000;
            switch (Utils.GetDictValue(dict, STATUS))
            {
                case POWERED_ON:
                    Poe = PoeStatus.On;
                    break;
                case SEARCHING:
                    Poe = PoeStatus.Searching;
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
            PriorityLevel = Enum.TryParse(Utils.GetDictValue(dict, PRIORITY), true, out PriorityLevelType res) ? res : PriorityLevelType.Low;
            string powerClass = Utils.ExtractNumber(Utils.GetDictValue(dict, CLASS));
            Class = Poe != PoeStatus.NoPoe && !string.IsNullOrEmpty(powerClass) ? $"{powerClass} ({powerClassTable[powerClass]})" : string.Empty;
            Type = Utils.GetDictValue(dict, TYPE);
        }

        public void LoadPoEConfig(Dictionary<string, string> dict) 
        {
            Is4Pair = (Utils.GetDictValue(dict, POWER_4PAIR)) == "enabled";
            IsPowerOverHdmi = (Utils.GetDictValue(dict, POWER_OVER_HDMI)) == "enabled";
            IsCapacitorDetection = (Utils.GetDictValue(dict, POWER_CAPACITOR_DETECTION)) == "enabled";
            switch (Utils.GetDictValue(dict, POWER_823BT))
            {
                case "NA":
                    Protocol8023bt = ConfigType.Unavailable;
                    break;

                case "enabled":
                    Protocol8023bt = ConfigType.Enable;
                    break;

                case "disabled":
                    Protocol8023bt = ConfigType.Disable;
                    break;
            }
        }

        public void LoadLldpRemoteTable(Dictionary<string, string> dict)
        {
            EndPointDevice.LoadLldpRemoteTable(dict);
        }

        public void LoadLldpInventoryTable(Dictionary<string, string> dict)
        {
            EndPointDevice.LoadLldpInventoryTable(dict);
        }

        public void UpdatePortStatus(Dictionary<string, string> dict)
        {
            IsEnabled = (Utils.GetDictValue(dict, ADMIN_STATUS)) == "enable";
            string sValStatus = Utils.FirstChToUpper(Utils.GetDictValue(dict, LINK_STATUS));
            if (!string.IsNullOrEmpty(sValStatus) && Enum.TryParse(sValStatus, out PortStatus portStatus)) Status = portStatus; else Status = PortStatus.Unknown;
        }

        public void UpdateMacList(List<Dictionary<string, string>> dictList)
        {
            MacList.Clear();
            foreach (Dictionary<string, string> dict in dictList)
            {
                if (AddMacToList(dict) >= 10) break;
            }
            IsUplink = (MacList.Count > 2);
        }

        public int AddMacToList(Dictionary<string, string> dict)
        {
            if (MacList.Count < 10)
            {
                MacList.Add(Utils.GetDictValue(dict, PORT_MAC_LIST) );
            }
            IsUplink = (MacList.Count > 2);
            return MacList.Count;
        }

        private string GetPortId(string chas)
        {
            string[] split = chas.Split('/');
            if (split.Length < 1) return "0";
            return split[split.Length - 1];
        }

        public double ParseNumber(string val)
        {
            return double.TryParse(val.Replace("*", ""), out double n) ? n : 0;
        }

    }
}
