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
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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
        private SwitchModel device;
        private SlotView slotView;
        private string selectedPort;
        private SlotModel selectedSlot;
        private ProgressReportResult reportResult = new ProgressReportResult();
        private bool isClosing = false;
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
        }

        private async void OnWindowClosing(object sender, CancelEventArgs e)
        {
            try
            {
                e.Cancel = true;
                await CloseRestApiService();
                this.Closing -= OnWindowClosing;
                this.Close();
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message + ":\n" + ex.StackTrace);
            }
        }

        #endregion constructor and initialization

        #region event handlers
        private void SwitchMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Login login = new Login(device.Login)
            {
                Password = device.Password,
                IpAddress = device.IpAddress,
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

        private void ViewActivity_Click(object sender, RoutedEventArgs e)
        {

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

        private async void ViewSnapshot_Click(object sender, RoutedEventArgs e)
        {
            ShowProgress("Reading configuration snapshot...");
            await Task.Run(() => restApiService.GetSnapshot());
            HideProgress();
            TextViewer tv = new TextViewer("Configuration Snapshot", device.ConfigSnapshot)
            {
                Owner = this,
                SaveFilename = device.Name + "-snapshot.txt"
            };
            tv.Show();
        }

        private void RunWiz_Click(object sender, RoutedEventArgs e)
        {
            var ds = new DeviceSelection(selectedPort)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (ds.ShowDialog() == true)
            {
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
            if (slotView.Slots.Count == 1) //do not highlight if only one row
            {
                _slotsView.CellStyle = currentDict["gridCellNoHilite"] as Style;
            }
            SetTitleColor();
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
                selectedPort = port.Name;
                _btnRunWiz.IsEnabled = true;
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
                Logger.Info($"{txt} completed on Switch {device.Name}, S/N {device.SerialNumber}, Model {device.Model}");
                await WaitAckProgress();
            }
            else
            {
                port.PriorityLevel = prevPriority;
                Logger.Error($"Couldn't change the Priority to {port.PriorityLevel} on Port {port.Name} of Switch {device.IpAddress}");
                HideInfoBox();
            }
            RefreshSlotAndPortsView();
            HideProgress();
        }

        private async void FPoE_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CheckBox cb = sender as CheckBox;
                if (selectedSlot == null || !cb.IsKeyboardFocusWithin) return;
                FocusManager.SetFocusedElement(FocusManager.GetFocusScope(cb), null);
                Keyboard.ClearFocus();
                RestUrlId cmd = (cb.IsChecked == true) ? RestUrlId.POE_FAST_ENABLE : RestUrlId.POE_FAST_DISABLE;
                bool res = await SetPerpetualOrFastPoe(cmd);
                if (!res) cb.IsChecked = !cb.IsChecked;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message + ":\n" + ex.StackTrace);
            }
            HideProgress();
            HideInfoBox();
        }

        private async void PPoE_Click(Object sender, RoutedEventArgs e)
        {
            try
            {
                CheckBox cb = sender as CheckBox;
                if (selectedSlot == null || !cb.IsKeyboardFocusWithin) return;
                FocusManager.SetFocusedElement(FocusManager.GetFocusScope(cb), null);
                Keyboard.ClearFocus();
                RestUrlId cmd = (cb.IsChecked == true) ? RestUrlId.POE_PERPETUAL_ENABLE : RestUrlId.POE_PERPETUAL_DISABLE;
                bool res = await SetPerpetualOrFastPoe(cmd);
                if (!res) cb.IsChecked = !cb.IsChecked;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message + ":\n" + ex.StackTrace);
            }
            HideProgress();
            HideInfoBox();
        }

        private async Task<bool> SetPerpetualOrFastPoe(RestUrlId cmd)
        {
            string action = cmd == RestUrlId.POE_PERPETUAL_ENABLE || cmd == RestUrlId.POE_FAST_ENABLE ? "Enabling" : "Disabling";
            string poeType = (cmd == RestUrlId.POE_PERPETUAL_ENABLE || cmd == RestUrlId.POE_PERPETUAL_DISABLE) ? "Perpetual" : "Fast";
            ShowProgress($"{action} {poeType} PoE on slot {selectedSlot.Name}...");
            bool ok = false;
            await Task.Run(() => ok = restApiService.SetPerpetualOrFastPoe(selectedSlot.Name, cmd));
            Logger.Info($"{action} {poeType} PoE on slot {selectedSlot.Name} completed on switch {device.Name}, S/N {device.SerialNumber}, model {device.Model}");
            await WaitAckProgress();
            return ok;
            //RefreshSlotAndPortsView();
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
                restApiService = new RestApiService(device, progress);
                if (device.IsConnected)
                {
                    await CloseRestApiService();
                    SetDisconnectedState();
                    return;
                }
                await Task.Run(() => restApiService.Connect());
                UpdateConnectedState(true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message + ":\n" + ex.StackTrace);
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
                    if (device.ConfigChanged)
                    {
                        bool proceed = ShowMessageBox("Write Memory required", "Switch configuration has changed!\nIt will take around 30 sec to execute Write Memory Flash.\nDo you want to proceed?", MsgBoxIcons.Warning, MsgBoxButtons.OkCancel);
                        if (proceed)
                        {
                            await Task.Run(() => restApiService.WriteMemory());
                        }
                    }
                    restApiService.Close();
                }
                await Task.Run(() => Thread.Sleep(250)); //needed for the closing event handler
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message + ":\n" + ex.StackTrace);
            }
            HideProgress();
            HideInfoBox();
        }

        private async void LaunchPoeWizard(DeviceType deviceType)
        {
            try
            {
                DateTime startTime = DateTime.Now;
                await Task.Run(() => restApiService.ScanSwitch());
                UpdateConnectedState(false);
                ProgressReport wizardProgressReport = new ProgressReport("PoE Wizard Report:");
                reportResult = new ProgressReportResult();
                ShowProgress($"Running PoE Wizard on port {selectedPort}...");
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
                wizardProgressReport.Title = "PoE Wizard Report:";
                wizardProgressReport.Type = reportResult.Proceed ? ReportType.Error : ReportType.Info;
                wizardProgressReport.Message = $"{reportResult.Message}\n\nTotal duration: {Utils.CalcStringDuration(startTime, true)}";
                progress.Report(wizardProgressReport);
                await WaitAckProgress();
                RefreshSlotAndPortsView();
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message + ":\n" + ex.StackTrace);
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
        }

        private async void RefreshSwitch()
        {
            try
            {
                await Task.Run(() => restApiService.ScanSwitch());
                UpdateConnectedState(false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message + ":\n" + ex.StackTrace);
            }
            HideProgress();
            HideInfoBox();
        }

        private async Task RunWizardCamera()
        {
            await Enable823BT();
            if (!reportResult.Proceed) return;
            await ResetPortPower();
            if (!reportResult.Proceed) return;
            await EnableHdmiMdi();
            if (!reportResult.Proceed) return;
            await ChangePriority();
        }

        private async Task RunWizardTelephone()
        {
            await ChangePerpetualFastPoE(RestUrlId.ENABLE_PERPETUAL_DISABLE_FAST_POE);
            HideInfoBox();
            await Enable2PairPower();
            if (!reportResult.Proceed) return;
            await ChangePriority();
            if (!reportResult.Proceed) return;
            await EnableHdmiMdi();
            if (!reportResult.Proceed) return;
            await ResetPortPower();
        }

        private async Task RunWizardWirelessLan()
        {
            await Enable823BT();
            if (!reportResult.Proceed) return;
            await ResetPortPower();
            if (!reportResult.Proceed) return;
            await Enable2PairPower();
            if (!reportResult.Proceed) return;
            await ChangePriority();
            if (!reportResult.Proceed) return;
            await EnableHdmiMdi();
        }

        private async Task RunWizardOther()
        {
            await ChangePriority();
            if (!reportResult.Proceed) return;
            await Enable823BT();
            if (!reportResult.Proceed) return;
            await EnableHdmiMdi();
            if (!reportResult.Proceed) return;
            await ResetPortPower();
            if (!reportResult.Proceed) return;
            await Enable2PairPower();
        }

        private async Task Enable823BT()
        {
            bool proceed = ShowMessageBox("Enable 802.3.bt", "To enable 802.3.bt all devices on the same slot will restart. Do you want to proceed?", MsgBoxIcons.Warning, MsgBoxButtons.OkCancel);
            if (!proceed) return;
            await RunPoeWizard(new List<RestUrlId>() { RestUrlId.POWER_823BT_ENABLE }, 15);
            Logger.Info($"Enable 802.3.bt on port {selectedPort} completed on switch {device.Name}, S/N {device.SerialNumber}, model {device.Model}");
        }

        private async Task Enable2PairPower()
        {
            await RunPoeWizard(new List<RestUrlId>() { RestUrlId.POWER_2PAIR_PORT }, 15);
            Logger.Info($"Enable 2-Pair Power on port {selectedPort} completed on switch {device.Name}, S/N {device.SerialNumber}, model {device.Model}");
        }

        private async Task ChangePerpetualFastPoE(RestUrlId cmd)
        {
            await RunPoeWizard(new List<RestUrlId>() { cmd }, 15);
            Logger.Info($"Enable/Disable Perpetual/Fast PoE on port {selectedPort} completed on switch {device.Name}, S/N {device.SerialNumber}, model {device.Model}");
        }

        private async Task ChangePriority()
        {
            await RunPoeWizard(new List<RestUrlId>() { RestUrlId.CHECK_POWER_PRIORITY }, 15);
            Logger.Info($"PoE Wizard Check Power Priority on port {selectedPort} completed on switch {device.Name}, S/N {device.SerialNumber}, model {device.Model}");
            if (!reportResult.Proceed)
            {
                reportResult.UpdateResult(selectedPort, true);
                return;
            }
            bool proceed = ShowMessageBox("Power Priority Change", "Some other devices with lower priority may stop. Do you want to proceed?", MsgBoxIcons.Warning, MsgBoxButtons.OkCancel);
            if (!proceed) return;
            await RunPoeWizard(new List<RestUrlId>() { RestUrlId.POWER_PRIORITY_PORT }, 15);
            Logger.Info($"Change Power Priority on port {selectedPort} completed on switch {device.Name}, S/N {device.SerialNumber}, model {device.Model}");
        }

        private async Task EnableHdmiMdi()
        {
            await RunPoeWizard(new List<RestUrlId>() { RestUrlId.POWER_HDMI_ENABLE, RestUrlId.LLDP_POWER_MDI_ENABLE, RestUrlId.LLDP_EXT_POWER_MDI_ENABLE }, 15);
            Logger.Info($"Enable Power over HDMI on port {selectedPort} completed on switch {device.Name}, S/N {device.SerialNumber}, model {device.Model}");
        }

        private async Task ResetPortPower()
        {
            await RunPoeWizard(new List<RestUrlId>() { RestUrlId.RESET_POWER_PORT }, 15);
            Logger.Info($"Reset Power on port {selectedPort} completed on switch {device.Name}, S/N {device.SerialNumber}, model {device.Model}");
        }

        private async Task RunPoeWizard(List<RestUrlId> cmdList, int waitSec)
        {
            await Task.Run(() => restApiService.RunPoeWizard(selectedPort, reportResult, cmdList, waitSec));
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
                Logger.Info($"Connected to switch {device.Name}, S/N {device.SerialNumber}, model {device.Model}");
                SetConnectedState(checkCertified);
            }
            else
            {
                Logger.Info($"Switch S/N {device.SerialNumber}, model {device.Model} Disconnected");
                SetDisconnectedState();
            }
        }

        private async void SetConnectedState(bool checkCertified)
        {
            DataContext = device;
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
            _refreshSwitch.IsEnabled = true;
            _disconnectMenuItem.Visibility = Visibility.Visible;
            slotView = new SlotView(device);
            _slotsView.ItemsSource = slotView.Slots;
            _slotsView.SelectedIndex = 0;
            if (slotView.Slots.Count == 1) //do not highlight if only one row
            {
                _slotsView.CellStyle = currentDict["gridCellNoHilite"] as Style;
            }
            _slotsView.Visibility = Visibility.Visible;
            _portList.Visibility = Visibility.Visible;
        }

        private async Task RebootSwitch(int waitSec)
        {
            string duration = "";
            await Task.Run(() => duration = restApiService.RebootSwitch(waitSec));
            string txt = $"Switch {device.IpAddress} ready to connect";
            if (!string.IsNullOrEmpty(duration)) txt += $"\nReboot duration: {duration}";
            ShowMessageBox("Connection", txt, MsgBoxIcons.Info, MsgBoxButtons.Ok);
        }

        private void SetDisconnectedState()
        {
            _comImg.Source = (ImageSource)currentDict["disconnected"];
            string oldIp = device.IpAddress;
            device = new SwitchModel();
            device.IpAddress = oldIp;
            _switchAttributes.Text = "";
            _btnRunWiz.IsEnabled = false;
            DataContext = null;
            restApiService = null;
            _switchMenuItem.IsEnabled = true;
            _snapshotMenuItem.IsEnabled = false;
            _refreshSwitch.IsEnabled = false;
            _disconnectMenuItem.Visibility = Visibility.Collapsed;
            _slotsView.Visibility= Visibility.Hidden;
            _portList.Visibility= Visibility.Hidden;
        }

        #endregion private methods

        private void Label_SourceUpdated(object sender, System.Windows.Data.DataTransferEventArgs e)
        {

        }
    }
}
