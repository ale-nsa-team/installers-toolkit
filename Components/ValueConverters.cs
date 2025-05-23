﻿using PoEWizard.Data;
using PoEWizard.Device;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using static PoEWizard.Data.Constants;
using static PoEWizard.Data.Utils;

namespace PoEWizard.Components
{
    internal static class Colors
    {
        internal static SolidColorBrush Danger => (SolidColorBrush)new BrushConverter().ConvertFrom("#ff6347");
        internal static SolidColorBrush Clear => MainWindow.Theme == ThemeType.Dark ? Brushes.Lime
                        : (SolidColorBrush)new BrushConverter().ConvertFrom("#12b826");
        internal static SolidColorBrush Warn => Brushes.Orange;
        internal static SolidColorBrush Unknown => Brushes.Gray;
        internal static SolidColorBrush Disable => (SolidColorBrush)new BrushConverter().ConvertFrom("#aaa");
        internal static SolidColorBrush Problem => MainWindow.Theme == ThemeType.Dark
            ? (SolidColorBrush)new BrushConverter().ConvertFrom("#C29494") : Brushes.Orchid;
        internal static SolidColorBrush Default => MainWindow.Theme == ThemeType.Dark ? Brushes.White : Brushes.Black;
    }

    public class RectangleValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                float val = ParseFloat(value);
                return val > 45 ? val - 45 : val;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (IsInvalid(value)) return false;
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Visibility v = bool.Parse(parameter?.ToString() ?? "true") ? Visibility.Visible : Visibility.Collapsed;
            Visibility notv = v == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            return bool.Parse(value.ToString()) ? v : notv;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public class ValueToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {

            try
            {
                if (IsInvalid(value)) return Colors.Default;
                string val = value?.ToString() ?? string.Empty;
                string param = parameter?.ToString() ?? string.Empty;
                switch (param)
                {
                    case "ConnectionStatus":
                        return val == "Reachable" ? Colors.Clear : Colors.Danger;
                    case "Temperature":
                        return val == "UnderThreshold" ? Colors.Clear : val == "OverThreshold" ? Colors.Warn : Colors.Danger;
                    case "Power":
                        float percent = ParseFloat(value);
                        return percent > 10 ? Colors.Clear : (percent > 0 ? Colors.Warn : Colors.Danger);
                    case "Poe":
                        switch (val)
                        {
                            case "On":
                                return Colors.Clear;
                            case "Fault":
                            case "Deny":
                            case "Conflict":
                                return Colors.Danger;
                            case "Searching":
                                return Colors.Problem;
                            case "Off":
                                return Colors.Warn;
                            default:
                                return Colors.Disable;
                        }
                    case "PoeStatus":
                        return val == "UnderThreshold" ? Colors.Clear : val == "NearThreshold" ? Colors.Warn : Colors.Danger;
                    case "PortStatus":
                        return val == "Up" ? Colors.Clear :
                               val == "Down" ? Colors.Danger :
                               val == "Blocked" ? Colors.Warn : Colors.Unknown;
                    case "PowerSupply":
                        return val == "Up" ? Colors.Clear : Colors.Danger;
                    case "RunningDir":
                        return val == CERTIFIED_DIR ? Colors.Danger : Colors.Default;
                    case "Boolean":
                        return val.ToLower() == "true" ? Colors.Clear : Colors.Danger;
                    case "AosVersion":
                        return IsOldAosVersion(val) ? Colors.Warn : Colors.Default;
                    case "SyncStatus":
                        return val == SyncStatusType.Synchronized.ToString() ? Colors.Clear :
                               val == SyncStatusType.NotSynchronized.ToString() ? Colors.Warn :
                               val == SyncStatusType.Unknown.ToString() ? Colors.Unknown : Colors.Danger;
                    case "LpCmmDebugLevel":
                    case "LpNiDebugLevel":
                        SwitchDebugLogLevel level = StringToSwitchDebugLevel(val);
                        return level == SwitchDebugLogLevel.Info ? Colors.Clear :
                               level == SwitchDebugLogLevel.Invalid ? Colors.Danger :
                               level == SwitchDebugLogLevel.Off ? Colors.Danger :
                               level > SwitchDebugLogLevel.Off && level < SwitchDebugLogLevel.Warn ? Colors.Problem :
                               level >= SwitchDebugLogLevel.Warn && level < SwitchDebugLogLevel.Info ? Colors.Warn :
                               level >= SwitchDebugLogLevel.Debug1 ? Colors.Danger : Colors.Problem;
                    case "PortSpeed":
                        return val == "1000" ? Colors.Default : Colors.Warn;
                    default:
                        return Colors.Unknown;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            return Colors.Unknown;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public class ValueToStyleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Style defStyle = MainWindow.Instance.FindResource("devToolTip") as Style;
            if (IsInvalid(value)) return defStyle;
            int val = int.TryParse(value.ToString(), out val) ? val : 0;
            if (val > 0)
            {
                return MainWindow.Instance.FindResource("clickable") as Style;
            }
            else
            {
                return defStyle;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public class FpgaToColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (IsInvalid(values)) return Colors.Default;
                string versionType = parameter?.ToString() ?? FPGA;
                string model = values[0].ToString();
                if (model.Contains("(")) model = RemoveMasterSlave(model);
                string versions = values[1].ToString();
                if (versions.Contains("(")) versions = RemoveMasterSlave(versions);
                if (string.IsNullOrEmpty(versions)) return Colors.Unknown;
                int[] minversion = GetMinimunVersion(model, versionType);
                if (minversion == null) return Colors.Default;
                string[] s = versions.Split('.');
                int[] fpgas = Array.ConvertAll(s, int.Parse);
                return ((fpgas[0] < minversion[0]) || (fpgas[0] == minversion[0] && fpgas[1] < minversion[1])) ? Colors.Warn : Colors.Default;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            return Colors.Default;
        }

        private static string RemoveMasterSlave(string prop)
        {
            string[] split = prop.Split('(');
            if (split.Length > 1 && !string.IsNullOrEmpty(split[0])) prop = split[0].Trim();
            return prop;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            string[] splitValues = (value?.ToString() ?? string.Empty).Split(' ');
            return splitValues;
        }
    }

    public class BadVersionToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            SolidColorBrush val = new FpgaToColorConverter().Convert(values, targetType, parameter, culture) as SolidColorBrush;
            return val == Colors.Warn ? Visibility.Visible : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            string[] splitValues = (value?.ToString() ?? string.Empty).Split(' ');
            return splitValues;
        }
    }

