﻿using PoEWizard.Comm;
using PoEWizard.Components;
using PoEWizard.Data;
using PoEWizard.Device;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static PoEWizard.Data.Constants;
using static PoEWizard.Data.RestUrl;

namespace PoEWizard
{
    /// <summary>
    /// Interaction logic for Mainwindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        #region Private Variables
        private readonly ResourceDictionary darkDict;
        private readonly ResourceDictionary lightDict;
        private ResourceDictionary currentDict;
        private readonly string appVersion;
        private readonly IProgress<ProgressReport> progress;
        private bool reportAck;
        private RestApiService restApiService;
        private SftpService sftpService;
        private SwitchModel device;
        private SlotView slotView;
        private PortModel selectedPort;
        private int selectedPortIndex;
        private SlotModel selectedSlot;
        private WizardReport reportResult = new WizardReport();
        private bool isClosing = false;
        private DeviceType selectedDeviceType;
        private string lastIpAddr;
        private string lastPwd;
        #endregion
        #region public variables
        public static Window Instance;
        public static ThemeType theme;
        public static string dataPath;

        #endregion

        #region constructor and initialization
        public MainWindow()
        {
            InitializeComponent();
            lightDict = Resources.MergedDictionaries[0];
            darkDict = Resources.MergedDictionaries[1];
            currentDict = darkDict;
            Instance = this;
            device = new SwitchModel();
            //application info
            Assembly assembly = Assembly.GetExecutingAssembly();
            string version = assembly.GetName().Version.ToString();
            string title = assembly.GetCustomAttribute<AssemblyTitleAttribute>().Title;
            string ale = assembly.GetCustomAttribute<AssemblyCompanyAttribute>().Company;
            appVersion = title + " (V." + string.Join(".", version.Split('.').ToList().Take(2)) + ")";
            this.Title += $" (V {string.Join(".", version.Split('.').ToList().Take(2))})";
            //datapath
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            dataPath = Path.Combine(appData, ale, title);

            // progress report handling
            progress = new Progress<ProgressReport>(report =>
            {
                reportAck = false;
                HideInfoBox();
                switch (report.Type)
                {
                    case ReportType.Status:
                        ShowInfoBox(report.Message);
                        break;
                    case ReportType.Error:
                        reportAck = ShowMessageBox(report.Title, report.Message, MsgBoxIcons.Error);
                        break;
                    case ReportType.Warning:
                        reportAck = ShowMessageBox(report.Title, report.Message, MsgBoxIcons.Warning);
                        break;
                    case ReportType.Info:
                        reportAck = ShowMessageBox(report.Title, report.Message, MsgBoxIcons.Info);
                        break;
                    default:
                        break;
                }
            });
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            SetTitleColor();
            _btnConnect.IsEnabled = false;
        }

