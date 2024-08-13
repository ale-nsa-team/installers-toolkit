﻿using PoEWizard.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
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
        public string Fpga { get; set; }
        public string Cpld { get; set; }
        public string RunningDir { get; set; }
        public SyncStatusType SyncStatus { get; set; }
        public string SyncStatusLabel => GetLabelSyncStatus();
        public string Location { get; set; }
        public string Description { get; set; }
        public string SerialNumber { get; set; }
        public string Contact { get; set; }
        public double Power { get; set; } = 0;
        public double Budget { get; set; } = 0;
        public string UpTime { get; set; }
        public List<ChassisModel> ChassisList { get; set; }
        public bool IsConnected { get; set; }
        public PowerSupplyState PowerSupplyState => GetPowerSupplyState();
        public string ConfigSnapshot { get; set; }
        public string CurrTemperature { get; set; } 
        public ThresholdType TemperatureStatus { get; set; }
        public int Cpu { get; set; }
        public int CpuThreshold { get; set; }
        public bool SupportsPoE { get; set; }
        public Dictionary<string, SwitchDebugApp> DebugApp { get; set; }
        public int LpNiDebugLevel => GetAppLogLevel(LPNI);
        public string LpNiLabelDebugLevel => GetLabelAppLogLevel(LPNI);
        public int LpCmmDebugLevel => GetAppLogLevel(LPCMM);
        public string LpCmmLabelDebugLevel => GetLabelAppLogLevel(LPCMM);

        public SwitchModel() : this("", DEFAULT_USERNAME, DEFAULT_PASSWORD, 5) { }

        public SwitchModel(string ipAddr, string username, string password, int cnxTimeout)
        {
            IpAddress = ipAddr;
            Login = username;
            Password = password;
            CnxTimeout = cnxTimeout;
            IsConnected = false;
            SyncStatus = SyncStatusType.Unknown;
            DebugApp = new Dictionary<string, SwitchDebugApp>();
        }

        public void LoadFromDictionary(Dictionary<string, string> dict, DictionaryType dt)
        {
            switch (dt)
            {
                case DictionaryType.SystemRunningDir:
                    Name = Utils.GetDictValue(dict, SYS_NAME);
                    Description = Utils.GetDictValue(dict, SYS_DESCR);
                    Location = Utils.GetDictValue(dict, SYS_LOCATION);
                    Contact = Utils.GetDictValue(dict, SYS_CONTACT);
                    TimeSpan dur = TimeSpan.FromSeconds(Utils.StringToLong(Utils.GetDictValue(dict, SYS_UP_TIME)) / 100);
                    UpTime = $"{dur.Days} days, {dur.Hours} hours, {dur.Minutes} minutes and {dur.Seconds} seconds";
                    string sync = Utils.GetDictValue(dict, CONFIG_CHANGE_STATUS);
                    string cert = Utils.GetDictValue(dict, CHAS_CONTROL_CERTIFY);
                    SyncStatus = sync == "1" ? cert == "3" ? SyncStatusType.Synchronized :
                                 cert == "2" ? SyncStatusType.SynchronizedNeedCertified : SyncStatusType.SynchronizedUnknownCertified :
                                 SyncStatusType.NotSynchronized;
                    RunningDir = Utils.GetDictValue(dict, SYS_RUNNING_CONFIGURATION);
                    break;

                case DictionaryType.MicroCode:
                    Version = Utils.GetDictValue(dict, RELEASE);
                    break;

                case DictionaryType.Cmm:
                    SerialNumber = Utils.GetDictValue(dict, SERIAL_NUMBER);
                    Model = Utils.GetDictValue(dict, MODEL_NAME);
                    Fpga = Utils.GetDictValue(dict, FPGA);
                    Cpld = Utils.GetDictValue(dict, CPLD);
                    MacAddress = Utils.GetDictValue(dict, CHASSIS_MAC_ADDRESS);
                    break;
            }
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
                    }
                    break;

                case DictionaryType.PortsList:
                    int nchas = list.GroupBy(d => GetChassisId(d)).Count();
                    for (int i = 1; i <= nchas; i++)
                    {
                        List<Dictionary<string, string>> chasList = list.Where(d => GetChassisId(d) == i).ToList();
                        if (chasList?.Count == 0) continue;
                        ChassisModel chas = this.GetChassis(GetChassisId(chasList[0]));
                        int nslots = chasList.GroupBy(c => GetSlotId(c)).Count();
                        for (int j = 1; j <= nslots; j++)
                        {
                            ChassisSlotPort chassisSlot = new ChassisSlotPort(chasList[j][CHAS_SLOT_PORT]);
                            SlotModel slot = chas.GetSlot(chassisSlot.SlotNr);
                            if (slot == null)
                            {
                                slot = new SlotModel(chasList[j][CHAS_SLOT_PORT]);
                                chas.Slots.Add(slot);
                            }
                            List<Dictionary<string, string>> slotList = chasList.Where(c => GetSlotId(c) == j).ToList();
                            slot.NbPorts = slotList.Count;
                            foreach (var dict in slotList)
                            {
                                chassisSlot = new ChassisSlotPort(dict[CHAS_SLOT_PORT]);
                                PortModel port = slot.GetPort(chassisSlot.PortNr);
                                if (port == null) slot.Ports.Add(new PortModel(dict)); else port.UpdatePortStatus(dict);
                            }
                        }
                    }
                    break;

                case DictionaryType.PowerSupply:
                    foreach (var dic in list)
                    {
                        var chas = GetChassis(ParseId(dic[CHAS_PS], 0));
                        if (chas == null) continue;
                        int psId = GetPsId(dic[CHAS_PS]);
                        chas.PowerSupplies.Add(new PowerSupplyModel(psId, dic[LOCATION]) { Name = $"{chas.Number}/{psId}" });
                    }
                    break;

                case DictionaryType.MacAddressList:
                    string prevPort = "";
                    Dictionary<string, PortModel> ports = new Dictionary<string, PortModel>();
                    foreach (Dictionary<string, string> dict in list)
                    {
                        string currPort = Utils.GetDictValue(dict, INTERFACE);
                        ChassisSlotPort slotPort = new ChassisSlotPort(currPort);
                        ChassisModel chassis = GetChassis(slotPort.ChassisNr);
                        if (chassis == null) continue;
                        SlotModel slot = chassis.GetSlot(slotPort.SlotNr);
                        if (slot == null) continue;
                        PortModel port = slot.GetPort(slotPort.PortNr);
                        if (port == null) continue;
                        if (currPort != prevPort)
                        {
                            port.MacList.Clear();
                            prevPort = currPort;
                        }
                        if (port.MacList?.Count >= 10) continue;
                        port.AddMacToList(dict);
                        ports[port.Name] = port;
                    }
                    foreach(string key in ports.Keys)
                    {
                        PortModel port = ports[key];
                        if (!string.IsNullOrEmpty(port.EndPointDevice.Type)) continue;
                        Dictionary<string, string> ep = new Dictionary<string, string>
                        {
                            [LOCAL_PORT] = port.Name,
                            [CAPABILITIES_ENABLED] = "Unknown",
                            [MAC_NAME] = string.Join(",", port.MacList)
                        };
                        port.EndPointDevice = new EndPointDeviceModel(ep);
                        port.EndPointDevicesList.Add(port.EndPointDevice);
                    }
                    break;

                case DictionaryType.TemperatureList:
                    SwitchTemperature temperature = new SwitchTemperature();
                    foreach (var dic in list)
                    {
                        string[] split = Utils.GetDictValue(dic, CHAS_DEVICE).Trim().Split('/');
                        ChassisModel chas = GetChassis(Utils.StringToInt(split[0]));
                        if (chas == null) continue;
                        chas.LoadTemperature(dic);
                        if (temperature.Current < chas.Temperature.Current) temperature = chas.Temperature;
                    }
                    CurrTemperature = $"{(temperature.Current * 9 / 5) + 32}{F} ({temperature.Current}{C})";
                    TemperatureStatus = temperature.Status;
                    break;

                case DictionaryType.CpuTrafficList:
                    int cpu = 0;
                    foreach (var dic in list)
                    {
                        string slotNr = Utils.GetDictValue(dic, CPU).ToLower().Replace("slot", "").Trim();
                        SlotModel slot = GetSlot(slotNr);
                        if (slot == null) continue;
                        slot.Cpu = Utils.StringToInt(Utils.GetDictValue(dic, CURRENT));
                        if (slot.Cpu > cpu) cpu = slot.Cpu;
                    }
                    Cpu = cpu;
                    break;

                case DictionaryType.SwitchDebugAppList:
                    foreach (Dictionary<string, string> dict in list)
                    {
                        string appId = Utils.GetDictValue(dict, DEBUG_APP_ID);
                        string appName = Utils.GetDictValue(dict, DEBUG_APP_NAME);
                        string appIndex = Utils.GetDictValue(dict, DEBUG_APP_INDEX);
                        if (!string.IsNullOrEmpty(appId) && !string.IsNullOrEmpty(appName) && !string.IsNullOrEmpty(appIndex))
                        {
                            string nbSubApp = string.Empty;
                            switch (appName)
                            {
                                case LPNI:
                                    nbSubApp = "3";
                                    break;

                                case LPCMM:
                                    nbSubApp = "4";
                                    break;
                            }
                            DebugApp[appName] = new SwitchDebugApp(appName, appId, appIndex, nbSubApp);
                        }
                    }
                    break;
            }
        }

        public void LoadLldpFromList(Dictionary<string, List<Dictionary<string, string>>> list, DictionaryType dt)
        {
            foreach (string key in list.Keys.ToList())
            {
                ChassisSlotPort slotPort = new ChassisSlotPort(key);
                ChassisModel chassis = GetChassis(slotPort.ChassisNr);
                if (chassis == null) continue;
                SlotModel slot = chassis.GetSlot(slotPort.SlotNr);
                if (slot == null) continue;
                PortModel port = slot.GetPort(slotPort.PortNr);
                if (port == null) continue;
                List<Dictionary<string, string>> dictList = list[key];
                if (dt == DictionaryType.LldpRemoteList) port.LoadLldpRemoteTable(dictList); else port.LoadLldpInventoryTable(dictList);
            }
        }

        public void UpdateCpuThreshold(Dictionary<string, string> dict)
        {
            CpuThreshold = Utils.StringToInt(Utils.GetDictValue(dict, CPU_THRESHOLD).Trim());
        }

        public ChassisModel GetChassis(int chassisNumber)
        {
            return ChassisList.FirstOrDefault(c => c.Number == chassisNumber);
        }

        public ChassisModel GetChassis(string slotName)
        {
            return ChassisList.FirstOrDefault(c => c.Number == ParseId(slotName, 0));
        }

        public SlotModel GetSlot(string slotName)
        {
            ChassisModel cm = GetChassis(slotName);
            return cm?.Slots.FirstOrDefault(s => s.Name == slotName);
        }

        private PowerSupplyState GetPowerSupplyState()
        {
            if (ChassisList != null && ChassisList.Count > 0)
            {
                foreach (ChassisModel chassis in ChassisList)
                {
                    foreach (PowerSupplyModel ps in chassis.PowerSupplies)
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
                        port.IsUplink = port.IsLldpMdi || port.IsLldpExtMdi || port.IsVfLink;
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

        public void SetAppLogLevel(string app, int logLevel)
        {
            if (!string.IsNullOrEmpty(app) && DebugApp.ContainsKey(app)) this.DebugApp[app].DebugLevel = logLevel;
            else this.DebugApp[app].DebugLevel = (int)SwitchDebugLogLevel.Unknown;
        }

        public PortModel GetPort(string slotPortNr)
        {
            ChassisSlotPort slotPort = new ChassisSlotPort(slotPortNr);
            ChassisModel chassisModel = ChassisList.FirstOrDefault(c => c.Number == slotPort.ChassisNr);
            if (chassisModel == null) return null;
            SlotModel slotModel = chassisModel.Slots.FirstOrDefault(c => c.Number == slotPort.SlotNr);
            if (slotModel == null) return null;
            return slotModel.Ports.FirstOrDefault(c => c.Number == slotPort.PortNr);
        }

        private int GetChassisId(Dictionary<string, string> chas)
        {
            string[] parts = chas[CHAS_SLOT_PORT].Split('/');
            if (parts.Length < 3) return 1;
            return ParseId(chas[CHAS_SLOT_PORT], 0);
        }

        private int GetSlotId(Dictionary<string, string> chas)
        {
            string[] parts = chas[CHAS_SLOT_PORT].Split('/');
            int idx = (parts.Length > 2) ? 1 : 0;
            return ParseId(chas[CHAS_SLOT_PORT], idx);
        }

        private int GetPsId(string chId)
        {
            return ParseId(chId, 1);
        }

        private int ParseId(string chSlotPort, int index)
        {
            string[] parts = chSlotPort.Split('/');
            return parts.Length > index ? (int.TryParse(parts[index], out int i) ? i : 0) : 0;
        }

        private int GetAppLogLevel(string app)
        {
            return !string.IsNullOrEmpty(app) && DebugApp.ContainsKey(app) ? DebugApp[app].DebugLevel : (int)SwitchDebugLogLevel.Unknown;
        }

        private string GetLabelAppLogLevel(string app)
        {
            return !string.IsNullOrEmpty(app) && DebugApp.ContainsKey(app) ? Utils.IntToSwitchDebugLevel(DebugApp[app].DebugLevel).ToString() : string.Empty;
        }

        private string GetLabelSyncStatus()
        {
            return SyncStatus != SyncStatusType.Unknown ? Utils.GetEnumDescription(SyncStatus) : string.Empty;
        }
    }
}
