﻿using PoEWizard.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static PoEWizard.Data.Constants;

namespace PoEWizard.Device
{

    public class DebugAppModel
    {
        public string ApplicationId { get; set; }
        public string ApplicationName { get; set; }
        public string SubApplicationId { get; set; }
        public string SubApplicationName { get; set; }
        public SwitchDebugLogLevel DebugLevel { get; set; }
        public string SwitchLogLevel { get; set; }

        public DebugAppModel()
        {
            this.ApplicationId = string.Empty;
            this.ApplicationName = string.Empty;
            this.SubApplicationId = string.Empty;
            this.SubApplicationName = string.Empty;
            this.DebugLevel = SwitchDebugLogLevel.Info;
            int logLevel = (int)this.DebugLevel;
            SwitchLogLevel = logLevel.ToString();
        }
        public DebugAppModel(Dictionary<string, string> dict)
        {
            LoadFromDictionary(dict);
        }

        public void LoadFromDictionary(Dictionary<string, string> dict)
        {
            
            this.ApplicationId = Utils.GetDictValue(dict, DEBUG_APP_ID);
            this.ApplicationName = Utils.GetDictValue(dict, DEBUG_APP_NAME);
            this.SubApplicationId = Utils.GetDictValue(dict, DEBUG_SUB_APP_ID);
            this.SubApplicationName = Utils.GetDictValue(dict, DEBUG_SUB_APP_NAME);
            this.SwitchLogLevel = Utils.GetDictValue(dict, DEBUG_SUB_APP_LEVEL);
            this.DebugLevel = !string.IsNullOrEmpty(this.SwitchLogLevel) ? (SwitchDebugLogLevel)Enum.Parse(typeof(SwitchDebugLogLevel), this.SwitchLogLevel) : SwitchDebugLogLevel.Info;
        }
    }

    public class LpNiModel
    {
        public DebugAppModel LanNi { get; set; }
        public DebugAppModel LanXtr { get; set; }
        public DebugAppModel LanNiUtl { get; set; }
        public string SwitchLogLevel => GetSwitchDebugLevel();

        public LpNiModel()
        {
            this.LanNi = new DebugAppModel();
            this.LanXtr = new DebugAppModel();
            this.LanNiUtl = new DebugAppModel();
        }

        public void LoadLanNiFromDictionary(Dictionary<string, string> dict)
        {
            this.LanNi.LoadFromDictionary(dict);
        }
        public void LoadLanXtrFromDictionary(Dictionary<string, string> dict)
        {
            this.LanXtr.LoadFromDictionary(dict);
        }
        public void LoadLanNiUtlFromDictionary(Dictionary<string, string> dict)
        {
            this.LanNiUtl.LoadFromDictionary(dict);
        }

        private string GetSwitchDebugLevel()
        {
            return this.LanNi.SwitchLogLevel;
        }
    }

    public class SwitchDebugModel
    {
        public string LanPowerStatus { get; set; }
        public string LocalSavedFilePath { get; set; }
        public DebugAppModel LldpNiApp { get; set; }
        public LpNiModel LpNiApp { get; set; }
        public SwitchDebugLogLevel DebugLevelSelected { get; set; }
        public string SwitchDebugLevelSelected { get; set; }
        public WizardReport WizardReport { get; set; }

        public SwitchDebugModel(WizardReport wizardReport, SwitchDebugLogLevel logLevel)
        {
            this.WizardReport = wizardReport;
            this.LocalSavedFilePath = Path.Combine(MainWindow.dataPath, Path.GetFileName(SWLOG_PATH));
            this.LanPowerStatus = string.Empty;
            this.LldpNiApp = new DebugAppModel();
            this.LpNiApp = new LpNiModel();
            this.DebugLevelSelected = logLevel;
            int logDebugLevel = (int)this.DebugLevelSelected;
            this.SwitchDebugLevelSelected = logDebugLevel.ToString();
        }

        public void LoadLldpNiFromDictionary(Dictionary<string, string> dict)
        {
            this.LldpNiApp.LoadFromDictionary(dict);
        }

        public void LoadLpNiFromDictionary(List<Dictionary<string, string>> dictList)
        {
            foreach (Dictionary<string, string> dict in dictList)
            {
                string appName = Utils.GetDictValue(dict, DEBUG_SUB_APP_NAME);
                if (appName == DEBUG_SUB_APP_LANNI) this.LpNiApp.LoadLanNiFromDictionary(dict);
                else if (appName == DEBUG_SUB_APP_LANXTR) this.LpNiApp.LoadLanXtrFromDictionary(dict);
                else this.LpNiApp.LoadLanNiUtlFromDictionary(dict);
            }
        }

        public void CreateTacTextFile(string localTarFilepath, SwitchModel device, PortModel port)
        {
            LocalSavedFilePath = localTarFilepath;
            string filePath = Path.Combine(Path.GetDirectoryName(LocalSavedFilePath), Constants.TAC_TEXT_FILE_NAME);
            StringBuilder txt = new StringBuilder("Hello ALE TAC,\n\n\tI am having problems with a PoE device");
            if (port != null)
            {
                txt.Append(" on port ").Append(port.Name).Append(".");
                if (port.EndPointDevice != null && !string.IsNullOrEmpty(port.EndPointDevice.Type))
                {
                    txt.Append("  It is a ");
                    if (!string.IsNullOrEmpty(port.EndPointDevice.Description)) txt.Append(port.EndPointDevice.Description);
                    else if (!string.IsNullOrEmpty(port.EndPointDevice.Name)) txt.Append(port.EndPointDevice.Name);
                    else txt.Append(port.EndPointDevice.Type);
                    if (!string.IsNullOrEmpty(port.EndPointDevice.EthernetType)) txt.Append(", ").Append(port.EndPointDevice.EthernetType);
                    txt.Append(".");
                }
            }
            else txt.Append(".");
            txt.Append("\n\tI have run the ALE PoE wizard and it did not repair the problem.");
            if (device != null)
            {
                txt.Append("\n\tThe switch IP address is ").Append(device.IpAddress).Append(". It is a ").Append(device.Model);
                txt.Append(" model, running ").Append(device.Version).Append(" with serial number ").Append(device.SerialNumber).Append("\n");
            }
            if (WizardReport != null) txt.Append("\nALE PoE wizard attempts that have failed:").Append(WizardReport.Message);
            txt.Append("\n\tThe switch log tech support .tar file is attached.\n\n\t\tThanks.\n");
            Utils.CreateTextFile(filePath, txt);
        }

    }
}