        private async void OnWindowClosing(object sender, CancelEventArgs e)
        {
            try
            {
                e.Cancel = true;
                sftpService?.Disconnect();
                await CloseRestApiService();
                this.Closing -= OnWindowClosing;
                this.Close();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        #endregion constructor and initialization

        #region event handlers
        private void SwitchMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Login login = new Login(device.Login)
            {
                Password = device.Password,
                IpAddress = string.IsNullOrEmpty(device.IpAddress) ? lastIpAddr : device.IpAddress,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            if (login.ShowDialog() == true)
            {
                device.Login = login.User;
                device.Password = login.Password;
                device.IpAddress = login.IpAddress;
                Connect();
            }
        }

        private void DisconnectMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Connect();
        }

        private void ConnectBtn_Click(object sender, MouseEventArgs e)
        {
            Connect();
        }

        private void ViewLog_Click(object sender, RoutedEventArgs e)
        {
            TextViewer tv = new TextViewer("Application Log", canClear: true)
            {
                Owner = this,
                Filename = Logger.LogPath,
            };
            tv.Show();
        }

        private async void ViewVcBoot_Click(object sender, RoutedEventArgs e)
        {
            ShowProgress("Loading vcboot.cfg file...");
            Logger.Debug($"Loading vcboot.cfg file from switch {device.Name}");
            string res = string.Empty;
            await Task.Run(() => 
            {
                if (sftpService == null)
                {
                    sftpService = new SftpService(device.IpAddress, device.Login, device.Password);
                }
                sftpService.Connect();
                res = sftpService.DownloadToMemory(VCBOOT_PATH);
                string fname = sftpService.DownloadFile(VCBOOT_PATH);
            });
            HideProgress();
            TextViewer tv = new TextViewer("VCBoot config file", res)
            {
                Owner = this,
                SaveFilename = device.Name + "-vcboot.cfg"
            };
            Logger.Debug("Displaying vcboot file.");
            tv.Show();
        }

        private async void ViewSnapshot_Click(object sender, RoutedEventArgs e)
        {
            ShowProgress("Reading configuration snapshot...");
            await Task.Run(() => restApiService.GetSnapshot());
            HideInfoBox();
            HideProgress();
            TextViewer tv = new TextViewer("Configuration Snapshot", device.ConfigSnapshot)
            {
                Owner = this,
                SaveFilename = device.Name + "-snapshot.txt"
            };
            tv.Show();
        }

        private void ViewPS_Click(object sender, RoutedEventArgs e)
        {
            var ps = new PowerSupply(device)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            ps.Show();
        }

        private void RunWiz_Click(object sender, RoutedEventArgs e)
        {
            var ds = new DeviceSelection(selectedPort.Name)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                DeviceType = selectedDeviceType
            };

            if (ds.ShowDialog() == true)
            {
                selectedDeviceType = ds.DeviceType;
                LaunchPoeWizard(ds.DeviceType);
            }
        }

        private void RefreshSwitch_Click(object sender, RoutedEventArgs e)
        {
            RefreshSwitch();
        }

        private void Help_Click(object sender, RoutedEventArgs e)
        {

        }