    public class ConfigTypeToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (IsInvalid(value)) return false;
                string val = value?.ToString() ?? string.Empty;
                string par = parameter?.ToString() ?? string.Empty;
                ConfigType ct = Enum.TryParse(val, true, out ConfigType c) ? c : ConfigType.Unavailable;
                return par == "Available" ? ct != ConfigType.Unavailable : ct == ConfigType.Enable;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public class ConfigTypeToSimbolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (IsInvalid(value)) return string.Empty;
                string val = value?.ToString() ?? string.Empty;
                ConfigType ct = Enum.TryParse(val, true, out ConfigType c) ? c : ConfigType.Unavailable;
                switch (ct)
                {
                    case ConfigType.Enable: return "✓";
                    case ConfigType.Disable: return "✗";
                    default: return string.Empty;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public class AosVersionToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (IsInvalid(value)) return Visibility.Collapsed;
                return IsOldAosVersion(value) ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Visibility.Collapsed;
            }

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public class TempStatusToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (IsInvalid(value)) return Visibility.Collapsed;
                return value.ToString() == "OverThreshold" || value.ToString() == "Danger" ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public class BoolToSymbolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (IsInvalid(value)) return string.Empty;
                bool val = bool.TryParse(value.ToString(), out bool b) && b;
                return val ? "✓" : "✗";
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public class BoolToPoEModeConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (IsInvalid(values)) return string.Empty;
                string val = values[0].ToString();
                PoeStatus poeType = string.IsNullOrEmpty(val) || val.Contains("UnsetValue") ? PoeStatus.NoPoe : (PoeStatus)Enum.Parse(typeof(PoeStatus), val);
                bool is4pair = !bool.TryParse(values[1].ToString(), out bool b) || b;
                return poeType != PoeStatus.NoPoe ? (is4pair ? "4-pair" : "2-pair") : string.Empty;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return string.Empty;
            }

        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            string[] splitValues = (value?.ToString() ?? string.Empty).Split(' ');
            return splitValues;
        }
    }

    public class PoeToPriorityLevelConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (IsInvalid(values)) return PriorityLevelType.Low;
                string val = values[0].ToString();
                PoeStatus poeType = string.IsNullOrEmpty(val) || val.Contains("UnsetValue") ? PoeStatus.NoPoe : (PoeStatus)Enum.Parse(typeof(PoeStatus), val);
                string priorityLevel = values[1].ToString();
                return poeType != PoeStatus.NoPoe ? Enum.Parse(typeof(PriorityLevelType), priorityLevel) : PriorityLevelType.Low;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return PriorityLevelType.Low;
            }

        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            string[] splitValues = (value?.ToString() ?? string.Empty).Split(' ');
            return splitValues;
        }
    }

    public class CpuUsageToColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (IsInvalid(values)) return Colors.Default;
                double pct = GetThresholdPercentage(values);
                return pct > 0.1 ? Colors.Clear : pct < 0 ? Colors.Danger : Colors.Warn;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Colors.Unknown;
            }

        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            string[] splitValues = (value?.ToString() ?? string.Empty).Split(' ');
            return splitValues;
        }
    }

    public class CpuUsageToToolTipConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (IsInvalid(values)) return null;
                int thrshld = int.TryParse(values[1].ToString(), out int i) ? i : 0;
                double pct = GetThresholdPercentage(values);
                return pct > 0.1 ? string.Empty : pct < 0 ? $"CPU usage over threshold ({thrshld}%)" : $"CPU usage near threshold ({thrshld}%)";
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }

        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            string[] splitValues = (value?.ToString() ?? string.Empty).Split(' ');
            return splitValues;
        }
    }

    public class CpuUsageToVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (IsInvalid(values)) return Visibility.Collapsed;
                double pct = GetThresholdPercentage(values);
                return pct > 0.1 ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return Visibility.Collapsed;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            string[] splitValues = (value?.ToString() ?? string.Empty).Split(' ');
            return splitValues;
        }
    }

    public class SnmpVersionToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (IsInvalid(value) || IsInvalid(parameter)) return Visibility.Collapsed;
            string item = value as string;
            string par = parameter as string;
            return item.Contains(par) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public class StringToTooltipEnableConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (IsInvalid(value)) return false;
            string item = value as string;
            return !string.IsNullOrEmpty(item);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public class DeviceToTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (IsInvalid(value)) return null;
                if (value is List<EndPointDeviceModel> edmList)
                {
                    if (edmList.Count < 1) return null;
                    bool hasmore = edmList.Count > MAX_NB_DEVICES_TOOL_TIP;
                    List<EndPointDeviceModel> displayList = hasmore ? edmList.GetRange(0, MAX_NB_DEVICES_TOOL_TIP) : edmList;
                    List<string> tooltip = displayList.Select(x => x.ToTooltip()).ToList();
                    int maxlen = tooltip.Max(t => MaxLineLen(t));
                    if (hasmore) tooltip.Add($"{new string(' ', maxlen / 2 - 6)}({edmList.Count - MAX_NB_DEVICES_TOOL_TIP} more...)");
                    string separator = $"\n{new string(UNDERLINE, maxlen)}\n\n";
                    return string.Join(separator, tooltip);
                }
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public class IpAddrListToTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (IsInvalid(value)) return null;
                if (value is Dictionary<string, string> ipList)
                {
                    if (ipList.Count < 1) return null;
                    List<string> tooltip = ipList.Select(kvp => $"{kvp.Key}  {kvp.Value}").ToList();
                    int maxlen = tooltip.Max(t => MaxLineLen(t));
                    tooltip.Insert(0, "MAC                IP");
                    tooltip.Insert(1, new string('-', maxlen));
                    return string.Join("\n", tooltip);
                }
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public class IpAddrListFilterToTooltipConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (IsInvalid(values[0])) return DependencyProperty.UnsetValue;
            try
            {
                string srcText = IsInvalid(values[1]) ? string.Empty : values[1].ToString();
                if (values[0] is Dictionary<string, string> ipList)
                {
                    List<string> tooltip;
                    if (IsValidPartialIp(srcText))
                    {
                        tooltip = ipList.Where(kvp => kvp.Value.StartsWith(srcText)).Select(kvp => $"{kvp.Key}  {kvp.Value}").ToList();
                    }
                    else if (IsValidPartialMac(srcText))
                    {
                        tooltip = ipList.Where(kvp => kvp.Key.StartsWith(srcText)).Select(kvp => $"{kvp.Key}  {kvp.Value}").ToList();
                    }
                    else
                    {
                        tooltip = ipList.Select(kvp => $"{kvp.Key}:{kvp.Value}").ToList();
                    }

                    int maxlen = tooltip.Max(t => MaxLineLen(t));
                    tooltip.Insert(0, "MAC                IP");
                    tooltip.Insert(1, new string('-', maxlen));
                    return string.Join("\n", tooltip);
                }

            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            return DependencyProperty.UnsetValue;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DeviceFilterToTooltipConverter : IMultiValueConverter
    {

        private string searchText = string.Empty;

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (IsInvalid(values[0])) return null;
                List<EndPointDeviceModel> epdList = values[0] as List<EndPointDeviceModel>;
                if (epdList.Count < 1) return DependencyProperty.UnsetValue;
                searchText = IsInvalid(values[1]) ? string.Empty : values[1].ToString();
                SearchType searchType = IsValidPartialMac(searchText) ? SearchType.Mac : IsValidPartialIp(searchText) ? SearchType.Ip : SearchType.Name;
                List<EndPointDeviceModel> foundList = new List<EndPointDeviceModel>();
                switch (searchType)
                {
                    case SearchType.Mac:
                        foundList = epdList.FindAll(epd => epd.MacAddress.Split(',').Any(ma => ma.StartsWith(searchText, StringComparison.CurrentCultureIgnoreCase)));
                        break;
                    case SearchType.Name:
                        foundList = epdList.FindAll(epd =>
                            epd.Name.IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                            epd.Vendor.IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                            GetVendorNames(epd.MacAddress).Any(s => s.IndexOf(searchText, StringComparison.CurrentCultureIgnoreCase) >= 0));
                        break;
                    default:
                        foundList = epdList;
                        break;
                }
                List<string> tooltip = new List<string>();
                foreach (EndPointDeviceModel dev in foundList)
                {
                    string tip = dev.ToFilterTooltip(searchType, searchText);
                    if (!string.IsNullOrEmpty(tip)) tooltip.Add(tip);
                }
                int maxlen = tooltip.Max(t => MaxLineLen(t));
                string separator = $"\n{new string(UNDERLINE, maxlen)}\n\n";
                return string.Join(separator, tooltip);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return DependencyProperty.UnsetValue;
            }

        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            string[] splitValues = (value?.ToString() ?? string.Empty).Split(' ');
            return splitValues;
        }

        private int MaxLineLen(string s)
        {
            return s.Split('\n').Max(l => l.Length);
        }

    }

    public class PoeToTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (IsInvalid(value)) return DependencyProperty.UnsetValue;
                PoeStatus poe = (PoeStatus)value;

                switch (poe)
                {
                    case PoeStatus.On:
                        return Translate("i18n_on_tt");
                    case PoeStatus.Off:
                        return Translate("i18n_off_tt");
                    case PoeStatus.Searching:
                        return Translate("i18n_srch_tt");
                    case PoeStatus.Fault:
                        return Translate("i18n_fault_tt");
                    case PoeStatus.Deny:
                        return Translate("i18n_deny_tt");
                    case PoeStatus.Delayed:
                        return Translate("i18n_dld_tt");
                    case PoeStatus.Conflict:
                        return Translate("i18n_conf_tt");
                    case PoeStatus.NoPoe:
                        return Translate("i18n_nop_tt");
                    default:
                        return DependencyProperty.UnsetValue;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

    public class StatusToTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (IsInvalid(value)) return DependencyProperty.UnsetValue;
                PortStatus status = (PortStatus)value;
                if (status == PortStatus.Blocked) return Translate("i18n_ptBlock");
                return DependencyProperty.UnsetValue;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (IsInvalid(value)) return DependencyProperty.UnsetValue;
                if (value is List<string> strList)
                {
                    if (strList.Count < 1) return DependencyProperty.UnsetValue;
                    bool hasmore = strList.Count > MAX_NB_DEVICES_TOOL_TIP;
                    List<string> displayList = hasmore ? strList.GetRange(0, MAX_NB_DEVICES_TOOL_TIP) : strList;
                    int maxlen = displayList.Max(t => MaxLineLen(t));
                    if (hasmore) displayList.Add(" ...");
                    return string.Join(",", displayList);
                }
                else if (value is string sVal)
                {
                    if (!string.IsNullOrEmpty(sVal)) return sVal;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }

        private int MaxLineLen(string s)
        {
            return s.Split('\n').Max(l => l.Length);
        }
    }

    public class PortToTooltipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (IsInvalid(value)) return DependencyProperty.UnsetValue;
                if (!(value is List<string>)) return DependencyProperty.UnsetValue;
                List<string> tooltip = value as List<string>;
                return string.Join("\n", tooltip);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }

        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }

}
