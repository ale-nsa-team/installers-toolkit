﻿using PoEWizard.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using static PoEWizard.Data.Constants;

namespace PoEWizard.Device
{
    [Serializable]
    public class SwitchModel
    {
        public string Name { get; set; }

        public string IpAddress { get; set; }
        public string MacAddress { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
        public SwitchStatus Status { get; set; }
        public int CnxTimeout { get; set; }
        public string Model { get; set; }
        public string Version { get; set; }
        public string RunningDir { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
        public string SerialNumber { get; set; }
        public string Contact { get; set; }
        public string Power { get; set; } = "0";
        public string Budget { get; set; } = "0";
        public string UpTime { get; set; }

        public List<ChassisModel> ChassisList { get; set; }

        public string ReleaseNumber { get; set; }
        public bool IsConnected { get; set; }

        public PowerSupplyState PowerSupplyState => GetPowerSupplyState();

        public SwitchModel() : this("", DEFAULT_USERNAME, DEFAULT_PASSWORD, 5) { }

        public SwitchModel(string ipAddr, string username, string password, int cnxTimeout)
        {
            IpAddress = ipAddr;
            Login = username;
            Password = password;
            CnxTimeout = cnxTimeout;
            IsConnected = false;
        }

        public void LoadFromDictionary(Dictionary<string, string> dict, DictionaryType dt)
        {
            switch (dt)
            {
                case DictionaryType.System:
                    Name = dict["Name"];
                    Description = dict["Description"];
                    Location = dict["Location"];
                    Contact = dict["Contact"];
                    UpTime = dict["Up Time"];
                    break;
                case DictionaryType.Chassis:
                    Model = dict["Model Name"];
                    SerialNumber = dict["Serial Number"];
                    MacAddress = dict["MAC Address"];
                    break;
                case DictionaryType.RunningDir:
                    RunningDir = dict["Running configuration"];
                    break;
                case DictionaryType.MicroCode:
                    Version = dict["Release"];
                    break;
            }
;
        }

        public void LoadFromList(List<Dictionary<string, string>> list, DictionaryType dt)
        {
            switch (dt)
            {
                case DictionaryType.Chassis:
                    ChassisList = new List<ChassisModel>();
                    foreach (Dictionary<string, string> dict in list)
                    {
                        ChassisModel ci = new ChassisModel(dict);
                        ChassisList.Add(ci);
                        if (ci.IsMaster)
                        {
                            this.Model = ci.Model;
                            this.MacAddress = ci.MacAddress;
                            this.SerialNumber = ci.SerialNumber;
                        }
                    }
                    break;

                case DictionaryType.PortsList:
                    int nchas = list.GroupBy(d => GetChassisId(d)).Count();
                    for (int i = 1; i <= nchas; i++)
                    {
                        List<Dictionary<string, string>> chasList = list.Where(d => GetChassisId(d) == i).ToList();
                        ChassisModel chas = this.GetChassis(GetChassisId(chasList[0]));
                        int nslots = chasList.GroupBy(c => GetSlotId(c)).Count();
                        for (int j = 1; j <= nslots; j++)
                        {
                            var slot = new SlotModel(chasList[j][CHAS_SLOT_PORT]);
                            List<Dictionary<string, string>> slotList = chasList.Where(c => GetSlotId(c) == j).ToList();
                            slot.NbPorts = slotList.Count;
                            foreach (var dict in slotList)
                            {
                                slot.Ports.Add(new PortModel(dict));
                            }
                            chas.Slots.Add(slot);
                        }
                    }
                    
                    break;
                case DictionaryType.LanPower:
                    break;
            }
        }

        public ChassisModel GetChassis(int chassisNumber)
        {
            return ChassisList.FirstOrDefault(c => c.Number == chassisNumber);
        }

        private PowerSupplyState GetPowerSupplyState()
        {
            if (ChassisList != null && ChassisList.Count > 0)
            {
                foreach (ChassisModel chassis in ChassisList)
                {
                    foreach (PowerSupplyInfo ps in chassis.PowerSupplies)
                    {
                        if (ps.Status == PowerSupplyState.Down)
                        {
                            return PowerSupplyState.Down;
                        }
                    }
                }
                return PowerSupplyState.Up;
            }
            return PowerSupplyState.Unknown;
        }

        public void UpdateSwitchUplinks()
        {
            if (this.ChassisList?.Count == 0) return;
            foreach (ChassisModel chassis in this.ChassisList)
            {
                foreach (SlotModel slot in chassis.Slots)
                {
                    slot.Ports?.ToList().ForEach(port =>
                    {
                        port.IsUplink = port.IsLldp || port.IsVfLink;
                    });
                }
            }
        }

        public void UpdateUplink(string portNr, bool isUplink)
        {
            this.ChassisList?.ForEach(c =>
            {
                c.Slots?.ForEach(s =>
                {
                    s.Ports.ForEach(p =>
                    {
                        string name = $"{c.Number}/{s.Number}/{p.Number}";
                        if (name.Equals(portNr))
                        {
                            p.IsUplink = isUplink;
                            return;
                        }
                    });
                });
            });
        }

        private int GetChassisId(Dictionary<string, string> chas)
        {
            string chId = chas[CHAS_SLOT_PORT];
            string[] parts = chId.Split('/');
            return int.TryParse(parts[0], out int i) ? i : 0;
        }

        private int GetSlotId(Dictionary<string, string> chas)
        {
            string chId = chas[CHAS_SLOT_PORT];
            string[] parts = chId.Split('/');
            return int.TryParse(parts[1], out int i) ? i : 0;
        }
    }

}