        private void LogLevelItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            string level = mi.Header.ToString();
            if (mi.IsChecked) return;
            foreach (MenuItem item in _logLevels.Items)
            {
                item.IsChecked = false;
            }
            mi.IsChecked = true;
            LogLevel lvl = (LogLevel)Enum.Parse(typeof(LogLevel), level);
            Logger.LogLevel = lvl;
        }

        private void ThemeItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            string t = mi.Header.ToString();
            if (mi.IsChecked) return;
            mi.IsChecked = true;
            if (t == "Dark")
            {
                _lightMenuItem.IsChecked = false;
                theme = ThemeType.Dark;
                Resources.MergedDictionaries.Remove(lightDict);
                Resources.MergedDictionaries.Add(darkDict);
                currentDict = darkDict;
            }
            else
            {
                _darkMenuItem.IsChecked = false;
                theme = ThemeType.Light;
                Resources.MergedDictionaries.Remove(darkDict);
                Resources.MergedDictionaries.Add(lightDict);
                currentDict = lightDict;
            }
            if (slotView?.Slots.Count == 1) //do not highlight if only one row
            {
                _slotsView.CellStyle = currentDict["gridCellNoHilite"] as Style;
            }
            SetTitleColor();
            //force color converters to run
            DataContext = null;
            DataContext = device;
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            AboutBox about = new AboutBox
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            about.Show();
        }

        private void SlotSelection_Changed(object sender, RoutedEventArgs e)
        {
            if (_slotsView.SelectedItem is SlotModel slot)
            {
                selectedSlot = slot;
                _portList.ItemsSource = slot.Ports;
            }

        }

        private void PortSelection_Changed(Object sender, RoutedEventArgs e)
        {
            if (_portList.SelectedItem is PortModel port)
            {
                selectedPort = port;
                selectedPortIndex = _portList.SelectedIndex;
                _btnRunWiz.IsEnabled = selectedPort.Poe != PoeStatus.NoPoe;
            }
        }

        private async void Priority_Changed(object sender, SelectionChangedEventArgs e)
        {
            var cb = sender as ComboBox;
            PortModel port = _portList.CurrentItem as PortModel;
            if (cb.SelectedValue.ToString() != port.PriorityLevel.ToString())
            {
                ShowMessageBox("Priority", $"Selected priority: {cb.SelectedValue}");
                PriorityLevelType prevPriority = port.PriorityLevel;
                port.PriorityLevel = (PriorityLevelType)Enum.Parse(typeof(PriorityLevelType), cb.SelectedValue.ToString());
                await SetPoePriority(port, prevPriority);
            }
        }

        private async Task SetPoePriority(PortModel port, PriorityLevelType prevPriority)
        {
            if (port == null) return;
            string txt = $"Changing Priority to {port.PriorityLevel} on port {port.Name}";
            ShowProgress($"{txt}...");
            bool ok = false;
            await Task.Run(() => ok = restApiService.ChangePowerPriority(port.Name, port.PriorityLevel));
            if (ok)
            {
                Logger.Activity($"{txt} completed on Switch {device.Name}, S/N {device.SerialNumber}, Model {device.Model}");
                await WaitAckProgress();
            }
            else
            {
                port.PriorityLevel = prevPriority;
                Logger.Error($"Couldn't change the Priority to {port.PriorityLevel} on port {port.Name} of Switch {device.IpAddress}");
            }
            HideInfoBox();
            HideProgress();
            RefreshSlotAndPortsView();
        }

        private async void FPoE_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CheckBox cb = sender as CheckBox;
                if (selectedSlot == null || !cb.IsKeyboardFocusWithin) return;
                FocusManager.SetFocusedElement(FocusManager.GetFocusScope(cb), null);
                Keyboard.ClearFocus();
                CommandType cmd = (cb.IsChecked == true) ? CommandType.POE_FAST_ENABLE : CommandType.POE_FAST_DISABLE;
                bool res = await SetPerpetualOrFastPoe(cmd);
                if (!res) cb.IsChecked = !cb.IsChecked;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            HideProgress();
            HideInfoBox();
            RefreshSlotAndPortsView();
        }

        private async void PPoE_Click(Object sender, RoutedEventArgs e)
        {
            try
            {
                CheckBox cb = sender as CheckBox;
                if (selectedSlot == null || !cb.IsKeyboardFocusWithin) return;
                FocusManager.SetFocusedElement(FocusManager.GetFocusScope(cb), null);
                Keyboard.ClearFocus();
                CommandType cmd = (cb.IsChecked == true) ? CommandType.POE_PERPETUAL_ENABLE : CommandType.POE_PERPETUAL_DISABLE;
                bool res = await SetPerpetualOrFastPoe(cmd);
                if (!res) cb.IsChecked = !cb.IsChecked;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            HideProgress();
            HideInfoBox();
            RefreshSlotAndPortsView();
        }

        private async Task<bool> SetPerpetualOrFastPoe(CommandType cmd)
        {
            string action = cmd == CommandType.POE_PERPETUAL_ENABLE || cmd == CommandType.POE_FAST_ENABLE ? "Enabling" : "Disabling";
            string poeType = (cmd == CommandType.POE_PERPETUAL_ENABLE || cmd == CommandType.POE_PERPETUAL_DISABLE) ? "Perpetual" : "Fast";
            ShowProgress($"{action} {poeType} PoE on slot {selectedSlot.Name}...");
            bool ok = false;
            await Task.Run(() => ok = restApiService.SetPerpetualOrFastPoe(selectedSlot, cmd));
            Logger.Activity($"{action} {poeType} PoE on slot {selectedSlot.Name} completed on switch {device.Name}, S/N {device.SerialNumber}, model {device.Model}");
            await WaitAckProgress();
            RefreshSlotAndPortsView();
            return ok;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        #endregion event handlers

        #region private methods
        private void SetTitleColor()
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            int bckgndColor = theme == ThemeType.Dark ? 0x333333 : 0xFFFFFF;
            int textColor = theme == ThemeType.Dark ? 0xFFFFFF : 0x000000;
            DwmSetWindowAttribute(handle, 35, ref bckgndColor, Marshal.SizeOf(bckgndColor));
            DwmSetWindowAttribute(handle, 36, ref textColor, Marshal.SizeOf(textColor));
        }

        private async void Connect()
        {
            try
            {
                if (string.IsNullOrEmpty(device.IpAddress))
                {
                    device.IpAddress = lastIpAddr;
                    device.Login = "admin";
                    device.Password = lastPwd;
                }
                restApiService = new RestApiService(device, progress);
                if (device.IsConnected)
                {
                    ShowProgress($"Disconnecting from switch {device.IpAddress}...");
                    await CloseRestApiService();
                    SetDisconnectedState();
                    return;
                }
                ShowProgress($"Connecting to switch {device.IpAddress}...");
                isClosing = false;
                DateTime startTime = DateTime.Now;
                reportResult = new WizardReport();
                await Task.Run(() => restApiService.Connect(reportResult));
                UpdateConnectedState(true);
                await CheckSwitchScanResult($"Connect to switch {device.IpAddress}...", startTime);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            HideProgress();
            HideInfoBox();
        }

        private async Task CloseRestApiService()
        {
            try
            {
                if (restApiService != null && !isClosing)
                {
                    isClosing = true;
                    if (device.SyncStatus == SyncStatusType.NotSynchronized)
                    {
                        if (ShowMessageBox("Write Memory required",
                                "Flash memory is not synchronized\nDo you want to save it now?\nIt will may take up to 30 sec to execute Write Memory.",
                                MsgBoxIcons.Warning, MsgBoxButtons.OkCancel))
                        {
                            _btnRunWiz.IsEnabled = false;
                            _refreshSwitch.IsEnabled = false;
                            _comImg.Visibility = Visibility.Collapsed;
                            await Task.Run(() => restApiService.WriteMemory());
                            _comImg.Visibility = Visibility.Visible;
                        }
                    }
                    restApiService.Close();
                }
                await Task.Run(() => Thread.Sleep(250)); //needed for the closing event handler
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            HideProgress();
            HideInfoBox();
        }

        private async void LaunchPoeWizard(DeviceType deviceType)
        {
            try
            {
                ProgressReport wizardProgressReport = new ProgressReport("PoE Wizard Report:");
                reportResult = new WizardReport();
                ShowProgress($"Running PoE Wizard on port {selectedPort.Name}...");
                DateTime startTime = DateTime.Now;
                await Task.Run(() => restApiService.ScanSwitch());
                UpdateConnectedState(false);
                switch (deviceType)
                {
                    case DeviceType.Camera:
                        await RunWizardCamera();
                        break;
                    case DeviceType.Phone:
                        await RunWizardTelephone();
                        break;
                    case DeviceType.AP:
                        await RunWizardWirelessLan();
                        break;
                    default:
                        await RunWizardOther();
                        break;
                }
                await Task.Run(() => restApiService.RefreshSwitchPorts());
                WizardResult result = reportResult.GetReportResult(selectedPort.Name);
                if (result == WizardResult.NothingToDo || result == WizardResult.Fail) await RunLastWizardActions();
                string msg = $"{reportResult.Message}\n\nTotal duration: {Utils.CalcStringDuration(startTime, true)}";
                if (!string.IsNullOrEmpty(reportResult.Message))
                {
                    wizardProgressReport.Title = "PoE Wizard Report:";
                    wizardProgressReport.Type = reportResult.GetReportResult(selectedPort.Name) == WizardResult.Fail ? ReportType.Error : ReportType.Info;
                    wizardProgressReport.Message = msg;
                    progress.Report(wizardProgressReport);
                    await WaitAckProgress();
                }
                StringBuilder txt = new StringBuilder("PoE Wizard completed on port ");
                txt.Append(selectedPort.Name).Append(" with device type ").Append(deviceType).Append(":").Append(msg).Append("\nPoE status: ").Append(selectedPort.Poe);
                txt.Append(", Port Status: ").Append(selectedPort.Status).Append(", Power: ").Append(selectedPort.Power).Append(" Watts");
                if (selectedPort.EndPointDevice != null) txt.Append("\n").Append(selectedPort.EndPointDevice);
                else if (selectedPort.MacList?.Count > 0 && !string.IsNullOrEmpty(selectedPort.MacList[0])) txt.Append(", Device MAC: ").Append(selectedPort.MacList[0]);
                Logger.Activity(txt.ToString());
                RefreshSlotAndPortsView();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            HideProgress();
            HideInfoBox();
        }

        private void RefreshSlotAndPortsView()
        {
            _slotsView.ItemsSource = null;
            _portList.ItemsSource = null;
            selectedSlot = device.GetSlot(selectedSlot.Name);
            _slotsView.ItemsSource = device.GetChassis(selectedSlot.Name)?.Slots ?? new List<SlotModel>();
            _portList.ItemsSource = selectedSlot?.Ports ?? new List<PortModel>();
            ReselectPort();
        }

        private async void RefreshSwitch()
        {
            try
            {
                DateTime startTime = DateTime.Now;
                reportResult = new WizardReport();
                await Task.Run(() => restApiService.ScanSwitch(reportResult));
                UpdateConnectedState(false);
                await CheckSwitchScanResult($"Refresh switch {device.IpAddress}", startTime);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            HideProgress();
            HideInfoBox();
        }

        private async Task CheckSwitchScanResult(string title, DateTime startTime)
        {
            string duration = Utils.CalcStringDuration(startTime, true);
            if (reportResult.Result?.Count < 1) return;
            WizardResult result = reportResult.GetReportResult(SWITCH);
            if (result == WizardResult.Fail || result == WizardResult.Warning)
            {
                progress.Report(new ProgressReport(title) { Title = title, Type = ReportType.Error, Message = $"{reportResult.Message}" });
                await WaitAckProgress();
            }
            else if (reportResult.Result?.Count > 0)
            {
                int resetSlotCnt = 0;
                foreach (var reports in reportResult.Result)
                {
                    List<ReportResult> reportList = reports.Value as List<ReportResult>;
                    if (reportList?.Count > 0)
                    {
                        ReportResult report = reportList[reportList.Count - 1];
                        string alertMsg = $"{report.AlertDescription}\nDo you want to turn it On?";
                        if (report?.Result == WizardResult.Warning && ShowMessageBox($"Slot {report.ID} warning", alertMsg, MsgBoxIcons.Warning, MsgBoxButtons.OkCancel))
                        {
                            await Task.Run(() => restApiService.RunPowerUpSlot(report.ID));
                            resetSlotCnt++;
                            Logger.Debug($"{report}\nSlot {report.ID} turned On");
                        }
                    }
                }
                if (resetSlotCnt > 0)
                {
                    await Task.Run(() => WaitTask(20, $"Waiting Ports to come UP on Switch {device.IpAddress}"));
                    await Task.Run(() => restApiService.RefreshSwitchPorts());
                    RefreshSlotAndPortsView();
                }
            }
            Logger.Debug($"{title} completed (duration: {duration})");
        }

        private async Task RunWizardCamera()
        {
            await Enable823BT();
            if (IsWizardStopped()) return;
            await EnableHdmiMdi();
            if (IsWizardStopped()) return;
            if (IsWizardStopped()) return;
            await ChangePriority();
            if (IsWizardStopped()) return;
            await Enable2PairPower();
            if (IsWizardStopped()) return;
            await EnableCapacitorDetection();
        }

        private async Task RunWizardTelephone()
        {
            await Enable2PairPower();
            if (IsWizardStopped()) return;
            await ChangePriority();
            if (IsWizardStopped()) return;
            await EnableHdmiMdi();
            if (IsWizardStopped()) return;
            await Enable823BT();
            if (IsWizardStopped()) return;
            await EnableCapacitorDetection();
        }

        private async Task RunWizardWirelessLan()
        {
            await Enable823BT();
            if (IsWizardStopped()) return;
            await Enable2PairPower();
            if (IsWizardStopped()) return;
            await EnableHdmiMdi();
            if (IsWizardStopped()) return;
            await ChangePriority();
        }

        private async Task RunWizardOther()
        {
            await ChangePriority();
            if (IsWizardStopped()) return;
            await Enable823BT();
            if (IsWizardStopped()) return;
            await EnableHdmiMdi();
            if (IsWizardStopped()) return;
            await Enable2PairPower();
            if (IsWizardStopped()) return;
            await EnableCapacitorDetection();
        }

        private async Task Enable823BT()
        {
            await RunPoeWizard(new List<CommandType>() { CommandType.CHECK_823BT });
            if (reportResult.GetReportResult(selectedPort.Name) == WizardResult.Skip) return;
            if (reportResult.GetReportResult(selectedPort.Name) == WizardResult.Warning)
            {
                string alertDescription = reportResult.GetAlertDescription(selectedPort.Name);
                string msg = !string.IsNullOrEmpty(alertDescription) ? alertDescription : "To enable 802.3.bt all devices on the same slot will restart";
                if (!ShowMessageBox("Enable 802.3.bt", $"{msg}\nDo you want to proceed?", MsgBoxIcons.Warning, MsgBoxButtons.OkCancel))
                    return;
            }
            await RunPoeWizard(new List<CommandType>() { CommandType.POWER_823BT_ENABLE });
            Logger.Debug($"Enable 802.3.bt on port {selectedPort.Name} completed on switch {device.Name}, S/N {device.SerialNumber}, model {device.Model}");
        }

        private async Task Enable2PairPower()
        {
            await RunPoeWizard(new List<CommandType>() { CommandType.POWER_2PAIR_PORT }, 30);
            Logger.Debug($"Enable 2-Pair Power on port {selectedPort.Name} completed on switch {device.Name}, S/N {device.SerialNumber}, model {device.Model}");
        }

        private async Task EnableCapacitorDetection()
        {
            await RunPoeWizard(new List<CommandType>() { CommandType.CAPACITOR_DETECTION_ENABLE });
            Logger.Debug($"Enable Capacitor Detection on port {selectedPort.Name} completed on switch {device.Name}, S/N {device.SerialNumber}, model {device.Model}");
        }

        private async Task ChangePriority()
        {
            await RunPoeWizard(new List<CommandType>() { CommandType.CHECK_POWER_PRIORITY });
            if (reportResult.GetReportResult(selectedPort.Name) == WizardResult.Skip) return;
            if (reportResult.GetReportResult(selectedPort.Name) == WizardResult.Warning)
            {
                string alert = reportResult.GetAlertDescription(selectedPort.Name);
                if (!ShowMessageBox("Power Priority Change",
                                    $"{(!string.IsNullOrEmpty(alert) ? $"{alert}" : "")}\nSome other devices with lower priority may stop\nDo you want to proceed?",
                                    MsgBoxIcons.Warning, MsgBoxButtons.OkCancel)) return;
            }
            await RunPoeWizard(new List<CommandType>() { CommandType.POWER_PRIORITY_PORT });
            Logger.Debug($"Change Power Priority on port {selectedPort.Name} completed on switch {device.Name}, S/N {device.SerialNumber}, model {device.Model}");
        }

        private async Task EnableHdmiMdi()
        {
            await RunPoeWizard(new List<CommandType>() { CommandType.POWER_HDMI_ENABLE, CommandType.LLDP_POWER_MDI_ENABLE, CommandType.LLDP_EXT_POWER_MDI_ENABLE }, 15);
            Logger.Debug($"Enable Power over HDMI on port {selectedPort.Name} completed on switch {device.Name}, S/N {device.SerialNumber}, model {device.Model}");
        }

        private async Task RunPoeWizard(List<CommandType> cmdList, int waitSec = 15)
        {
            await Task.Run(() => restApiService.RunPoeWizard(selectedPort.Name, reportResult, cmdList, waitSec));
        }

        private async Task RunLastWizardActions()
        {
            bool reset = false;
            WizardResult result = reportResult.GetReportResult(selectedPort.Name);
            if (result == WizardResult.NothingToDo) reportResult.RemoveLastWizardReport(selectedPort.Name);
            if (selectedPort.Poe == PoeStatus.Searching)
            {
                await RunWizardCommands(new List<CommandType>() { CommandType.CAPACITOR_DETECTION_ENABLE }, 45);
                if (reportResult.GetReportResult(selectedPort.Name) != WizardResult.Ok && result != WizardResult.Fail) reportResult.RemoveLastWizardReport(selectedPort.Name);
                reset = true;
            }
            await CheckDefaultMaxPower();
            if (selectedPort.Poe == PoeStatus.Off && (ShowMessageBox("Port PoE turned Off", $"The PoE on port {selectedPort.Name} is turned Off!\n Do you want to turn it On?",
                                                                     MsgBoxIcons.Warning, MsgBoxButtons.OkCancel)))
            {
                await RunWizardCommands(new List<CommandType>() { CommandType.RESET_POWER_PORT }, 30);
                Logger.Info($"PoE turned Off, reset power on port {selectedPort.Name} completed on switch {device.Name}, S/N {device.SerialNumber}, model {device.Model}");
                reset = true;
            }
            if (!reset && ShowMessageBox("Resetting Port", $"Do you want to recycle the power on port {selectedPort.Name}?", MsgBoxIcons.Warning, MsgBoxButtons.OkCancel))
            {
                await RunWizardCommands(new List<CommandType>() { CommandType.RESET_POWER_PORT }, 30);
                Logger.Info($"Recycling the power on port {selectedPort.Name} completed on switch {device.Name}, S/N {device.SerialNumber}, model {device.Model}");
            }
            reportResult.UpdateResult(selectedPort.Name, result);
        }

        private async Task CheckDefaultMaxPower()
        {
            await RunWizardCommands(new List<CommandType>() { CommandType.CHECK_MAX_POWER });
            if (reportResult.GetReportResult(selectedPort.Name) == WizardResult.Warning)
            {
                string alert = reportResult.GetAlertDescription(selectedPort.Name);
                string msg = !string.IsNullOrEmpty(alert) ? alert : $"Changing Max. Power on port {selectedPort.Name} to default";
                if (ShowMessageBox("Check default Max. Power", $"{msg}\nDo you want to change?", MsgBoxIcons.Warning, MsgBoxButtons.OkCancel))
                {
                    await RunWizardCommands(new List<CommandType>() { CommandType.CHANGE_MAX_POWER });
                }
            }
        }

        private async Task RunWizardCommands(List<CommandType> cmdList, int waitSec = 15)
        {
            await Task.Run(() => restApiService.RunWizardCommands(selectedPort.Name, reportResult, cmdList, waitSec));
        }

        private bool IsWizardStopped()
        {
            return reportResult.GetReportResult(selectedPort.Name) == WizardResult.Ok || reportResult.GetReportResult(selectedPort.Name) == WizardResult.NothingToDo;
        }

        private void WaitTask(int waitTime, string txt)
        {
            DateTime startTime = DateTime.Now;
            int dur = 0;
            progress.Report(new ProgressReport($"{txt} ..."));
            while (dur < waitTime)
            {
                Thread.Sleep(1000);
                dur = (int)Utils.GetTimeDuration(startTime);
                progress.Report(new ProgressReport($"{txt} ({dur} sec) ..."));
            }
        }

        private async Task WaitAckProgress()
        {
            await Task.Run(() =>
            {
                DateTime startTime = DateTime.Now;
                while (!reportAck)
                {
                    if (Utils.GetTimeDuration(startTime) > 120) break;
                    Thread.Sleep(100);
                }
            });
        }

        private bool ShowMessageBox(string title, string message, MsgBoxIcons icon = MsgBoxIcons.Info, MsgBoxButtons buttons = MsgBoxButtons.Ok)
        {
            _infoBox.Visibility = Visibility.Collapsed;
            CustomMsgBox msgBox = new CustomMsgBox(this)
            {
                Header = title,
                Message = message,
                Img = icon,
                Buttons = buttons
            };
            return (bool)msgBox.ShowDialog();
        }

        private void ShowInfoBox(string message)
        {
            _infoBlock.Inlines.Clear();
            _infoBlock.Inlines.Add(message);
            _infoBox.Visibility = Visibility.Visible;
        }

        private void ShowProgress(string message)
        {
            _progressBar.Visibility = Visibility.Visible;
            _status.Text = message;

        }

        private void HideProgress()
        {
            _progressBar.Visibility = Visibility.Hidden;
            _status.Text = DEFAULT_APP_STATUS;
        }

        private void HideInfoBox()
        {
            _infoBox.Visibility = Visibility.Collapsed;
        }

        private void UpdateConnectedState(bool checkCertified)
        {
            if (device.IsConnected)
            {
                Logger.Activity($"Connected to switch {device.Name}, S/N {device.SerialNumber}, model {device.Model}");
                SetConnectedState(checkCertified);
            }
            else
            {
                Logger.Activity($"Switch S/N {device.SerialNumber}, model {device.Model} Disconnected");
                SetDisconnectedState();
            }
        }

        private async void SetConnectedState(bool checkCertified)
        {
            DataContext = null;
            DataContext = device;
            Logger.Debug($"Data context set to {device.Name}");
            if (device.RunningDir == CERTIFIED_DIR && checkCertified)
            {
                string msg = $"The switch booted on {CERTIFIED_DIR} directory, no changes can be saved.\n" +
                    $"Do you want to reboot the switch on {WORKING_DIR} directory?";
                bool res = ShowMessageBox("Connection", msg, MsgBoxIcons.Warning, MsgBoxButtons.OkCancel);
                if (res)
                {
                    await RebootSwitch(600);
                    SetDisconnectedState();
                    return;
                }
            }
            _comImg.Source = (ImageSource)currentDict["connected"];
            _switchAttributes.Text = $"Connected to: {device.Name}";
            _btnConnect.Cursor = Cursors.Hand;
            _switchMenuItem.IsEnabled = false;
            _snapshotMenuItem.IsEnabled = true;
            _vcbootMenuItem.IsEnabled = true;
            _refreshSwitch.IsEnabled = true;
            _psMenuItem.IsEnabled = true;
            _disconnectMenuItem.Visibility = Visibility.Visible;
            _tempStatus.Visibility = Visibility.Visible;
            _cpu.Visibility = Visibility.Visible;
            slotView = new SlotView(device);
            _slotsView.ItemsSource = slotView.Slots;
            Logger.Debug($"Slots view items source: {slotView.Slots.Count} slot(s)");
            _slotsView.SelectedIndex = 0;
            if (slotView.Slots.Count == 1) //do not highlight if only one row
            {
                _slotsView.CellStyle = currentDict["gridCellNoHilite"] as Style;
            }
            _slotsView.Visibility = Visibility.Visible;
            _portList.Visibility = Visibility.Visible;
            _fpgaLbl.Visibility = string.IsNullOrEmpty(device.Fpga) ? Visibility.Collapsed : Visibility.Visible;
            _cpldLbl.Visibility = string.IsNullOrEmpty(device.Cpld) ? Visibility.Collapsed : Visibility.Visible;
            _btnConnect.IsEnabled = true;
            _comImg.ToolTip = "Click to disconnect";
            if (device.TemperatureStatus == ThresholdType.Danger)
            {
                _tempWarn.Source = new BitmapImage(new Uri(@"Resources\danger.png", UriKind.Relative));
            }
            else
            {
                _tempWarn.Source = new BitmapImage(new Uri(@"Resources\warning.png", UriKind.Relative));
            }
            ReselectPort();
        }

        private void SetDisconnectedState()
        {
            _comImg.Source = (ImageSource)currentDict["disconnected"];
            lastIpAddr = device.IpAddress;
            lastPwd = device.Password;
            DataContext = null;
            device = new SwitchModel();
            _switchAttributes.Text = "";
            _btnRunWiz.IsEnabled = false;
            _switchMenuItem.IsEnabled = true;
            _snapshotMenuItem.IsEnabled = false;
            _vcbootMenuItem.IsEnabled = false;
            _refreshSwitch.IsEnabled = false;
            _psMenuItem.IsEnabled = false;
            _comImg.ToolTip = "Click to reconnect";
            _disconnectMenuItem.Visibility = Visibility.Collapsed;
            _tempStatus.Visibility = Visibility.Hidden;
            _cpu.Visibility = Visibility.Hidden;
            _slotsView.Visibility= Visibility.Hidden;
            _portList.Visibility= Visibility.Hidden;
            _fpgaLbl.Visibility = Visibility.Visible;
            _cpldLbl.Visibility = Visibility.Collapsed;
            selectedPort = null;
            selectedPortIndex = -1;
            DataContext = device;
            restApiService = null;
        }

        private void ReselectPort()
        {
            if (selectedPort != null)
            {
                _portList.SelectionChanged -= PortSelection_Changed;
                _portList.SelectedItem = _portList.Items[selectedPortIndex];
                _portList.SelectionChanged += PortSelection_Changed;
            }
        }

        private async Task RebootSwitch(int waitSec)
        {
            string duration = "";
            await Task.Run(() => duration = restApiService.RebootSwitch(waitSec));
            string txt = $"Switch {device.IpAddress} ready to connect";
            if (!string.IsNullOrEmpty(duration)) txt += $"\nReboot duration: {duration}";
            ShowMessageBox("Connection", txt, MsgBoxIcons.Info, MsgBoxButtons.Ok);
        }

        #endregion private methods
    }
}
