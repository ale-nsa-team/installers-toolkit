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
        public string NetMask { get; set; }

        #region Selected slot data
        public string SelectedSlot { get; set; }
        public string Model { get; set; }
        public string SerialNumber { get; set; }
        public string MacAddress { get; set; }
        public string CurrTemperature { get; set; }
        public ThresholdType TemperatureStatus { get; set; }
        public string Cpu { get; set; }
        public string Fpga { get; set; }
        public string Cpld { get; set; }
        public string FreeFlash { get; set; }
        #endregion

        public string DefaultGwy { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
        public SwitchStatus Status { get; set; }
        public int CnxTimeout { get; set; }
        public string Version { get; set; }
        public string RunningDir { get; set; }
        public SyncStatusType SyncStatus { get; set; }
        public string SyncStatusLabel => GetLabelSyncStatus();
        public string Location { get; set; }
        public string Description { get; set; }
        public string Contact { get; set; }
        public double Power { get; set; } = 0;
        public double Budget { get; set; } = 0;
        public string UpTime { get; set; }
        public List<ChassisModel> ChassisList { get; set; }
        public bool IsConnected { get; set; }
        public PowerSupplyState PowerSupplyState => GetPowerSupplyState();
        public string ConfigSnapshot { get; set; }
        public int CpuThreshold { get; set; }
        public bool SupportsPoE { get; set; }
        public Dictionary<string, SwitchDebugApp> DebugApp { get; set; }
        public int LpNiDebugLevel => GetAppLogLevel(LPNI);
        public string LpNiLabelDebugLevel => GetLabelAppLogLevel(LPNI);
        public int LpCmmDebugLevel => GetAppLogLevel(LPCMM);
        public string LpCmmLabelDebugLevel => GetLabelAppLogLevel(LPCMM);

        public SwitchModel() : this("", DEFAULT_USERNAME, DEFAULT_PASSWORD, SWITCH_CONNECT_TIMEOUT_SEC) { }

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
            if (dict == null || dict.Count == 0) return;
            switch (dt)
            {
                case DictionaryType.SystemRunningDir:
                    Name = Utils.GetDictValue(dict, SYS_NAME);
                    Description = Utils.GetDictValue(dict, SYS_DESCR);
                    Location = Utils.GetDictValue(dict, SYS_LOCATION);
                    Contact = Utils.GetDictValue(dict, SYS_CONTACT);
                    TimeSpan dur = TimeSpan.FromSeconds(Utils.StringToLong(Utils.GetDictValue(dict, SYS_UP_TIME)) / 100);
                    UpTime = $"{(dur.Days > 0 ? $"{dur.Days} d : " : "")}{(dur.Hours > 0 ? $"{dur.Hours} h : " : "")}{dur.Minutes} min : {dur.Seconds} sec";
                    string sync = Utils.GetDictValue(dict, CONFIG_CHANGE_STATUS);
                    string cert = Utils.GetDictValue(dict, CHAS_CONTROL_CERTIFY);
                    SyncStatus = sync == "1" ? cert == "3" ? SyncStatusType.Synchronized :
                                 cert == "2" ? SyncStatusType.SynchronizedNeedCertified : SyncStatusType.SynchronizedUnknownCertified :
                                 SyncStatusType.NotSynchronized;
                    RunningDir = Utils.ToPascalCase(Utils.GetDictValue(dict, SYS_RUNNING_CONFIGURATION));
                    break;

                case DictionaryType.MicroCode:
                    Version = Utils.GetDictValue(dict, RELEASE);
                    break;
            }
        }

        public void LoadFromList(List<Dictionary<string, string>> dictList, DictionaryType dt)
        {
            if (dictList == null || dictList.Count == 0) return;
            ChassisModel chassis;
            switch (dt)
            {
                case DictionaryType.Chassis:
                    ChassisList = new List<ChassisModel>();
                    foreach (Dictionary<string, string> dict in dictList)
                    {
                        chassis = new ChassisModel(dict);
                        ChassisList.Add(chassis);
                    }
                    break;

                case DictionaryType.Cmm:
                    if (ChassisList?.Count > 0)
                    {
                        foreach (Dictionary<string, string> dict in dictList)
                        {
                            int chassisNr = Utils.StringToInt(Utils.GetDictValue(dict, ID)) - 1;
                            if (chassisNr >= 0 && chassisNr < ChassisList.Count)
                            {
                                chassis = this.ChassisList[chassisNr];
                                chassis.SerialNumber = Utils.GetDictValue(dict, SERIAL_NUMBER);
                                chassis.Model = Utils.GetDictValue(dict, MODEL_NAME);
                                chassis.Fpga = Utils.GetDictValue(dict, FPGA);
                                chassis.Cpld = Utils.GetDictValue(dict, CPLD);
                                chassis.MacAddress = Utils.GetDictValue(dict, CHASSIS_MAC_ADDRESS);
                            }
                            UpdateChassisSelectedSlot();
                        }
                    }
                    break;

                case DictionaryType.PortsList:
                    int nchas = dictList.GroupBy(d => GetChassisId(d)).Count();
                    for (int i = 1; i <= nchas; i++)
                    {
                        List<Dictionary<string, string>> chasList = dictList.Where(d => GetChassisId(d) == i).ToList();
                        if (chasList?.Count == 0) continue;
                        chassis = this.GetChassis(GetChassisId(chasList[0]));
                        if (chassis == null) continue;
                        int nslots = chasList.GroupBy(c => GetSlotId(c)).Count();
                        for (int j = 1; j <= nslots; j++)
                        {
                            List<Dictionary<string, string>> slotList = chasList.Where(c => GetSlotId(c) == j).ToList();
                            if (slotList?.Count == 0) continue;
                            ChassisSlotPort chassisSlot = new ChassisSlotPort(slotList[0][CHAS_SLOT_PORT]);
                            SlotModel slot = chassis.GetSlot(chassisSlot.SlotNr);
                            if (slot == null)
                            {
                                slot = new SlotModel(chassisSlot);
                                chassis.Slots.Add(slot);
                            }
                            slot.IsMaster = chassis.IsMaster;
                            foreach (var dict in slotList)
                            {
                                chassisSlot = new ChassisSlotPort(dict[CHAS_SLOT_PORT]);
                                PortModel port = slot.GetPort(dict[CHAS_SLOT_PORT]);
                                if (port == null) slot.Ports.Add(new PortModel(dict)); else port.UpdatePortStatus(dict);
                            }
                            slot.NbPorts = slotList.Count;
                        }
                    }
                    break;

                case DictionaryType.PowerSupply:
                    foreach (var dic in dictList)
                    {
                        chassis = GetChassis(ParseId(dic[CHAS_PS], 0));
                        if (chassis == null) continue;
                        int psId = GetPsId(dic[CHAS_PS]);
                        PowerSupplyModel newPsm = new PowerSupplyModel(psId, dic[LOCATION]) { Name = $"{chassis.Number}/{psId}" };
                        PowerSupplyModel psm = chassis.PowerSupplies.FirstOrDefault(p => p.Id == psId);
                        if (psm == null) chassis.PowerSupplies.Add(newPsm);
                        else psm = newPsm;
                    }
                    break;

                case DictionaryType.TemperatureList:
                    SwitchTemperature temperature = new SwitchTemperature();
                    foreach (var dic in dictList)
                    {
                        string[] split = Utils.GetDictValue(dic, CHAS_DEVICE).Trim().Split('/');
                        chassis = GetChassis(Utils.StringToInt(split[0]));
                        if (chassis == null) continue;
                        chassis.LoadTemperature(dic);
                    }
                    UpdateTemperatureSelectedSlot();
                    break;

                case DictionaryType.CpuTrafficList:
                    foreach (var dic in dictList)
                    {
                        string slotNr = Utils.GetDictValue(dic, CPU).ToLower().Replace("slot", "").Trim();
                        chassis = GetChassis(slotNr);
                        if (chassis == null) continue;
                        chassis.Cpu = Utils.StringToInt(Utils.GetDictValue(dic, CURRENT));
                    }
                    UpdateCpuSelectedSlot();
                    break;

                case DictionaryType.SwitchDebugAppList:
                    foreach (Dictionary<string, string> dict in dictList)
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
                            this.DebugApp[appName] = new SwitchDebugApp(appName, appId, appIndex, nbSubApp);
                        }
                    }
                    break;

                case DictionaryType.ShowInterfacesList:
                    foreach (Dictionary<string, string> dict in dictList)
                    {
                        string slotPortNr = Utils.GetDictValue(dict, PORT);
                        if (string.IsNullOrEmpty(slotPortNr)) continue;
                        PortModel port = GetPort(slotPortNr);
                        if (port == null) continue;
                        port.LoadDetailInfo(dict);
                    }
                    break;
            }
        }

        public void LoadMacAddressFromList(List<Dictionary<string, string>> dictList, int maxNbMacPerPort)
        {
            if (dictList == null || dictList.Count == 0) return;
            string prevPort = "";
            Dictionary<string, PortModel> ports = new Dictionary<string, PortModel>();
            foreach (Dictionary<string, string> dict in dictList)
            {
                string currPort = Utils.GetDictValue(dict, INTERFACE);
                PortModel port = GetPort(currPort);
                if (port == null) continue;
                if (currPort != prevPort)
                {
                    port.MacList.Clear();
                    prevPort = currPort;
                }
                if (port.MacList?.Count >= maxNbMacPerPort) continue;
                port.AddMacToList(dict);
                ports[port.Name] = port;
            }
            foreach (string key in ports.Keys)
            {
                PortModel port = ports[key];
                port.CreateVirtualDeviceEndpoint();
            }
        }

        public void LoadLldpFromList(Dictionary<string, List<Dictionary<string, string>>> list, DictionaryType dt)
        {
            if (list == null || list.Count < 1) return;
            foreach (string key in list.Keys.ToList())
            {
                PortModel port = GetPort(key);
                if (port == null) continue;
                List<Dictionary<string, string>> dictList = list[key];
                if (dt == DictionaryType.LldpRemoteList) port.LoadLldpRemoteTable(dictList); else port.LoadLldpInventoryTable(dictList);
            }
        }

        public PortModel GetPort(string slotPortNr)
        {
            ChassisSlotPort slotPort = new ChassisSlotPort(slotPortNr);
            ChassisModel chassis = GetChassis(slotPort.ChassisNr);
            if (chassis == null) return null;
            SlotModel slot = chassis.GetSlot(slotPort.SlotNr);
            if (slot == null) return null;
            return slot.GetPort(slotPortNr);
        }

        public void UpdateSelectedSlotData(string slotNr)
        {
            if (!string.IsNullOrEmpty(slotNr)) SelectedSlot = slotNr;
            UpdateChassisSelectedSlot();
            UpdateTemperatureSelectedSlot();
            UpdateCpuSelectedSlot();
        }

        private void UpdateChassisSelectedSlot()
        {
            if (string.IsNullOrEmpty(SelectedSlot)) SelectedSlot = "1/1";
            ChassisModel chassis = GetChassis(SelectedSlot);
            if (chassis == null) return;
            Model = chassis.Model;
            SerialNumber = chassis.SerialNumber;
            MacAddress = SetMasterSlave(chassis, chassis.MacAddress);
            Fpga = !string.IsNullOrEmpty(chassis.Fpga) ? chassis.Fpga : string.Empty;
            Cpld = !string.IsNullOrEmpty(chassis.Cpld) ? chassis.Cpld : string.Empty;
            FreeFlash = chassis.FreeFlash;
        }

        private void UpdateTemperatureSelectedSlot()
        {
            if (string.IsNullOrEmpty(SelectedSlot)) SelectedSlot = "1/1";
            ChassisModel chassis = GetChassis(SelectedSlot);
            if (chassis == null) return;
            CurrTemperature = $"{(chassis.Temperature.Current * 9 / 5) + 32}{F} ({chassis.Temperature.Current}{C})";
            TemperatureStatus = chassis.Temperature.Status;
        }

        private void UpdateCpuSelectedSlot()
        {
            if (string.IsNullOrEmpty(SelectedSlot)) SelectedSlot = "1/1";
            ChassisModel chassis = GetChassis(SelectedSlot);
            if (chassis == null) return;
            Cpu = $"{chassis.Cpu}%";
        }

        public void LoadFlashSizeFromList(string data, ChassisModel chassis)
        {
            if (chassis == null) return;
            List<Dictionary<string, string>> dictList = CliParseUtils.ParseDiskSize(data, "/flash");
            if (dictList == null || dictList.Count == 0) return;
            if (dictList == null || dictList.Count == 0) return;
            foreach (Dictionary<string, string> dict in dictList)
            {
                string mount = Utils.GetDictValue(dict, MOUNTED);
                if (!string.IsNullOrEmpty(mount) && mount == "/flash")
                {
                    chassis.FlashSize = $"{Utils.GetDictValue(dict, SIZE_TOTAL)}B";
                    chassis.FlashSizeUsed = $"{Utils.GetDictValue(dict, SIZE_USED)}B";
                    chassis.FlashSizeFree = $"{Utils.GetDictValue(dict, SIZE_AVAILABLE)}B";
                    chassis.FlashUsage = Utils.GetDictValue(dict, DISK_USAGE);
                    chassis.FreeFlash = $"{chassis.FlashUsage}, Total: {chassis.FlashSize}";
                    break;
                }
            }
            UpdateFlashSelectedSlot();
        }

        public void LoadFreeFlashFromList(string data)
        {
            List<Dictionary<string, string>> dictList = CliParseUtils.ParseFreeSpace(data);
            if (dictList == null || dictList.Count == 0) return;
            foreach (Dictionary<string, string> dict in dictList)
            {
                int chassisNr = Utils.StringToInt(Utils.GetDictValue(dict, P_CHASSIS));
                ChassisModel chassis = GetChassis(chassisNr);
                if (chassis != null)
                {
                    long freeSize = Utils.StringToLong(Utils.GetDictValue(dict, FLASH));
                    chassis.FreeFlash = Utils.PrintNumberBytes(freeSize);
                }
            }
            UpdateFlashSelectedSlot();
        }

        private void UpdateFlashSelectedSlot()
        {
            if (string.IsNullOrEmpty(SelectedSlot)) SelectedSlot = "1/1";
            ChassisModel chassis = GetChassis(SelectedSlot);
            if (chassis == null) return;
            FreeFlash = chassis.FreeFlash;
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

        public void SetAppLogLevel(string app, int logLevel)
        {
            if (!string.IsNullOrEmpty(app) && this.DebugApp.ContainsKey(app)) this.DebugApp[app].DebugLevel = logLevel;
            else this.DebugApp[app] = new SwitchDebugApp(app, logLevel);
        }

        private string SetMasterSlave(ChassisModel chassis, string sValue)
        {
            if (ChassisList.Count > 1) return $"{sValue} (Chassis {chassis.Number}, {(chassis.IsMaster ? MASTER : SLAVE)})"; else return sValue;
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
