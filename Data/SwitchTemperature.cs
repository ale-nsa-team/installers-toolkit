﻿using System.Collections.Generic;
using static PoEWizard.Data.Constants;

namespace PoEWizard.Data
{
    public class SwitchTemperature
    {
        public string Device { get; set; }
        public int Current { get; set; }
        public string Range { get; set; }
        public int Threshold { get; set; }
        public int Danger { get; set; }
        public ThresholdType Status { get; set; }

        public SwitchTemperature()
        {
            Device = "";
            Current = 0;
            Range = "";
            Threshold = 0;
            Status = ThresholdType.Unknown;
        }

        public SwitchTemperature(Dictionary<string, string> dict)
        {
            string[] split = (dict.TryGetValue(CHAS_DEVICE, out string s) ? s : "").Trim().Split('/');
            if (split.Length > 1) Device = split[1].Trim();
            Current = Utils.StringToInt((dict.TryGetValue(CURRENT, out s) ? s : "").Trim());
            Range = (dict.TryGetValue(RANGE, out s) ? s : "").Trim();
            Threshold = Utils.StringToInt((dict.TryGetValue(THRESHOLD, out s) ? s : "").Trim());
            Danger = Utils.StringToInt((dict.TryGetValue(DANGER, out s) ? s : "").Trim());
            if (Current <= Threshold) Status = ThresholdType.UnderThreshold;
            else if (Current >= Danger) Status = ThresholdType.Danger;
            else Status = ThresholdType.OverThreshold;
        }
    }
}
