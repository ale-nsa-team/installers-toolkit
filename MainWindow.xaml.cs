﻿using Microsoft.Win32;
using PoEWizard.Comm;
using PoEWizard.Components;
using PoEWizard.Data;
using PoEWizard.Device;
using PoEWizard.Exceptions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static PoEWizard.Data.Constants;
using static PoEWizard.Data.Utils;

namespace PoEWizard
{
    /// <summary>
    /// Interaction logic for Mainwindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("Kernel32.dll")]
        public static extern bool AttachConsole(int processId);

        #region Private Variables
        private readonly ResourceDictionary darkDict;
        private readonly ResourceDictionary lightDict;
        private ResourceDictionary currentDict;
        private readonly IProgress<ProgressReport> progress;
        private bool reportAck;
        private SftpService sftpService;
        private SwitchModel swModel;
        private SlotView slotView;
        private PortModel selectedPort;
        private int selectedPortIndex;
        private SlotModel selectedSlot;
        private int prevSlot;
        private int selectedSlotIndex;
        private bool isWaitingSlotOn = false;
        private WizardReport reportResult = new WizardReport();
        private bool isClosing = false;
        private DeviceType selectedDeviceType;
        private string lastIpAddr;
        private string lastPwd;
        private SwitchDebugModel debugSwitchLog;
        private static bool isTrafficRunning = false;
        private string stopTrafficAnalysisReason = string.Empty;
        private int selectedTrafficDuration;
        private DateTime startTrafficAnalysisTime;
        private double maxCollectLogsDur = 0;
        private string lastSearch = string.Empty;
        private string currAlias = string.Empty;
        private int prevIdx = -1;
        private OpType opType;
        private CancellationTokenSource tokenSource;
        #endregion

        #region properties
        public static Window Instance { get; private set; }
        public static ThemeType Theme { get; private set; }
        public static string DataPath { get; private set; }
        public static ResourceDictionary Strings { get; private set; }
        public static RestApiService restApiService;
        public static Dictionary<string, string> ouiTable = new Dictionary<string, string>();
        public static bool IsIpScanRunning { get; set; } = false;
        public static Config Config { get; set; }
        #endregion

        #region constructor and initialization
        public MainWindow()
        {
            swModel = new SwitchModel();
            //File Version info
            FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
            //datapath
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            DataPath = Path.Combine(appData, fileVersionInfo.CompanyName, fileVersionInfo.ProductName);
            InitializeComponent();
            lightDict = Resources.MergedDictionaries[0];
            darkDict = Resources.MergedDictionaries[1];
            Strings = Resources.MergedDictionaries[2];
            currentDict = darkDict;
            Instance = this;

            // progress report handling
            progress = new Progress<ProgressReport>(report =>
            {
                reportAck = false;
                switch (report.Type)
                {
                    case ReportType.Status:
                        if (report.Message == null) HideInfoBox();
                        else ShowInfoBox(report.Message);
                        break;
                    case ReportType.Error:
                        reportAck = ShowMessageBox(report.Title, report.Message, MsgBoxIcons.Error) == MsgBoxResult.Yes;
                        break;
                    case ReportType.Warning:
                        reportAck = ShowMessageBox(report.Title, report.Message, MsgBoxIcons.Warning) == MsgBoxResult.Yes;
                        break;
                    case ReportType.Info:
                        reportAck = ShowMessageBox(report.Title, report.Message) == MsgBoxResult.Yes;
                        break;
                    case ReportType.Value:
                        if (!string.IsNullOrEmpty(report.Title)) ShowProgress(report.Title, false);
                        if (report.Message == "-1") HideProgress();
                        else _progressBar.Value = double.TryParse(report.Message, out double dVal) ? dVal : 0;
                        break;
                    default:
                        break;
                }
            });
            this.Title += $" (V {string.Join(".", fileVersionInfo.ProductVersion.Split('.').ToList().Take(2))})";
            this.Height = SystemParameters.PrimaryScreenHeight * 0.95;
            this.Width = SystemParameters.PrimaryScreenWidth * 0.95;
            Activity.DataPath = DataPath;
            Config = new Config(Path.Combine(DataPath, "app.cfg"));
            Theme = Enum.TryParse(Config.Get("theme"), out ThemeType t) ? t : ThemeType.Dark;
            if (Theme == ThemeType.Light) ThemeItem_Click(_lightMenuItem, null);
            BuildOuiTable();
            SetLanguageMenuOptions();
            if (Config.GetInt("wait_cpu_health", 0) == 0)
            {
                Config.Set("wait_cpu_health", WAIT_CPU_HEALTH);
            }

            //check cli arguments
            string[] args = Environment.GetCommandLineArgs();
            switch (args.Length)
            {
                case 1:
                    break;
                case 2:
                    if (IsValidIP(args[1]))
                    {
                        swModel.IpAddress = args[1];
                        swModel.Login = DEFAULT_USERNAME;
                        swModel.Password = DEFAULT_PASSWORD;
                        Connect();
                    }
                    else goto default;
                    break;
                case 3:
                    if (IsValidIP(args[1]))
                    {
                        swModel.IpAddress = args[1];
                        swModel.Login = args[2];
                        swModel.Password = DEFAULT_PASSWORD;
                        Connect();
                    }
                    else goto default;
                    break;
                case 4:
                    if (IsValidIP(args[1]))
                    {
                        swModel.IpAddress = args[1];
                        swModel.Login = args[2];
                        swModel.Password = args[3].Replace("\r\n", string.Empty);
                        Connect();
                    }
                    else goto default;
                    break;
                default:
                    try
                    {
                        string appName = fileVersionInfo.InternalName;
                        AttachConsole(-1);
                        Console.WriteLine();
                        if (args.Length > 1 && !args[1].ToLower().Contains("help"))
                        {
                            Console.WriteLine(Translate("i18n_cliInv"));
                        }
                        else
                        {
                            Console.WriteLine(Translate("i18n_cliArgs"));
                        }
                        Console.WriteLine($"\t{appName} /help: {Translate("i18n_cliHlp")}");
                        Console.WriteLine($"\t{appName} {Translate("i18n_cliParams")}:");
                        Console.WriteLine($"\t{Translate("i18n_cliSw")}");
                        Console.WriteLine($"\t{Translate("i18n_noCred")}");

                        System.Windows.Forms.SendKeys.SendWait("{ENTER}");
                    }
                    catch
                    {
                        Logger.Error("Could not attach console to the application to display cli help message");
                    }
                    this.Close();
                    break;

            }
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            SetTitleColor(this);
            _btnConnect.IsEnabled = false;
        }

        private async void OnWindowClosing(object sender, CancelEventArgs e)
        {
            try
            {
                e.Cancel = true;
                string confirm = Translate("i18n_closing");
                stopTrafficAnalysisReason = "interrupted by the user before closing the application";
                bool close = StopTrafficAnalysis(TrafficStatus.Abort, $"{Translate("i18n_taDisc")} {swModel.Name}", Translate("i18n_taSave"), confirm);
                if (!close) return;
                this.Closing -= OnWindowClosing;
                await WaitCloseTrafficAnalysis();
                tokenSource?.Cancel();
                sftpService?.Disconnect();
                sftpService = null;
                await CloseRestApiService(FirstChToUpper(confirm));
                await Task.Run(() => Config.Save());
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
            Login login = new Login(swModel.Login)
            {
                Password = swModel.Password,
                IpAddress = string.IsNullOrEmpty(swModel.IpAddress) ? lastIpAddr : swModel.IpAddress,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            if (login.ShowDialog() == true)
            {
                swModel.Login = login.User;
                swModel.Password = login.Password;
                swModel.IpAddress = login.IpAddress;
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
            TextViewer tv = new TextViewer(Translate("i18n_tappLog"), canClear: true)
            {
                Owner = this,
                Filename = Logger.LogPath,
            };
            tv.Show();
        }

        private void ViewActivities_Click(object sender, RoutedEventArgs e)
        {
            TextViewer tv = new TextViewer(Translate("i18n_tactLog"), canClear: true)
            {
                Owner = this,
                Filename = Activity.FilePath
            };
            tv.Show();
        }

        private async void ViewVcBoot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string title = Translate("i18n_lvcboot");
                string msg = $"{title} {Translate("i18n_fromsw")} {swModel.Name}";
                ShowInfoBox($"{msg}{WAITING}");
                ShowProgress($"{title}{WAITING}");
                Logger.Debug(msg);
                string res = string.Empty;
                string sftpError = null;
                await Task.Run(() =>
                {
                    sftpService = new SftpService(swModel.IpAddress, swModel.Login, swModel.Password);
                    sftpError = sftpService.Connect();
                    if (string.IsNullOrEmpty(sftpError)) res = sftpService.DownloadToMemory(VCBOOT_WORK);
                });
                HideProgress();
                if (!string.IsNullOrEmpty(sftpError))
                {
                    ShowMessageBox(msg, $"{Translate("i18n_noSftp")} {swModel.Name}!\n{sftpError}", MsgBoxIcons.Warning, MsgBoxButtons.Ok);
                    return;
                }
                TextViewer tv = new TextViewer(Translate("i18n_tvcboot"), res)
                {
                    Owner = this,
                    SaveFilename = $"{swModel.Name}-{VCBOOT_FILE}"
                };
                Logger.Debug("Displaying vcboot file.");
                tv.Show();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            HideInfoBox();
        }

        private async void ViewSnapshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowProgress(Translate("i18n_lsnap"));
                await Task.Run(() => restApiService.GetSnapshot(new CancellationToken()));
                HideInfoBox();
                HideProgress();
                TextViewer tv = new TextViewer(Translate("i18n_tsnap"), swModel.ConfigSnapshot)
                {
                    Owner = this,
                    SaveFilename = $"{swModel.Name}{SNAPSHOT_SUFFIX}"
                };
                tv.Show();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private void ViewHwInfo_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ViewPS_Click(object sender, RoutedEventArgs e)
        {
            var ps = new PowerSupply(swModel)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            ps.Show();
        }

        private async void ViewVlan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string title = $"{Translate("i18n_vlanReading")} {swModel.Name}{WAITING}";
                ShowInfoBox(title);
                ShowProgress(title);
                List<Dictionary<string, string>> dictList = await Task.Run(() => restApiService.GetVlanSettings());
                HideInfoBox();
                HideProgress();
                ShowVlan($"{Translate("i18n_vlanTitle")}", dictList);
            }
            catch (Exception ex)
            {
                HideInfoBox();
                HideProgress();
                Logger.Error(ex);
            }
        }

        private void SearchDev_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SearchParams sp = new SearchParams(this) { SearchParam = lastSearch };
                sp.ShowDialog();
                if (sp.SearchParam == null) return;
                lastSearch = sp.SearchParam;
                var sd = new SearchDevice(swModel, lastSearch)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Progress = progress
                };
                if (string.IsNullOrEmpty(sd.SearchText)) return;
                if (sd.PortsFound?.Count > 0)
                {
                    sd.ShowDialog();
                    PortModel portSelected = sd.SelectedPort;
                    if (portSelected == null) return;
                    JumpToSelectedPort(portSelected);
                    return;
                }
                switch (sd.SearchType)
                {
                    case SearchType.Mac:
                        ShowMessageBox(Translate("i18n_sport"), Translate("i18n_fmac", lastSearch), MsgBoxIcons.Warning);
                        break;
                    case SearchType.Name:
                        ShowMessageBox(Translate("i18n_sport"), Translate("i18n_fdev", lastSearch), MsgBoxIcons.Warning);
                        break;
                    case SearchType.Ip:
                        ShowMessageBox(Translate("i18n_sport"), Translate("i18n_fip", lastSearch), MsgBoxIcons.Warning);
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            finally
            {
                HideInfoBox();
                HideProgress();
            }
        }

        private void JumpToSelectedPort(PortModel portSelected)
        {
            if (_portList.Items?.Count > 0)
            {
                string[] split = portSelected.Name.Split('/');
                string slotPortNr = $"{split[0]}/{split[1]}";
                int selIndex = -1;
                for (int idx = 0; idx < _slotsView.Items.Count; idx++)
                {
                    SlotModel slot = _slotsView.Items[idx] as SlotModel;
                    if (slot?.Name == slotPortNr)
                    {
                        selIndex = idx;
                        break;
                    }
                }
                if (selIndex < 0 || selIndex >= _slotsView.Items.Count) return;
                selectedSlotIndex = selIndex;
                _slotsView.SelectedItem = _slotsView.Items[selectedSlotIndex];
                _slotsView.ScrollIntoView(_slotsView.SelectedItem);
                selIndex = -1;
                for (int idx = 0; idx < _portList.Items.Count; idx++)
                {
                    PortModel port = _portList.Items[idx] as PortModel;
                    if (port?.Name == portSelected.Name)
                    {
                        selIndex = idx;
                        break;
                    }
                }
                if (selIndex < 0 || selIndex >= _portList.Items.Count) return;
                selectedPortIndex = selIndex;
                _portList.SelectedItem = _portList.Items[selectedPortIndex];
                _portList.ScrollIntoView(_portList.SelectedItem);
            }
        }

        private async void FactoryReset(object sender, RoutedEventArgs e)
        {

            ResetSelection rs = new ResetSelection(this);
            rs.ShowDialog();
            if (rs.DialogResult == false) return;

            PassCode pc = new PassCode(this);
            if (pc.ShowDialog() == false) return;
            if (pc.Password != pc.SavedPassword)
            {
                ShowMessageBox(Translate("i18n_fctRst"), Translate("i18n_badPwd"), MsgBoxIcons.Error);
                return;
            }
            Logger.Warn($"Switch {swModel.Name}, Model {swModel.Model}: {(rs.IsFullReset ? "Full" : "Partial")} Factory reset applied!");
            Activity.Log(swModel, rs.IsFullReset ? "Full " : "Partial " + "Factory reset applied.");
            ShowProgress(Translate("i18n_afrst"));
            FactoryDefault.Progress = progress;
            await Task.Run(() => FactoryDefault.Reset(swModel, rs.IsFullReset));
            ShowMessageBox(Translate("i18n_fctRst"), Translate("i18n_frReboot"));
            string snapFilepath = Path.Combine(DataPath, SNAPSHOT_FOLDER, $"{swModel.IpAddress}{SNAPSHOT_SUFFIX}");
            try
            {
                if (File.Exists(snapFilepath)) File.Delete(snapFilepath);
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to delete file {snapFilepath}", ex);
            }
            if (rs.IsFullReset)
            {
                restApiService.SendCommand(new CmdRequest(Command.REBOOT_SWITCH));
                string textMsg = $"{Translate("i18n_taDisc")} {swModel.Name}";
                ShowProgress($"{textMsg}{WAITING}");
                restApiService.Close();
                restApiService = null;
                SetDisconnectedState();
                HideProgress();
            }
            else
            {
                await RebootSwitch();
            }
        }

        private void LaunchConfigWizard(object sender, RoutedEventArgs e)
        {
            _status.Text = Translate("i18n_runCW");

            ConfigWiz wiz = new ConfigWiz(swModel)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            bool wasApplyed = (bool)wiz.ShowDialog();
            if (!wasApplyed)
            {
                HideProgress();
                return;
            }
            if (wiz.Errors.Count > 0)
            {
                string errMsg = wiz.Errors.Count > 1 ? Translate("i18n_cwErrs", $"{wiz.Errors.Count}") : Translate("i18n_cwErr");
                ShowMessageBox("Wizard", $"{errMsg}\n\n\u2022 {string.Join("\n\u2022 ", wiz.Errors)}", MsgBoxIcons.Error);
                Logger.Warn($"Configuration from Wizard applyed with errors:\n\t{string.Join("\n\t", wiz.Errors)}");
                Activity.Log(swModel, "Wizard applied with errors");
            }
            else
            {
                Activity.Log(swModel, "Config Wizard applied");
            }
            HideInfoBox();
            if (wiz.MustDisconnect)
            {
                ShowMessageBox("Config Wiz", Translate("i18n_discWiz"));
                isClosing = true; // to avoid write memory prompt
                Connect();
                return;
            }
            if (swModel.SyncStatus == SyncStatusType.Synchronized) swModel.SyncStatus = SyncStatusType.NotSynchronized;
            _status.Text = DEFAULT_APP_STATUS;
            SetConnectedState();
        }

        private void RunWiz_Click(object sender, RoutedEventArgs e)
        {
            if (selectedPort == null) return;
            var ds = new DeviceSelection(selectedPort.Name)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                DeviceType = selectedDeviceType
            };

            if (ds.ShowDialog() == true)
            {
                selectedDeviceType = ds.DeviceType;
                LaunchPoeWizard();
            }
        }

        private void UpgradeAos(object sender, RoutedEventArgs e)
        {
            LaunchUpgrade(true);
        }

        private void UpgradeUboot(object sender, RoutedEventArgs e)
        {
            LaunchUpgrade(false);
        }

        private async void LaunchUpgrade(bool isAos)
        {
            PassCode pc = new PassCode(this);
            if (pc.ShowDialog() == false) return;
            if (pc.Password != pc.SavedPassword)
            {
                ShowMessageBox(TranslateRestoreRunning(), Translate("i18n_badPwd"), MsgBoxIcons.Error);
                return;
            }
            var ofd = new OpenFileDialog()
            {
                Filter = $"{Translate("i18n_zipFile")}|*.zip",
                Title = Translate("i18n_upgSelFile"),
                InitialDirectory = Environment.SpecialFolder.MyDocuments.ToString(),
            };
            if (ofd.ShowDialog() == false) return;
            DisableButtons();
            AosUpgrade.Progress = progress;
            string source = isAos ? Translate("i18n_aosUpg") : Translate("i18n_ubootUpg");
            ShowProgress($"{source}...");
            bool res;
            if (isAos) res = await Task.Run(() => AosUpgrade.UpgradeAos(swModel, ofd.FileName));
            else res = await Task.Run(() => AosUpgrade.UpgradeUboot(swModel, ofd.FileName));
            HideProgress();
            EnableButtons();
            if (res)
            {
                Activity.Log(swModel, isAos ? "AOS upgrade applied" : "U-Boot upgrade applied");
                MsgBoxResult reboot = ShowMessageBox(source, Translate("i18n_upgReboot"), MsgBoxIcons.Question, MsgBoxButtons.YesNo);
                if (reboot == MsgBoxResult.No) return;
                restApiService?.StopTrafficAnalysis(TrafficStatus.Abort, $"interrupted by {source} before rebooting the switch {swModel.Name}");
                await RebootSwitch();
            }
            else
            {
                HideInfoBox();
                Activity.Log(swModel, isAos ? "AOS upgrade failed" : "U-Boot upgrade failed");
            }
        }

        private async void LaunchBackupCfg(object sender, RoutedEventArgs e)
        {
            try
            {
                DisableButtons();
                string cfgChanges = await GetSyncStatus(Translate("i18n_bckSync"));
                if (swModel.RunningDir == CERTIFIED_DIR || (swModel.SyncStatus != SyncStatusType.Synchronized && swModel.SyncStatus != SyncStatusType.NotSynchronized))
                {
                    if (ShowMessageBox(TranslateRestoreRunning(), $"{Translate("i18n_bckNotCert")}", MsgBoxIcons.Warning, MsgBoxButtons.YesNo) == MsgBoxResult.No)
                    {
                        return;
                    }
                }
                else if (swModel.RunningDir != CERTIFIED_DIR && swModel.SyncStatus == SyncStatusType.NotSynchronized && AuthorizeWriteMemory(TranslateBackupRunning(), cfgChanges))
                {
                    await Task.Run(() => restApiService.WriteMemory());
                    await GetSyncStatus(Translate("i18n_bckSync"));
                }
                Activity.Log(swModel, "Launching backup configuration");
                MsgBoxResult backupChoice = ShowMessageBox(TranslateBackupRunning(), $"{Translate("i18n_bckAskImg")}", MsgBoxIcons.Warning, MsgBoxButtons.YesNoCancel);
                bool backupImage = backupChoice == MsgBoxResult.Yes;
                if (backupChoice == MsgBoxResult.Cancel) return;
                string title = $"{Translate("i18n_bckRunning")} {swModel.Name}{WAITING}";
                ShowInfoBox($"{title}\n{Translate("i18n_bckDowloading")}");
                ShowProgress(title);
                DateTime startTime = DateTime.Now;
                double maxDur = backupImage ? 135 : 10;
                string zipPath = await Task.Run(() => restApiService.BackupConfiguration(maxDur, backupImage));
                if (!string.IsNullOrEmpty(zipPath))
                {
                    Logger.Info($"Created zip file \"{zipPath}\", backup duration: {CalcStringDuration(startTime)}");
                    string info = $"{Translate("i18n_fileSize")}: {PrintNumberBytes(new FileInfo(zipPath).Length)}";
                    ShowInfoBox($"{title}\n{info}\n{Translate("i18n_bckDur")}: {CalcStringDurationTranslate(startTime, true)}");
                    ShowProgress(title);
                    var sfd = new SaveFileDialog()
                    {
                        Filter = $"{Translate("i18n_zipFile")}|*.zip",
                        Title = Translate("i18n_bckSavefile"),
                        InitialDirectory = Environment.SpecialFolder.MyDocuments.ToString(),
                        FileName = Path.GetFileName(zipPath)
                    };
                    if (sfd.ShowDialog() == true)
                    {
                        if (sfd.FileName != zipPath)
                        {
                            File.Copy(zipPath, sfd.FileName, true);
                            File.Delete(zipPath);
                        }
                    }
                    else File.Delete(zipPath);
                }
                else
                {
                    ShowMessageBox(TranslateBackupRunning(), Translate("i18n_bckFail", swModel.Name), MsgBoxIcons.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                ShowMessageBox(TranslateBackupRunning(), $"{Translate("i18n_bckFail", swModel.Name)}\n{ex.Message}", MsgBoxIcons.Error);
            }
            finally
            {
                HideProgress();
                HideInfoBox();
                EnableButtons();
            }
        }

        private string TranslateBackupRunning()
        {
            return $"{Translate("i18n_bckRunning")} {swModel.Name}";
        }

        private async void LaunchRestoreCfg(object sender, RoutedEventArgs e)
        {
            try
            {
                DisableButtons();
                PassCode pc = new PassCode(this);
                if (pc.ShowDialog() == true)
                {
                    if (pc.Password != pc.SavedPassword)
                    {
                        ShowMessageBox(TranslateRestoreRunning(), Translate("i18n_badPwd"), MsgBoxIcons.Error);
                        return;
                    }
                    else
                    {
                        string cfgChanges = await GetSyncStatus(Translate("i18n_restSync"));
                        if (ShowMessageBox(TranslateRestoreRunning(), Translate("i18n_restConfirm", swModel.Name), MsgBoxIcons.Warning, MsgBoxButtons.YesNo) == MsgBoxResult.No)
                        {
                            return;
                        }
                        if (swModel.RunningDir == CERTIFIED_DIR || (swModel.SyncStatus != SyncStatusType.Synchronized && swModel.SyncStatus != SyncStatusType.NotSynchronized))
                        {
                            if (ShowMessageBox(TranslateRestoreRunning(), Translate("i18n_bckNotCert"), MsgBoxIcons.Warning, MsgBoxButtons.YesNo) == MsgBoxResult.No)
                            {
                                return;
                            }
                        }
                    }
                    var ofd = new OpenFileDialog()
                    {
                        Filter = $"{Translate("i18n_zipFile")}|*.zip",
                        Title = Translate("i18n_restSelFile"),
                        InitialDirectory = Environment.SpecialFolder.MyDocuments.ToString(),
                    };
                    if (ofd.ShowDialog() == true)
                    {
                        DateTime startTime = DateTime.Now;
                        Activity.Log(swModel, "Launching restore configuration");
                        bool reboot = await RestoreSwitchConfiguration(ofd.FileName);
                        Logger.Activity($"Restore configuration on switch {swModel.Name} ({swModel.IpAddress}) completed, duration: {Utils.CalcStringDuration(startTime)}");
                        if (reboot)
                        {
                            MsgBoxResult res = ShowMessageBox(TranslateRestoreRunning(), Translate("i18n_restReboot"), MsgBoxIcons.Question, MsgBoxButtons.YesNo);
                            if (res == MsgBoxResult.No) return;
                            restApiService?.StopTrafficAnalysis(TrafficStatus.Abort, $"interrupted by restore configuration before rebooting the switch {swModel.Name}");
                            await RebootSwitch();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                ShowMessageBox(TranslateRestoreRunning(), $"{Translate("i18n_restFail")}!\n{ex.Message}", MsgBoxIcons.Error);
            }
            finally
            {
                HideProgress();
                HideInfoBox();
                EnableButtons();
            }
        }

        private async Task<bool> RestoreSwitchConfiguration(string selFilePath)
        {
            Dictionary<MsgBoxIcons, string> dictInvalid = await Task.Run(() => restApiService.UnzipBackupSwitchFiles(5, selFilePath));
            string title = $"{Translate("i18n_restRunning")} {swModel.Name}{WAITING}";
            ShowInfoBox($"{title}\n{Translate("i18n_restUpLoading")}");
            ShowProgress(title);
            if (dictInvalid.Count == 0)
            {
                dictInvalid[MsgBoxIcons.Error] = CheckMissingBackupFiles();
                if (dictInvalid.Count == 0) return await ProceedToRestore();
            }
            if (dictInvalid.Count > 0)
            {
                string invalidMsg = string.Empty;
                if (dictInvalid.ContainsKey(MsgBoxIcons.Error) && !string.IsNullOrEmpty(dictInvalid[MsgBoxIcons.Error]))
                {
                    PurgeFilesInFolder(Path.Combine(DataPath, BACKUP_DIR));
                    invalidMsg = dictInvalid[MsgBoxIcons.Error];
                    ShowMessageBox(TranslateRestoreRunning(), $"{Translate("i18n_restInv", Path.GetFileName(selFilePath))}\n{invalidMsg}", MsgBoxIcons.Error);
                    return false;
                }
                else if (dictInvalid.ContainsKey(MsgBoxIcons.Warning) && !string.IsNullOrEmpty(dictInvalid[MsgBoxIcons.Warning]))
                {
                    invalidMsg = dictInvalid[MsgBoxIcons.Warning];
                    MsgBoxResult choice = ShowMessageBox(TranslateRestoreRunning(), $"{Translate("i18n_restInv", Path.GetFileName(selFilePath))}\n{invalidMsg}", MsgBoxIcons.Warning, MsgBoxButtons.OkCancel);
                    if (choice == MsgBoxResult.Cancel)
                    {
                        PurgeFilesInFolder(Path.Combine(DataPath, BACKUP_DIR));
                        return false;
                    }
                    return await ProceedToRestore();
                }
            }
            return true;
        }

        private async Task<bool> ProceedToRestore()
        {
            string restoreFolder = Path.Combine(DataPath, BACKUP_DIR);
            string swName = string.Empty;
            string swIp = string.Empty;
            List<Dictionary<string, string>> serial = new List<Dictionary<string, string>>();
            string swInfoFilePath = Path.Combine(restoreFolder, BACKUP_SWITCH_INFO_FILE);
            string swInfo = File.ReadAllText(swInfoFilePath);
            if (!string.IsNullOrEmpty(swInfo))
            {
                string[] lines = swInfo.Split('\n');
                if (lines.Length > 1)
                {
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrEmpty(line)) continue;
                        if (line.Contains(':'))
                        {
                            string[] split = line.Split(':');
                            if (split.Length > 1)
                            {
                                if (split[0].Trim() == BACKUP_SWITCH_NAME) swName = split[1].Trim();
                                else if (split[0].Trim() == BACKUP_SWITCH_IP) swIp = split[1].Trim();
                                else if (split[0].Contains(BACKUP_SERIAL_NUMBER) && split[0].Contains(BACKUP_CHASSIS))
                                {
                                    Dictionary<string, string> dict = new Dictionary<string, string>
                                    {
                                        [BACKUP_CHASSIS] = Regex.Split(split[0].Trim(), BACKUP_SERIAL_NUMBER)[0].Replace(BACKUP_CHASSIS, string.Empty).Trim(),
                                        [BACKUP_SERIAL_NUMBER] = split[1].Trim()
                                    };
                                    serial.Add(dict);
                                }
                            }
                        }
                    }
                }
                StringBuilder alert = CheckSerialNumber(swName, swIp, serial);
                if (alert.Length > 0)
                {
                    MsgBoxResult choice = ShowMessageBox(TranslateRestoreRunning(), $"{Translate("i18n_notMatchSerial")}\n{alert}", MsgBoxIcons.Warning, MsgBoxButtons.OkCancel);
                    if (choice == MsgBoxResult.Cancel) return false;
                }
                List<Dictionary<string, string>> dictList = CliParseUtils.ParseVlanConfig(File.ReadAllText(Path.Combine(Path.Combine(DataPath, BACKUP_DIR), BACKUP_VLAN_CSV_FILE)));
                ShowVlan($"{Translate("i18n_vlanBck")} {swName} ({swIp})", dictList);
            }
            string[] filesList = GetFilesInFolder(Path.Combine(restoreFolder, FLASH_DIR, FLASH_WORKING));
            bool foundImg = false;
            foreach (string file in filesList)
            {
                if (file.EndsWith(".img"))
                {
                    foundImg = true;
                    break;
                }
            }
            bool restoreImg = false;
            if (foundImg)
            {
                restoreImg = ShowMessageBox(TranslateRestoreRunning(), $"{Translate("i18n_restAskImg")}", MsgBoxIcons.Warning, MsgBoxButtons.YesNo) == MsgBoxResult.Yes;
            }
            string title = $"{Translate("i18n_restRunning")} {swModel.Name}{WAITING}";
            ShowInfoBox($"{title}\n{Translate("i18n_restUpLoading")}");
            double maxDur = restoreImg ? 135 : 10;
            await Task.Run(() => restApiService.UploadConfigurationFiles(maxDur, restoreImg));
            return true;
        }

        private string CheckMissingBackupFiles()
        {
            string invalidMsg = string.Empty;
            invalidMsg = CheckMissingFile(invalidMsg, VCBOOT_FILE, FLASH_CERTIFIED);
            invalidMsg = CheckMissingFile(invalidMsg, VCSETUP_FILE, FLASH_CERTIFIED);
            invalidMsg = CheckMissingFile(invalidMsg, VCBOOT_FILE, FLASH_WORKING);
            invalidMsg = CheckMissingFile(invalidMsg, VCSETUP_FILE, FLASH_WORKING);
            invalidMsg = CheckMissingFile(invalidMsg, BACKUP_SWITCH_INFO_FILE);
            invalidMsg = CheckMissingFile(invalidMsg, BACKUP_VLAN_CSV_FILE);
            return invalidMsg;
        }

        private string CheckMissingFile(string invalidMsg, string file, string dir = null)
        {
            string restoreFolder = Path.Combine(DataPath, BACKUP_DIR);
            string filePath = !string.IsNullOrEmpty(dir) ? Path.Combine(restoreFolder, FLASH_DIR, dir, file) : Path.Combine(restoreFolder, file);
            if (!File.Exists(filePath))
            {
                if (invalidMsg.Length > 0) invalidMsg += "\n";
                invalidMsg += !string.IsNullOrEmpty(dir) ? Translate("i18n_missingFile", $"/{FLASH_DIR}/{dir}/{file}") : Translate("i18n_missingFile", file);
            }
            return invalidMsg;
        }

        private StringBuilder CheckSerialNumber(string swName, string swIp, List<Dictionary<string, string>> serial)
        {
            StringBuilder alert = new StringBuilder();
            foreach (Dictionary<string, string> dict in serial)
            {
                if (!dict.ContainsKey(BACKUP_CHASSIS)) continue;
                int chassisNr = StringToInt(dict[BACKUP_CHASSIS]);
                ChassisModel chassis = swModel.GetChassis(chassisNr);
                if (chassis == null)
                {
                    if (alert.Length > 0) alert.Append("\n");
                    alert.Append(Translate("i18n_restChassis")).Append(" ").Append(chassisNr).Append(" ").Append(Translate("i18n_restNotExist")).Append(".");
                    continue;
                }
                if (!dict.ContainsKey(BACKUP_SERIAL_NUMBER)) continue;
                string sn = dict[BACKUP_SERIAL_NUMBER];
                if (sn != chassis.SerialNumber)
                {
                    if (alert.Length > 0) alert.Append("\n");
                    alert.Append(Translate("i18n_restChassis")).Append(" ").Append(chassisNr).Append(" ").Append(Translate("i18n_restSerial")).Append(":\n");
                    alert.Append("    - ").Append(Translate("i18n_restNotMatch", sn, chassis.SerialNumber));
                }
            }
            if (alert.Length > 0)
            {
                if (swIp != swModel.IpAddress || swName != swModel.Name)
                {
                    alert.Append("\n").Append(Translate("i18n_restInvSw", swName, swIp)).Append(".");
                }
            }
            return alert;
        }

        private void ShowVlan(string title, List<Dictionary<string, string>> dictList)
        {
            var vlan = new VlanSettings(dictList)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            HideInfoBox();
            vlan._title.Text = $"{title}";
            vlan.ShowDialog();
        }

        private string TranslateRestoreRunning()
        {
            return $"{Translate("i18n_restRunning")} {swModel.Name}";
        }

        private async void ResetPort_Click(object sender, RoutedEventArgs e)
        {
            if (selectedPort == null) return;
            string title = $"{Translate("i18n_rstpp")} {selectedPort.Name}";
            try
            {
                if (ShowMessageBox(title, $"{Translate("i18n_cprst")} {selectedPort.Name} {Translate("i18n_onsw")} {swModel.Name}?",
                    MsgBoxIcons.Warning, MsgBoxButtons.YesNo) == MsgBoxResult.No)
                {
                    return;
                }
                DisableButtons();
                string barText = $"{title}{WAITING}";
                ShowInfoBox(barText);
                ShowProgress(barText);
                await Task.Run(() => restApiService.ResetPort(selectedPort.Name, 60));
                HideProgress();
                HideInfoBox();
                await RefreshChanges();
                HideInfoBox();
                await WaitAckProgress();
            }
            catch (Exception ex)
            {
                HideProgress();
                HideInfoBox();
                Logger.Error(ex);
            }
            EnableButtons();
        }

        private async void RunTdr_Click(object sender, RoutedEventArgs e)
        {
            if (selectedPort == null) return;
            string title = $"{Translate("i18n_runTdr")} {selectedPort.Name}";

            if (selectedPort.Poe == PoeStatus.NoPoe)
            {
                ShowMessageBox(title, $"{Translate("i18n_noTdr")} {selectedPort.Name}", MsgBoxIcons.Error);
                return;
            }

            DisableButtons();
            string barText = $"{title}{WAITING}";
            ShowInfoBox(barText);
            ShowProgress(barText);
            TdrModel res = await Task.Run(() => restApiService.RunTdr(selectedPort.Name));
            HideProgress();
            HideInfoBox();
            if (res == null) return;
            TdrView tdr = new TdrView(res)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            tdr.ShowDialog();
            EnableButtons();
        }

        private void RefreshSwitch_Click(object sender, RoutedEventArgs e)
        {
            tokenSource.Cancel();
            RefreshSwitch();
        }

        private void WriteMemory_Click(object sender, RoutedEventArgs e)
        {
            WriteMemory();
        }

        private void Reboot_Click(object sender, RoutedEventArgs e)
        {
            PassCode pc = new PassCode(this);
            if (pc.ShowDialog() == true)
            {
                if (pc.Password != pc.SavedPassword)
                {
                    ShowMessageBox(Translate("i18n_reboot"), Translate("i18n_badPwd"), MsgBoxIcons.Error);
                }
                else
                {
                    LaunchRebootSwitch();
                }
            }
        }

        private async void LaunchRebootSwitch()
        {
            try
            {
                if (restApiService.IsWaitingReboot())
                {
                    if (ShowMessageBox(Translate("i18n_rebsw"), Translate("i18n_waitRebootCancel"), MsgBoxIcons.Info, MsgBoxButtons.YesNo) == MsgBoxResult.Yes)
                    {
                        await StopWaitingReboot();
                    }
                    return;
                }
                string cfgChanges = await GetSyncStatus($"{Translate("i18n_swrst")} {swModel.Name}");
                if (ShowMessageBox(Translate("i18n_rebsw"), $"{Translate("i18n_crebsw")} {swModel.Name}?", MsgBoxIcons.Warning, MsgBoxButtons.YesNo) == MsgBoxResult.Yes)
                {
                    if (swModel.SyncStatus != SyncStatusType.Synchronized && swModel.SyncStatus != SyncStatusType.NotSynchronized)
                    {
                        ShowMessageBox(Translate("i18n_rebsw"), $"{Translate("i18n_frebsw1")} {swModel.Name} {Translate("i18n_notCert")}", MsgBoxIcons.Error);
                        return;
                    }
                    if (swModel.RunningDir != CERTIFIED_DIR && swModel.SyncStatus == SyncStatusType.NotSynchronized && AuthorizeWriteMemory(Translate("i18n_rebsw"), cfgChanges))
                    {
                        await Task.Run(() => restApiService.WriteMemory());
                    }
                    string confirm = $"{Translate("i18n_swrst")} {swModel.Name}";
                    stopTrafficAnalysisReason = $"interrupted by the user before rebooting the switch {swModel.Name}";
                    string title = $"{Translate("i18n_swrst")} {swModel.Name}";
                    tokenSource?.Cancel();
                    bool close = StopTrafficAnalysis(TrafficStatus.Abort, title, Translate("i18n_taSave"), confirm);
                    if (!close) return;
                    await WaitCloseTrafficAnalysis();
                    await RebootSwitch();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            finally
            {
                HideInfoBox();
                HideProgress();
            }
        }

        private async Task StopWaitingReboot()
        {
            if (restApiService == null) return;
            _reboot.IsEnabled = false;
            string title = Translate("i18n_stoppingWaitReboot");
            ShowInfoBox(title);
            ShowProgress(title);
            restApiService.StopWaitingReboot();
            await Task.Run(() =>
            {
                while (restApiService.IsWaitingReboot())
                {
                    Thread.Sleep(250);
                    restApiService.StopWaitingReboot();
                }
            });
        }

        private async Task RebootSwitch()
        {
            opType = OpType.Reboot;
            string switchName = swModel.Name;
            string lastIp = swModel.IpAddress;
            string lastPasswd = swModel.Password;
            string title = $"{Translate("i18n_waitReboot", swModel.Name)}{WAITING}";
            ShowInfoBox(title);
            ShowProgress(title);
            ClearMainWindowGui();
            _comImg.Visibility = Visibility.Collapsed;
            _btnConnect.Visibility = Visibility.Collapsed;
            _switchMenuItem.IsEnabled = false;
            _reboot.IsEnabled = true;
            _rebootLabel.Content = Translate("i18n_waitingReboot");
            _btnCancel.Visibility = Visibility.Visible;
            string msg = await Task.Run(() => restApiService.RebootSwitch(MAX_SWITCH_REBOOT_TIME_SEC));
            if (string.IsNullOrEmpty(msg))
            {
                DisconnectSwitch(lastIp, lastPasswd);
                return;
            }
            if (ShowMessageBox(Translate("i18n_rebsw"), $"{msg}\n{Translate("i18n_recsw")} {switchName}?", MsgBoxIcons.Info, MsgBoxButtons.YesNo) == MsgBoxResult.Yes)
            {
                if (restApiService != null)
                {
                    if (restApiService.SwitchModel == null) restApiService.SwitchModel = swModel;
                    reportResult = new WizardReport();
                    opType = OpType.Connection;
                    tokenSource = new CancellationTokenSource();
                    msg = await Task.Run(() => restApiService?.WaitInit(reportResult, tokenSource.Token));
                    if (string.IsNullOrEmpty(msg))
                    {
                        DisconnectSwitch(lastIp, lastPasswd);
                        return;
                    }
                    ShowMessageBox($"{Translate("i18n_rstwait")} {switchName}", $"{Translate("i18n_initEnd")} {switchName}.\n{msg}", MsgBoxIcons.Info, MsgBoxButtons.Ok);
                }
                SetDisconnectedState();
                lastIpAddr = lastIp;
                lastPwd = lastPasswd;
                Connect();
            }
            else
            {
                SetDisconnectedState();
                lastIpAddr = lastIp;
                lastPwd = lastPasswd;
            }
        }

        private async void BtnCancel_Click(object sender, EventArgs e)
        {
            _btnCancel.IsEnabled = false;
            switch (opType)
            {
                case OpType.Reboot:
                    await StopWaitingReboot();
                    ShowMessageBox(Translate("i18n_rebsw"), Translate("i18n_stopWaitReboot"));
                    break;
                case OpType.Connection:
                    tokenSource.Cancel();
                    ClearMainWindowGui();
                    break;
                case OpType.Refresh:
                    tokenSource.Cancel();
                    break;
            }
        }

        private void DisconnectSwitch(string lastIp, string lastPasswd)
        {
            restApiService.Close();
            restApiService = null;
            SetDisconnectedState();
            lastIpAddr = lastIp;
            lastPwd = lastPasswd;
        }

        private async void CollectLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MsgBoxResult restartPoE = ShowMessageBox(Translate("i18n_logcol"), $"{Translate("i18n_pclogs1")} {swModel.Name} {Translate("i18n_pclogs2")}",
                    MsgBoxIcons.Warning, MsgBoxButtons.YesNoCancel);
                if (restartPoE == MsgBoxResult.Cancel) return;
                string txt = $"Collect Logs launched by the user";
                if (restartPoE == MsgBoxResult.Yes)
                {
                    maxCollectLogsDur = MAX_COLLECT_LOGS_RESET_POE_DURATION;
                    txt += " (power cycle PoE on all ports)";
                }
                else
                {
                    maxCollectLogsDur = MAX_COLLECT_LOGS_DURATION;
                }
                Logger.Activity($"{txt} on switch {swModel.Name}");
                Activity.Log(swModel, $"{txt}.");
                DisableButtons();
                string sftpError = await RunCollectLogs(restartPoE == MsgBoxResult.Yes, null);
                if (!string.IsNullOrEmpty(sftpError)) ShowMessageBox($"{Translate("i18n_clog")} {swModel.Name}", $"{Translate("i18n_noSftp")} {swModel.Name}!\n{sftpError}", MsgBoxIcons.Warning, MsgBoxButtons.Ok);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            EnableButtons();
            HideProgress();
            HideInfoBox();
        }

        private void Traffic_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (restApiService == null) return;
                if (IsTrafficAnalysisRunning())
                {
                    stopTrafficAnalysisReason = "interrupted by the user";
                    StopTrafficAnalysis(TrafficStatus.CanceledByUser, Translate("i18n_taIdle"), Translate("i18n_tastop"));
                }
                else
                {
                    var ds = new TrafficDurationSelection() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
                    if (ds.ShowDialog() == true)
                    {
                        selectedTrafficDuration = ds.TrafficDurationSec;
                        StartTrafficAnalysis();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private async void StartTrafficAnalysis()
        {
            try
            {
                isTrafficRunning = true;
                startTrafficAnalysisTime = DateTime.Now;
                _trafficLabel.Content = Translate("i18n_taRun");
                string switchName = swModel.Name;
                TrafficReport report = await Task.Run(() => restApiService.RunTrafficAnalysis(selectedTrafficDuration));
                if (report != null)
                {
                    if (report.NbPortsNoData >= MAX_NB_PORTS_NO_DATA) ShowMessageBox(Translate("i18n_taIdle"), $"{Translate("i18n_tanodata")}", MsgBoxIcons.Info, MsgBoxButtons.Ok);
                    TextViewer tv = new TextViewer(Translate("i18n_taIdle"), report.Summary)
                    {
                        Owner = this,
                        SaveFilename = $"{switchName}-{DateTime.Now:MM-dd-yyyy_hh_mm_ss}-traffic-analysis.txt",
                        CsvData = report.Data.ToString()
                    };
                    tv.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            finally
            {
                _trafficLabel.Content = Translate("i18n_taIdle");
                if (swModel.IsConnected) _traffic.IsEnabled = true; else _traffic.IsEnabled = false;
                HideProgress();
                HideInfoBox();
                isTrafficRunning = false;
            }
        }

        private bool IsTrafficAnalysisRunning()
        {
            return restApiService != null && restApiService.IsTrafficAnalysisRunning();
        }

        private bool StopTrafficAnalysis(TrafficStatus abortType, string title, string question, string confirm = null)
        {
            if (!isTrafficRunning) return true;
            try
            {
                StringBuilder txt = new StringBuilder(Translate("i18n_tarun"));
                string strSelDuration = string.Empty;
                int duration = 0;
                if (selectedTrafficDuration >= 60 && selectedTrafficDuration < 3600)
                {
                    duration = selectedTrafficDuration / 60;
                    strSelDuration = $" {duration} {Translate("i18n_tamin")}";
                }
                else
                {
                    duration = selectedTrafficDuration / 3600;
                    strSelDuration = $" {duration} {Translate("i18n_tahour")}";
                }
                if (duration > 1) strSelDuration += "s";
                txt.Append(strSelDuration).Append($").\n{Translate("i18n_tadur")} ").Append(CalcStringDurationTranslate(startTrafficAnalysisTime, true));
                txt.Append("\n").Append(question).Append("?");
                MsgBoxResult res = ShowMessageBox(title, txt.ToString(), MsgBoxIcons.Warning, MsgBoxButtons.YesNo);
                if (res == MsgBoxResult.Yes)
                {
                    restApiService?.StopTrafficAnalysis(TrafficStatus.CanceledByUser, stopTrafficAnalysisReason);
                }
                else if (abortType == TrafficStatus.Abort)
                {
                    if (!string.IsNullOrEmpty(confirm))
                    {
                        res = ShowMessageBox(title, $"{Translate("i18n_tacont")} {confirm}?", MsgBoxIcons.Warning, MsgBoxButtons.YesNo);
                        if (res == MsgBoxResult.No) return false;
                    }
                    restApiService?.StopTrafficAnalysis(abortType, stopTrafficAnalysisReason);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            HideInfoBox();
            HideProgress();
            return true;
        }

        private async Task WaitCloseTrafficAnalysis()
        {
            await Task.Run(() =>
            {
                while (isTrafficRunning)
                {
                    Thread.Sleep(250);
                }
            });
        }

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            string hlpFile = "help-enUS.html"; //must add code to select file if multiple languages are created
            HelpViewer hv = new HelpViewer(hlpFile)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            hv.Show();
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
            if (t == Translate("i18n_dark"))
            {
                _lightMenuItem.IsChecked = false;
                Theme = ThemeType.Dark;
                Resources.MergedDictionaries.Remove(lightDict);
                Resources.MergedDictionaries.Add(darkDict);
                currentDict = darkDict;
            }
            else
            {
                _darkMenuItem.IsChecked = false;
                Theme = ThemeType.Light;
                Resources.MergedDictionaries.Remove(darkDict);
                Resources.MergedDictionaries.Add(lightDict);
                currentDict = lightDict;
            }
            if (slotView?.Slots.Count == 1) //do not highlight if only one row
            {
                _slotsView.CellStyle = currentDict["gridCellNoHilite"] as Style;
            }
            Config.Set("theme", t);
            SetTitleColor(this);
            //force color converters to run
            DataContext = null;
            DataContext = swModel;
        }

        private void LangItem_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            if (mi.IsChecked) return;
            string l = mi.Header.ToString().Replace("-", "");
            string filename = Path.Combine(DataPath, LANGUAGE_FOLDER, $"strings-{l}.xaml");
            if (l == "enUS")
            {
                var uri = new Uri($"pack://application:,,,/Resources/strings-{l}.xaml", UriKind.RelativeOrAbsolute);
                Resources.MergedDictionaries.Remove(Strings);
                Strings = new ResourceDictionary
                {
                    Source = uri
                };
                Resources.MergedDictionaries.Add(Strings);
            }
            else if (!LoadLanguageFile(filename))
            {
                ShowMessageBox(Translate("i18n_lang"), Translate("i18n_badLang", $"strings-{l}.xaml"), MsgBoxIcons.Error);
                try { File.Delete(filename); } catch { }
                _langSel.Items.Remove(mi);
                return;
            }
            mi.IsChecked = true;
            Config.Set("language", mi.Header.ToString());
            foreach (MenuItem i in _langSel.Items)
            {
                if (i != mi) i.IsChecked = false;
            }
            if (swModel.IsConnected)
            {
                _switchAttributes.Text = $"{Translate("i18n_connTo")} {swModel.Name}";
            }
            //force tooltip converter to run
            if (selectedSlot != null)
            {
                _portList.ItemsSource = null;
                _portList.ItemsSource = selectedSlot.Ports;
            }
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
                prevSlot = selectedSlotIndex;
                selectedSlot = slot;
                swModel.SelectedSlot = slot.Name;
                selectedSlotIndex = _slotsView.SelectedIndex;
                swModel.UpdateSelectedSlotData(slot.Name);
                DataContext = null;
                DataContext = swModel;
                _portList.ItemsSource = slot.Ports;
                _btnResetPort.IsEnabled = false;
                _btnRunWiz.IsEnabled = false;
                _btnTdr.IsEnabled = false;
            }
        }

        private void PortSelection_Changed(Object sender, RoutedEventArgs e)
        {
            if (_portList.SelectedItem is PortModel port)
            {
                selectedPort = port;
                selectedPortIndex = _portList.SelectedIndex;
                _btnRunWiz.IsEnabled = selectedPort.Poe != PoeStatus.NoPoe;
                _btnResetPort.IsEnabled = true;
                _btnTdr.IsEnabled = selectedPort.Poe != PoeStatus.NoPoe;
            }
        }

        private void OnPortAliasGotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                currAlias = tb.Text.Trim();
            }
        }

        private void OnPortAliasLostFocus(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is TextBox tb && tb.Text.Trim() != currAlias)
                {
                    string alias = tb.Text.Trim() == string.Empty ? BLANK_ALIAS : tb.Text.Trim();
                    currAlias = string.Empty;
                    restApiService.SendCommand(new CmdRequest(Command.SET_PORT_ALIAS, selectedPort.Index.ToString(), alias));
                    Activity.Log(swModel, $"Port {selectedPort.Name} alias {(alias == BLANK_ALIAS ? "deleted" : "set to \"" + alias + "\"")}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                ShowMessageBox($"{Translate("i18n_updAlias")} {swModel.Name}", ex.Message, MsgBoxIcons.Error);
            }
        }

        private async void Priority_Changed(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var cb = sender as ComboBox;
                PortModel port = _portList.CurrentItem as PortModel;
                if (cb.SelectedValue.ToString() != port.PriorityLevel.ToString())
                {
                    ShowMessageBox(Translate("i18n_prio"), $"{Translate("i18n_selprio")} {cb.SelectedValue}");
                    PriorityLevelType prevPriority = port.PriorityLevel;
                    port.PriorityLevel = (PriorityLevelType)Enum.Parse(typeof(PriorityLevelType), cb.SelectedValue.ToString());
                    if (port == null) return;
                    string txt = $"{Translate("i18n_cprio")} {port.PriorityLevel} on port {port.Name}";
                    ShowProgress($"{txt}{WAITING}");
                    bool ok = false;
                    await Task.Run(() => ok = restApiService.ChangePowerPriority(port.Name, port.PriorityLevel));
                    if (ok)
                    {
                        await WaitAckProgress();
                    }
                    else
                    {
                        port.PriorityLevel = prevPriority;
                        Logger.Error($"Couldn't change the Priority to {port.PriorityLevel} on port {port.Name} of Switch {swModel.IpAddress}");
                    }
                    await RefreshChanges();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            finally
            {
                HideInfoBox();
                HideProgress();
            }
        }

        private async void Power_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            if (selectedSlot == null || !cb.IsKeyboardFocusWithin) return;
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(cb), null);
            Keyboard.ClearFocus();
            if (!selectedSlot.SupportsPoE)
            {
                ShowMessageBox(Translate("i18n_spoeOff"), $"{Translate("i18n_slot")} {selectedSlot.Name} {Translate("i18n_nopoe")}", MsgBoxIcons.Error);
                cb.IsChecked = false;
                return;
            }
            if (cb.IsChecked == false)
            {
                string msg = $"{Translate("i18n_cpoeoff")} {selectedSlot.Name}?";
                MsgBoxResult poweroff = ShowMessageBox(Translate("i18n_spoeOff"), msg, MsgBoxIcons.Question, MsgBoxButtons.YesNo);
                if (poweroff == MsgBoxResult.Yes)
                {
                    PassCode pc = new PassCode(this);
                    if (pc.ShowDialog() == false)
                    {
                        cb.IsChecked = true;
                        return;
                    }
                    if (pc.Password != pc.SavedPassword)
                    {
                        ShowMessageBox(Translate("i18n_spoeOff"), Translate("i18n_badPwd"), MsgBoxIcons.Error);
                        cb.IsChecked = true;
                        return;
                    }
                    DisableButtons();
                    await PowerSlotUpOrDown(Command.POWER_DOWN_SLOT, selectedSlot.Name);
                    Logger.Activity($"PoE on slot {selectedSlot.Name} turned off");
                    Activity.Log(swModel, $"PoE on slot {selectedSlot.Name} turned off");
                    return;
                }
                else
                {
                    cb.IsChecked = true;
                    return;
                }
            }
            else if (isWaitingSlotOn)
            {
                selectedSlot = slotView.Slots[prevSlot];
                _slotsView.SelectedIndex = prevSlot;
                ShowMessageBox(Translate("i18n_spoeon"), $"{Translate("i18n_wpoeon")} {selectedSlot.Name} {Translate("i18n_waitup")}");
                cb.IsChecked = false;
                return;
            }
            else
            {
                isWaitingSlotOn = true;
                DisableButtons();
                ShowProgress($"{Translate("i18n_poeon")} {Translate("i18n_onsl")} {selectedSlot.Name}");
                await Task.Run(() =>
                {
                    restApiService.PowerSlotUpOrDown(Command.POWER_UP_SLOT, selectedSlot.Name);
                    WaitSlotPortsUp();
                });
                Logger.Activity($"PoE on slot {selectedSlot.Name} turned on");
                Activity.Log(swModel, $"PoE on slot {selectedSlot.Name} turned on");
                RefreshSlotsAndPorts();
                isWaitingSlotOn = false;
            }
            slotView = new SlotView(swModel);
            _slotsView.ItemsSource = slotView.Slots;
        }

        private async void FPoE_Click(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            if (selectedSlot == null || !cb.IsKeyboardFocusWithin) return;
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(cb), null);
            Keyboard.ClearFocus();
            Command cmd = (cb.IsChecked == true) ? Command.POE_FAST_ENABLE : Command.POE_FAST_DISABLE;
            bool res = await SetPerpetualOrFastPoe(cmd);
            if (!res) cb.IsChecked = !cb.IsChecked;
        }

        private async void PPoE_Click(Object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            if (selectedSlot == null || !cb.IsKeyboardFocusWithin) return;
            FocusManager.SetFocusedElement(FocusManager.GetFocusScope(cb), null);
            Keyboard.ClearFocus();
            Command cmd = (cb.IsChecked == true) ? Command.POE_PERPETUAL_ENABLE : Command.POE_PERPETUAL_DISABLE;
            bool res = await SetPerpetualOrFastPoe(cmd);
            if (!res) cb.IsChecked = !cb.IsChecked;
        }

        private void IpAddress_Click(object sender, EventArgs e)
        {
            //let portselection event run first
            Task.Delay(TimeSpan.FromMilliseconds(250)).ContinueWith(t =>
            {
                Dispatcher.Invoke(() =>
                {
                    ConnectCurrentDevice();
                });
            });
        }

        private void ShowPopup(object sender, MouseEventArgs e)
        {
            if (sender is TextBlock tb)
            {
                try
                {
                    DataGridRow row = DataGridRow.GetRowContainingElement(tb);
                    int idx = row?.GetIndex() ?? -1;
                    if (idx == -1 || idx == prevIdx) return;
                    prevIdx = idx;
                    if (string.IsNullOrEmpty(tb.Text)) return;
                    Task.Delay(IP_LIST_POPUP_DELAY).ContinueWith(t =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            PortModel port = _portList.Items[idx] as PortModel;
                            var pos = e.GetPosition(tb);
                            if (Math.Abs(pos.Y) > 100 || Math.Abs(pos.X) > 100) return; //mouse is too far away.
                            PopupUserControl popup = new PopupUserControl
                            {
                                Progress = progress,
                                Data = port.IpAddrList,
                                KeyHeader = "MAC",
                                ValueHeader = "IP",
                                Target = tb,
                                Placement = PlacementMode.Relative,
                                OffsetX = pos.X - 5,
                                OffsetY = pos.Y - 5
                            };
                            popup.Show();
                        });
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            }
        }

        private void ConnectCurrentDevice()
        {
            _portList.SelectionChanged -= PortSelection_Changed;
            _portList.SelectedIndex = selectedPortIndex; //fix issue with selection jumping 2 rows above
            string ipAddr = selectedPort.IpAddress?.Replace("...", "").Trim();
            if (string.IsNullOrEmpty(ipAddr)) return;
            int port = selectedPort.RemotePort;
            ConnectToPort(ipAddr, port);
            _portList.SelectionChanged += PortSelection_Changed;
        }

        private void ConnectToPort(string ipAddr, int port)
        {
            try
            {
                switch (port)
                {
                    case 22:
                    case 23:
                        string putty = Config.Get("putty");
                        if (string.IsNullOrEmpty(putty))
                        {
                            var ofd = new OpenFileDialog()
                            {
                                Filter = $"{Translate("i18n_puttyFile")}|*.exe",
                                Title = Translate("i18n_puttyLoc"),
                                InitialDirectory = Environment.SpecialFolder.ProgramFiles.ToString()
                            };
                            if (ofd.ShowDialog() == false) return;
                            putty = ofd.FileName;
                            Config.Set("putty", putty);
                        }
                        string cnx = port == 22 ? "ssh" : "telnet";
                        Process.Start(putty, $"-{cnx} {ipAddr}");
                        break;
                    case 80:
                        Process.Start("explorer.exe", $"http://{ipAddr}");
                        break;
                    case 443:
                        Process.Start("explorer.exe", $"https://{ipAddr}");
                        break;
                    case 3389:
                        Process.Start("mstsc", $"/v: {ipAddr}");
                        break;
                    default:
                        ShowMessageBox("", Translate("i18n_noPtOpen", ipAddr));
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                if (!string.IsNullOrEmpty(ipAddr))
                {
                    if (port != 0) ShowMessageBox("", Translate("i18n_cnxFail", ipAddr, port.ToString()));
                    else ShowMessageBox("", Translate("i18n_noPtOpen", ipAddr));
                }
            }
            finally
            {
                HideInfoBox();
                HideProgress();
            }
        }

        private async Task<bool> SetPerpetualOrFastPoe(Command cmd)
        {
            try
            {
                string action = cmd == Command.POE_PERPETUAL_ENABLE || cmd == Command.POE_FAST_ENABLE ? Translate("i18n_enable") : Translate("i18n_disable");
                string poeType = (cmd == Command.POE_PERPETUAL_ENABLE || cmd == Command.POE_PERPETUAL_DISABLE) ? Translate("i18n_ppoe") : Translate("i18n_fpoe");
                ShowProgress($"{action} {poeType} {Translate("i18n_onsl")} {selectedSlot.Name}{WAITING}");
                bool ok = false;
                await Task.Run(() => ok = restApiService.SetPerpetualOrFastPoe(selectedSlot, cmd));
                await WaitAckProgress();
                await RefreshChanges();
                return ok;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            finally
            {
                HideProgress();
                HideInfoBox();
            }
            return false;
        }

        private async Task RefreshChanges()
        {
            await Task.Run(() => restApiService.GetSystemInfo());
            RefreshSlotAndPortsView();
            EnableButtons();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        #endregion event handlers

        #region private methods

        private void SetLanguageMenuOptions()
        {
            string langFolder = Path.Combine(DataPath, LANGUAGE_FOLDER);
            string pattern = @"(.*)(strings-)(.+)(.xaml)";
            if (!Directory.Exists(langFolder)) return;
            List<string> langs = Directory.GetFiles(langFolder, "*.xaml").ToList();
            langs.Sort();

            foreach (var file in langs)
            {
                Match match = Regex.Match(file, pattern);
                if (match.Success)
                {
                    string name = match.Groups[match.Groups.Count - 2].Value;
                    string iheader = name.Substring(0, name.Length - 2) + "-" + name.Substring(name.Length - 2).ToUpper();
                    MenuItem item = new MenuItem { Header = iheader };
                    if (iheader == Config.Get("language"))
                    {
                        if (LoadLanguageFile(file))
                        {
                            MenuItem enUs = (MenuItem)_langSel.Items[0] as MenuItem;
                            enUs.IsChecked = false;
                            item.IsChecked = true;
                        }
                    }
                    item.Click += new RoutedEventHandler(LangItem_Click);
                    _langSel.Items.Add(item);
                }
            }
        }

        private bool LoadLanguageFile(string filename)
        {
            try
            {
                if (File.Exists(filename))
                {
                    ResourceDictionary stringsDict = new ResourceDictionary
                    {
                        Source = new Uri(filename, UriKind.Absolute)
                    };
                    Resources.MergedDictionaries.Remove(Strings);
                    Strings = stringsDict;
                    Resources.MergedDictionaries.Add(Strings);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                try { File.Delete(filename); } catch { }
                ShowMessageBox("Import Language", $"Error importing language: {ex.Message}", MsgBoxIcons.Error);
            }
            return false;
        }

        private void LangExport_Click(object sender, RoutedEventArgs e)
        {
            string fname = Path.GetFileName(Resources.MergedDictionaries[2].Source.ToString());
            var sfd = new SaveFileDialog
            {
                Filter = $"{Translate("i18n_langf")}|*.xaml",
                Title = Translate("i18n_langfe"),
                FileName = fname,
                InitialDirectory = Environment.SpecialFolder.MyDocuments.ToString(),
            };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    using (StringWriter sw = new StringWriter())
                    {
                        XamlWriter.Save(Resources.MergedDictionaries[2], sw);
                        string res = sw.GetStringBuilder().ToString();
                        string formRes = res.Replace("<s:String", "\n<s:String");
                        using (StreamWriter writer = new StreamWriter(sfd.FileName))
                        {
                            writer.Write(formRes);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ShowMessageBox(Translate("i18n_langf"), $"{Translate("i18n_expfail")}: {ex.Message}");
                    Logger.Error("Failed to save language dictionary", ex);
                }
            }
        }

        private void LangImport_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = $"{Translate("i18n_langf")}|*.xaml",
                Title = Translate("i18n_langfi"),
                InitialDirectory = Environment.SpecialFolder.MyDocuments.ToString(),
            };
            if (ofd.ShowDialog() == true)
            {
                string fname = Path.GetFileName(ofd.FileName);
                string target = Path.Combine(DataPath, LANGUAGE_FOLDER, fname);
                bool exists = false;
                try
                {
                    if (fname == DEFAULT_LANG_FILE) throw new Exception(Translate("i18n_defLang"));
                    if (File.Exists(target))
                    {
                        var res = ShowMessageBox(Translate("i18n_langf"), Translate("i18n_dupLang", fname), MsgBoxIcons.Question, MsgBoxButtons.YesNo);
                        if (res == MsgBoxResult.No) return;
                        exists = true;
                    }
                    File.Copy(ofd.FileName, target, true);
                    if (!LoadLanguageFile(target)) return;
                    string lang = fname.Split(new char[] { '-', '.' })[1];
                    string iheader = lang.Substring(0, 2) + "-" + lang.Substring(2).ToUpper();
                    MenuItem item;
                    if (exists)
                    {
                        item = _langSel.Items.OfType<MenuItem>().FirstOrDefault(m => m.Header.ToString() == iheader);
                    }
                    else
                    {
                        item = new MenuItem { Header = iheader };
                        item.Click += new RoutedEventHandler(LangItem_Click);
                        _langSel.Items.Add(item);
                    }
                    item.IsChecked = true;

                    foreach (MenuItem i in _langSel.Items)
                    {
                        if (i != item) i.IsChecked = false;
                    }
                }
                catch (Exception ex)
                {
                    ShowMessageBox(Translate("i18n_langf"), $"{Translate("i18n_impfail")}: {ex.Message}", MsgBoxIcons.Error);
                    Logger.Error($"Failed to import language dictionary {ofd.FileName}", ex);
                }
            }
        }

        private async void Connect()
        {
            try
            {
                if (string.IsNullOrEmpty(swModel.IpAddress))
                {
                    swModel.IpAddress = lastIpAddr;
                    swModel.Login = "admin";
                    swModel.Password = lastPwd;
                }
                if (swModel.IsConnected)
                {
                    string textMsg = $"{Translate("i18n_taDisc")} {swModel.Name}";
                    stopTrafficAnalysisReason = $"interrupted by the user before disconnecting the switch {swModel.Name}";
                    bool close = StopTrafficAnalysis(TrafficStatus.Abort, textMsg, Translate("i18n_taSave"), textMsg);
                    if (!close) return;
                    await WaitCloseTrafficAnalysis();
                    ShowProgress($"{textMsg}{WAITING}");
                    tokenSource?.Cancel();
                    await CloseRestApiService(textMsg);
                    SetDisconnectedState();
                    return;
                }
                restApiService = new RestApiService(swModel, progress);
                isClosing = false;
                DateTime startTime = DateTime.Now;
                reportResult = new WizardReport();
                opType = OpType.Connection;
                _btnCancel.Visibility = Visibility.Visible;
                tokenSource = new CancellationTokenSource();
                await Task.Run(() => restApiService.Connect(reportResult, tokenSource.Token));
                if (!swModel.IsConnected)
                {
                    List<string> ips = Config.Get("switches").Split(',').ToList();
                    ips.RemoveAll(ip => ip == swModel.IpAddress);
                    Config.Set("switches", string.Join(",", ips));
                }
                UpdateConnectedState();
                //delay to update cpu health
                DelayGetCpuHealth();
                await CheckSwitchScanResult($"{Translate("i18n_cnsw")} {swModel.Name}{WAITING}", startTime);
                if (swModel.RunningDir == CERTIFIED_DIR)
                {
                    AskRebootCertified();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            finally
            {
                HideProgress();
                HideInfoBox();
            }
        }

        private void DelayGetCpuHealth()
        {
            Task.Delay(TimeSpan.FromSeconds(Config.GetInt("wait_cpu_health", 1)), tokenSource.Token).ContinueWith(t =>
            {
                try
                {
                    var res = restApiService?.SendCommand(new CmdRequest(Command.SHOW_HEALTH, ParseType.Htable2)) as List<Dictionary<string, string>>;
                    swModel.LoadFromList(res, DictionaryType.CpuTrafficList);
                    //launch ip scanner after this
                    DelayIpScan();
                    Dispatcher.Invoke(() =>
                    {
                        this.DataContext = null;
                        this.DataContext = swModel;
                    });
                }
                catch (OperationCanceledException)
                {
                    tokenSource.Token.ThrowIfCancellationRequested();
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            });
        }

        private void DelayIpScan()
        {
            if (string.IsNullOrEmpty(swModel.IpAddress) || IsIpScanRunning) return;
            tokenSource = new CancellationTokenSource();
            Task.Delay(TimeSpan.FromSeconds(1), tokenSource.Token).ContinueWith(async task =>
            {
                try
                {
                    Dispatcher.Invoke(() => _ipscan.Focusable = true); //to trigger flashing
                    IsIpScanRunning = true;
                    Logger.Activity($"Running ip scan on switch {swModel.Name} ({swModel.IpAddress})");
                    Stopwatch watch = new Stopwatch();
                    watch.Start();
                    await IpScan.LaunchScan(swModel);
                    watch.Stop();
                    Logger.Activity($"Ip scan took {watch.Elapsed:mm\\:ss}");
                    Dispatcher.Invoke(() =>
                    {
                        RefreshSlotAndPortsView();
                    });
                    Logger.Activity($"Running TCP port scan on switch {swModel.Name}");
                    watch.Restart();
                    await Task.Run(() => CheckForOpenPorts());
                    watch.Stop();
                    var t = watch.Elapsed;
                    if (t.TotalMilliseconds >= 1000)
                        Logger.Activity($"TCP port scan took {t:mm\\:ss}");
                    else
                        Logger.Activity($"TCP port scan took {Math.Round(t.TotalMilliseconds)} ms");
                    Dispatcher.Invoke(() =>
                    {
                        RefreshSlotAndPortsView();
                    });
                }
                catch (OperationCanceledException)
                {
                    tokenSource.Token.ThrowIfCancellationRequested();
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to execute ip scan", ex);
                }
                finally
                {
                    Dispatcher.Invoke(() => _ipscan.Focusable = false); //to stop flashing
                    IsIpScanRunning = false;
                }
            });
        }

        private void CheckForOpenPorts()
        {
            object lockObj = new object();

            if (swModel?.ChassisList == null) return;
            foreach (var chas in swModel.ChassisList)
            {
                foreach (var slot in chas.Slots)
                {
                    Parallel.ForEach(slot.Ports, port =>
                    {
                        if (!string.IsNullOrEmpty(port.IpAddress))
                        {
                            int ptNo = IpScan.GetOpenPort(port.IpAddress.Replace("...", "").Trim());
                            lock (lockObj)
                            {
                                port.RemotePort = ptNo;
                            }
                        }
                    });
                }
            }
        }

        private void AskRebootCertified()
        {
            MsgBoxResult reboot = ShowMessageBox("Connection", Translate("i18n_cert"), MsgBoxIcons.Warning, MsgBoxButtons.YesNo);
            if (reboot == MsgBoxResult.Yes)
            {
                LaunchRebootSwitch();
            }
            else
            {
                _writeMemory.IsEnabled = false;
                _cfgMenuItem.IsEnabled = false;
            }
        }

        private void BuildOuiTable()
        {
            string[] ouiEntries = null;
            string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, OUI_FILE);
            if (File.Exists(filePath))
            {
                ouiEntries = File.ReadAllLines(filePath);
            }
            else
            {
                filePath = Path.Combine(DataPath, OUI_FILE);
                if (File.Exists(filePath)) ouiEntries = File.ReadAllLines(filePath);

            }
            ouiTable = new Dictionary<string, string>();
            if (ouiEntries?.Length > 0)
            {
                for (int idx = 1; idx < ouiEntries.Length; idx++)
                {
                    string[] split = ouiEntries[idx].Split(',');
                    ouiTable[split[1].ToLower()] = split[2].Trim().Replace("\"", "");
                }
            }
        }

        private async Task CloseRestApiService(string title)
        {
            try
            {
                if (restApiService == null || isClosing) return;
                isClosing = true;
                string cfgChanges = await GetSyncStatus(title);
                if (swModel?.RunningDir != CERTIFIED_DIR && swModel?.SyncStatus == SyncStatusType.NotSynchronized)
                {
                    if (AuthorizeWriteMemory(Translate("i18n_wmem"), cfgChanges))
                    {
                        DisableButtons();
                        _comImg.Visibility = Visibility.Collapsed;
                        await Task.Run(() => restApiService?.WriteMemory());
                        _comImg.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is SwitchConnectionFailure) Logger.Warn(ex.Message); else Logger.Error(ex);
            }
            finally
            {
                restApiService?.Close();
                restApiService = null;
                HideProgress();
                HideInfoBox();
            }
        }

        private async void LaunchPoeWizard()
        {
            try
            {
                DisableButtons();
                ProgressReport wizardProgressReport = new ProgressReport(Translate("i18n_pwRep"));
                reportResult = new WizardReport();
                string barText = $"{Translate("i18n_pwRun")} {Translate("i18n_onport")} {selectedPort.Name}{WAITING}";
                ShowProgress(barText);
                DateTime startTime = DateTime.Now;
                tokenSource = new CancellationTokenSource();
                string msg = $"{Translate("i18n_pwRun")} {Translate("i18n_onport")} {selectedPort.Name}{WAITING}";
                await Task.Run(() => restApiService.ScanSwitch(msg, tokenSource.Token, reportResult));
                ShowProgress(barText);
                switch (selectedDeviceType)
                {
                    case DeviceType.Camera:
                        await RunWizardCamera();
                        break;
                    case DeviceType.Phone:
                        await RunWizardTelephone();
                        break;
                    case DeviceType.AP:
                        await RunWizardWirelessRouter();
                        break;
                    default:
                        await RunWizardOther();
                        break;
                }
                WizardResult result = reportResult.GetReportResult(selectedPort.Name);
                if (result == WizardResult.NothingToDo || result == WizardResult.Fail)
                {
                    await RunLastWizardActions();
                    result = reportResult.GetReportResult(selectedPort.Name);
                }
                msg = $"{reportResult.Message}\n\n{Translate("i18n_pwDur")} {CalcStringDurationTranslate(startTime, true)}";
                await Task.Run(() => restApiService.RefreshSwitchPorts());
                if (!string.IsNullOrEmpty(reportResult.Message))
                {
                    wizardProgressReport.Title = Translate("i18n_pwRep");
                    wizardProgressReport.Type = result == WizardResult.Fail ? ReportType.Error : ReportType.Info;
                    wizardProgressReport.Message = msg;
                    progress.Report(wizardProgressReport);
                    await WaitAckProgress();
                }
                StringBuilder txt = new StringBuilder("PoE Wizard completed on port ");
                txt.Append(selectedPort.Name).Append(" with device type ").Append(selectedDeviceType).Append(":").Append(msg).Append("\nPoE status: ").Append(selectedPort.Poe);
                txt.Append(", Port Status: ").Append(selectedPort.Status).Append(", Power: ").Append(selectedPort.Power).Append(" Watts");
                if (selectedPort.EndPointDevice != null) txt.Append("\n").Append(selectedPort.EndPointDevice);
                else if (selectedPort.MacList?.Count > 0 && !string.IsNullOrEmpty(selectedPort.MacList[0]))
                    txt.Append($", {Translate("i18n_pwDevMac")} ").Append(selectedPort.MacList[0]);
                Logger.Activity(txt.ToString());
                Activity.Log(swModel, $"PoE Wizard execution {(result == WizardResult.Fail ? "failed" : "succeeded")} on port {selectedPort.Name}");
                RefreshSlotAndPortsView();
                if (result == WizardResult.Fail)
                {
                    MsgBoxResult res = ShowMessageBox(Translate("i18n_pwiz"), Translate("i18n_pwNoRes"), MsgBoxIcons.Question, MsgBoxButtons.YesNo);
                    if (res == MsgBoxResult.No) return;
                    maxCollectLogsDur = MAX_COLLECT_LOGS_WIZARD_DURATION;
                    string sftpError = await RunCollectLogs(true, selectedPort);
                    if (!string.IsNullOrEmpty(sftpError)) ShowMessageBox(barText, $"{Translate("i18n_noSftp")} {swModel.Name}!\n{sftpError}", MsgBoxIcons.Warning, MsgBoxButtons.Ok);
                }
                await GetSyncStatus(null);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            finally
            {
                EnableButtons();
                HideProgress();
                HideInfoBox();
                sftpService?.Disconnect();
                sftpService = null;
                debugSwitchLog = null;
            }
        }

        private async Task<string> RunCollectLogs(bool restartPoE, PortModel port)
        {
            ShowInfoBox($"{Translate("i18n_clog")} {swModel.Name}{WAITING}");
            string sftpError = null;
            await Task.Run(() =>
            {
                sftpService = new SftpService(swModel.IpAddress, swModel.Login, swModel.Password);
                sftpError = sftpService.Connect();
                if (string.IsNullOrEmpty(sftpError)) sftpService.DeleteFile(SWLOG_PATH);
            });
            if (!string.IsNullOrEmpty(sftpError)) return sftpError;
            string barText = Translate("i18n_cclogs");
            ShowInfoBox(barText);
            StartProgressBar(barText);
            await Task.Run(() => sftpService.DeleteFile(SWLOG_PATH));
            await GenerateSwitchLogFile(restartPoE, port);
            return null;
        }

        private async Task GenerateSwitchLogFile(bool restartPoE, PortModel port)
        {
            const double MAX_WAIT_SFTP_RECONNECT_SEC = 60;
            const double MAX_WAIT_TAR_FILE_SEC = 180;
            const double PERIOD_SFTP_RECONNECT_SEC = 30;
            try
            {
                StartProgressBar($"{Translate("i18n_clog")} {swModel.Name}{WAITING}");
                DateTime initialTime = DateTime.Now;
                debugSwitchLog = new SwitchDebugModel(reportResult, SwitchDebugLogLevel.Debug3);
                await Task.Run(() => restApiService.RunGetSwitchLog(debugSwitchLog, restartPoE, maxCollectLogsDur, port?.Name));
                StartProgressBar($"{Translate("i18n_ttar")}{WAITING}");
                long fsize = 0;
                long previousSize = -1;
                DateTime startTime = DateTime.Now;
                string strDur = string.Empty;
                string msg;
                int waitCnt = 0;
                DateTime resetSftpConnectionTime = DateTime.Now;
                while (waitCnt < 2)
                {
                    strDur = CalcStringDurationTranslate(startTime, true);
                    msg = !string.IsNullOrEmpty(strDur) ? $"{Translate("i18n_wclogs")} ({strDur}){WAITING}" : $"{Translate("i18n_wclogs")}{WAITING}";
                    if (fsize > 0) msg += $"\n{Translate("i18n_fsize")} {PrintNumberBytes(fsize)}";
                    ShowInfoBox(msg);
                    UpdateSwitchLogBar(initialTime);
                    await Task.Run(() =>
                    {
                        previousSize = fsize;
                        fsize = sftpService.GetFileSize(SWLOG_PATH);
                    });
                    if (fsize > 0 && fsize == previousSize) waitCnt++; else waitCnt = 0;
                    Thread.Sleep(2000);
                    double duration = GetTimeDuration(startTime);
                    if (fsize == 0)
                    {
                        if (GetTimeDuration(resetSftpConnectionTime) >= PERIOD_SFTP_RECONNECT_SEC)
                        {
                            sftpService.ResetConnection();
                            Logger.Warn($"Waited too long ({CalcStringDuration(startTime, true)}) for the switch {swModel.Name} to start creating the tar file!");
                            resetSftpConnectionTime = DateTime.Now;
                        }
                        if (duration >= MAX_WAIT_SFTP_RECONNECT_SEC)
                        {
                            ShowWaitTarFileError(fsize, startTime);
                            return;
                        }
                    }
                    UpdateSwitchLogBar(initialTime);
                    if (duration >= MAX_WAIT_TAR_FILE_SEC)
                    {
                        ShowWaitTarFileError(fsize, startTime);
                        return;
                    }
                }
                strDur = CalcStringDurationTranslate(startTime, true);
                string strTotalDuration = CalcStringDurationTranslate(initialTime, true);
                ShowInfoBox($"{Translate("i18n_ttar")} {Translate("i18n_fromsw")} {swModel.Name}{WAITING}");
                DateTime startDowanloadTime = DateTime.Now;
                string fname = null;
                await Task.Run(() =>
                {
                    fname = sftpService.DownloadFile(SWLOG_PATH);
                });
                UpdateSwitchLogBar(initialTime);
                if (fname == null)
                {
                    ShowMessageBox(Translate("i18n_ttar"), $"{Translate("i18n_tfail")} \"{SWLOG_PATH}\" {Translate("i18n_fromsw")} {swModel.Name}!", MsgBoxIcons.Error);
                    return;
                }
                string downloadDur = CalcStringDurationTranslate(startDowanloadTime);
                string text = $"{Translate("i18n_tloaded")} {swModel.Name}{WAITING}\n{Translate("i18n_tddur")} {downloadDur}";
                text += $", {Translate("i18n_fsize")} {PrintNumberBytes(fsize)}\n{Translate("i18n_lclogs")} {strDur}";
                ShowInfoBox(text);
                var sfd = new SaveFileDialog()
                {
                    Filter = $"{Translate("i18n_tarf")}|*.tar",
                    Title = Translate("i18n_sfile"),
                    InitialDirectory = Environment.SpecialFolder.MyDocuments.ToString(),
                    FileName = $"{Path.GetFileName(fname)}-{swModel.Name}-{DateTime.Now:MM-dd-yyyy_hh_mm_ss}.tar"
                };
                FileInfo info = new FileInfo(fname);
                if (sfd.ShowDialog() == true)
                {
                    string saveas = sfd.FileName;
                    File.Copy(fname, saveas, true);
                    File.Delete(fname);
                    info = new FileInfo(saveas);
                    debugSwitchLog.CreateTacTextFile(selectedDeviceType, info.FullName, swModel, port);
                }
                UpdateSwitchLogBar(initialTime);
                StringBuilder txt = new StringBuilder("Log tar file \"").Append(SWLOG_PATH).Append("\" downloaded from the switch ").Append(swModel.IpAddress);
                txt.Append("\n\tSaved file: \"").Append(info.FullName).Append("\"\n\tFile size: ").Append(PrintNumberBytes(info.Length));
                txt.Append("\n\tDownload duration: ").Append(downloadDur).Append("\n\tTar file creation duration: ").Append(strDur);
                txt.Append("\n\tTotal duration to generate log file in ").Append(SwitchDebugLogLevel.Debug3).Append(" level: ").Append(strTotalDuration);
                Logger.Activity(txt.ToString());
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            finally
            {
                CloseProgressBar();
                HideInfoBox();
                HideProgress();
                sftpService?.Disconnect();
                sftpService = null;
                debugSwitchLog = null;
            }
        }

        private void UpdateSwitchLogBar(DateTime initialTime)
        {
            UpdateProgressBar(GetTimeDuration(initialTime), maxCollectLogsDur);
        }

        private void ShowWaitTarFileError(long fsize, DateTime startTime)
        {
            StringBuilder msg = new StringBuilder();
            msg.Append($"{Translate("i18n_ffail")} \"").Append(SWLOG_PATH).Append($"\" {Translate("i18n_onsw")} ").Append(swModel.IpAddress).Append("\n");
            msg.Append($"{Translate("i18n_fileTout")} (").Append(CalcStringDurationTranslate(startTime, true)).Append($")\n{Translate("i18n_fsize")} ");
            if (fsize == 0) msg.Append("0 Bytes"); else msg.Append(PrintNumberBytes(fsize));
            Logger.Error(msg.ToString());
            ShowMessageBox(Translate("i18n_wclogs"), msg.ToString(), MsgBoxIcons.Error);
        }

        private void RefreshSlotAndPortsView()
        {
            DataContext = null;
            _slotsView.ItemsSource = null;
            _portList.ItemsSource = null;
            DataContext = swModel;
            _switchAttributes.Text = $"{Translate("i18n_connTo")} {swModel.Name}";
            slotView = new SlotView(swModel);
            _slotsView.ItemsSource = slotView.Slots;
            if (selectedSlot != null)
            {
                _slotsView.SelectedItem = selectedSlot;
                _portList.ItemsSource = selectedSlot?.Ports ?? new List<PortModel>();
            }
        }

        private async void RefreshSwitch()
        {
            try
            {
                DisableButtons();
                DateTime startTime = DateTime.Now;
                reportResult = new WizardReport();
                opType = OpType.Refresh;
                _btnCancel.Visibility = Visibility.Visible;
                tokenSource = new CancellationTokenSource();
                await Task.Run(() => restApiService.RefreshSwitch($"{Translate("i18n_refrsw")} {swModel.Name}", tokenSource.Token, reportResult));
                await CheckSwitchScanResult($"{Translate("i18n_refrsw")} {swModel.Name}", startTime);
                DelayGetCpuHealth();
                RefreshSlotAndPortsView();
                if (swModel.RunningDir == CERTIFIED_DIR)
                {
                    AskRebootCertified();
                }

            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            finally
            {
                EnableButtons();
                HideProgress();
                HideInfoBox();
            }
        }

        private async void WriteMemory()
        {
            try
            {
                string cfgChanges = await GetSyncStatus($"{Translate("i18n_cfgCh")} {swModel.Name}");
                if (swModel.SyncStatus == SyncStatusType.Synchronized)
                {
                    ShowMessageBox(Translate("i18n_save"), $"{Translate("i18n_nochg")} {swModel.Name}", MsgBoxIcons.Info, MsgBoxButtons.Ok);
                    return;
                }
                if (AuthorizeWriteMemory(Translate("i18n_wmem"), cfgChanges))
                {
                    DisableButtons();
                    await Task.Run(() => restApiService.WriteMemory());
                    await Task.Run(() => restApiService.GetSyncStatus());
                    DataContext = null;
                    DataContext = swModel;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
            finally
            {
                EnableButtons();
                HideProgress();
                HideInfoBox();
            }
        }

        private async Task CheckSwitchScanResult(string title, DateTime startTime)
        {
            try
            {
                Logger.Debug($"{title} completed (duration: {CalcStringDuration(startTime, true)})");
                if (reportResult.Result?.Count < 1) return;
                WizardResult result = reportResult.GetReportResult(SWITCH);
                if (result == WizardResult.Fail || result == WizardResult.Warning)
                {
                    progress.Report(new ProgressReport(title)
                    {
                        Title = title,
                        Type = result == WizardResult.Fail ? ReportType.Error : ReportType.Warning,
                        Message = $"{reportResult.Message}"
                    });
                    await WaitAckProgress();
                }
                else if (reportResult.Result?.Count > 0)
                {
                    int resetSlotCnt = 0;
                    foreach (var reports in reportResult.Result)
                    {
                        List<ReportResult> reportList = reports.Value;
                        if (reportList?.Count > 0)
                        {
                            ReportResult report = reportList[reportList.Count - 1];
                            string alertMsg = $"{report.AlertDescription}\n{Translate("i18n_turnon")}";
                            if (report?.Result == WizardResult.Warning &&
                                ShowMessageBox($"{Translate("i18n_slot")} {report.ID}", alertMsg, MsgBoxIcons.Question, MsgBoxButtons.YesNo) == MsgBoxResult.Yes)
                            {
                                await PowerSlotUpOrDown(Command.POWER_UP_SLOT, report.ID);
                                resetSlotCnt++;
                                Logger.Debug($"{report}\nSlot {report.ID} turned On");
                            }
                        }
                    }
                    if (resetSlotCnt > 0)
                    {
                        ShowProgress($"{Translate("i18n_wpUp")} {swModel.Name}");
                        await Task.Run(() => WaitSlotPortsUp());
                        RefreshSlotsAndPorts();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private async Task PowerSlotUpOrDown(Command cmd, string slotNr)
        {
            string msg = cmd == Command.POWER_UP_SLOT ? Translate("i18n_poeon") : Translate("i18n_poeoff");
            ShowProgress(msg);
            progress.Report(new ProgressReport($"{msg}{WAITING}"));
            await Task.Run(() =>
            {
                restApiService.PowerSlotUpOrDown(cmd, slotNr);
                restApiService.RefreshSwitchPorts();
            }
            );
            RefreshSlotsAndPorts();
        }

        private void RefreshSlotsAndPorts()
        {
            RefreshSlotAndPortsView();
            EnableButtons();
            HideInfoBox();
            HideProgress();
        }

        private void WaitSlotPortsUp()
        {
            string msg = $"{Translate("i18n_wpUp")} {swModel.Name}";
            DateTime startTime = DateTime.Now;
            int dur = 0;
            progress.Report(new ProgressReport($"{msg}{WAITING}"));
            while (dur < WAIT_PORTS_UP_SEC)
            {
                Thread.Sleep(1000);
                dur = (int)GetTimeDuration(startTime);
                progress.Report(new ProgressReport($"{msg} ({dur} sec){WAITING}"));
            }
            restApiService.RefreshSwitchPorts();
        }

        private async Task RunWizardCamera()
        {
            await CheckCapacitorDetection();
            if (reportResult.IsWizardStopped(selectedPort.Name)) return;
            await Enable823BT();
            if (reportResult.IsWizardStopped(selectedPort.Name)) return;
            await EnableHdmiMdi();
            if (reportResult.IsWizardStopped(selectedPort.Name)) return;
            await ChangePriority();
            if (reportResult.IsWizardStopped(selectedPort.Name)) return;
            await ResetPortPower();
            if (reportResult.IsWizardStopped(selectedPort.Name)) return;
            await Enable2PairPower();
        }

        private async Task RunWizardTelephone()
        {
            await Enable2PairPower();
            if (reportResult.IsWizardStopped(selectedPort.Name)) return;
            await ResetPortPower();
            if (reportResult.IsWizardStopped(selectedPort.Name)) return;
            await ChangePriority();
            if (reportResult.IsWizardStopped(selectedPort.Name)) return;
            await EnableHdmiMdi();
            if (reportResult.IsWizardStopped(selectedPort.Name)) return;
            await Enable823BT();
            if (reportResult.IsWizardStopped(selectedPort.Name)) return;
            await CheckCapacitorDetection();
        }

        private async Task RunWizardWirelessRouter()
        {
            await ResetPortPower();
            if (reportResult.IsWizardStopped(selectedPort.Name)) return;
            await ChangePriority();
            if (reportResult.IsWizardStopped(selectedPort.Name)) return;
            await Enable823BT();
            if (reportResult.IsWizardStopped(selectedPort.Name)) return;
            await EnableHdmiMdi();
            if (reportResult.IsWizardStopped(selectedPort.Name)) return;
            await CheckCapacitorDetection();
            if (reportResult.IsWizardStopped(selectedPort.Name)) return;
            await Enable2PairPower();
        }

        private async Task RunWizardOther()
        {
            await ResetPortPower();
            if (reportResult.IsWizardStopped(selectedPort.Name)) return;
            await ChangePriority();
            if (reportResult.IsWizardStopped(selectedPort.Name)) return;
            await Enable823BT();
            if (reportResult.IsWizardStopped(selectedPort.Name)) return;
            await EnableHdmiMdi();
            if (reportResult.IsWizardStopped(selectedPort.Name)) return;
            await Enable2PairPower();
            if (reportResult.IsWizardStopped(selectedPort.Name)) return;
            await CheckCapacitorDetection();
        }

        private async Task Enable823BT()
        {
            await RunPoeWizard(new List<Command>() { Command.CHECK_823BT });
            WizardResult result = reportResult.GetCurrentReportResult(selectedPort.Name);
            if (result == WizardResult.Skip) return;
            if (result == WizardResult.Warning)
            {
                string alertDescription = reportResult.GetAlertDescription(selectedPort.Name);
                string msg = !string.IsNullOrEmpty(alertDescription) ? alertDescription : Translate("i18n_cbtEn");
                if (ShowMessageBox(Translate("i18n_pbtEn"), $"{msg}\n{Translate("i18n_proceed")}", MsgBoxIcons.Warning, MsgBoxButtons.YesNo) == MsgBoxResult.No)
                    return;
            }
            await RunPoeWizard(new List<Command>() { Command.POWER_823BT_ENABLE });
            Logger.Debug($"Enable 802.3.bt on port {selectedPort.Name} completed on switch {swModel.Name}, S/N {swModel.SerialNumber}, model {swModel.Model}");
        }

        private async Task CheckCapacitorDetection()
        {
            await RunPoeWizard(new List<Command>() { Command.CHECK_CAPACITOR_DETECTION }, 60);
            Logger.Debug($"Enable 2-Pair Power on port {selectedPort.Name} completed on switch {swModel.Name}, S/N {swModel.SerialNumber}, model {swModel.Model}");
        }

        private async Task Enable2PairPower()
        {
            await RunPoeWizard(new List<Command>() { Command.POWER_2PAIR_PORT }, 30);
            Logger.Debug($"Enable 2-Pair Power on port {selectedPort.Name} completed on switch {swModel.Name}, S/N {swModel.SerialNumber}, model {swModel.Model}");
        }

        private async Task ResetPortPower()
        {
            await RunPoeWizard(new List<Command>() { Command.RESET_POWER_PORT }, 30);
            Logger.Debug($"Recycling Power on port {selectedPort.Name} completed on switch {swModel.Name}, S/N {swModel.SerialNumber}, model {swModel.Model}");
        }

        private async Task ChangePriority()
        {
            await RunPoeWizard(new List<Command>() { Command.CHECK_POWER_PRIORITY });
            WizardResult result = reportResult.GetCurrentReportResult(selectedPort.Name);
            if (result == WizardResult.Skip) return;
            if (result == WizardResult.Warning)
            {
                string alert = reportResult.GetAlertDescription(selectedPort.Name);
                if (ShowMessageBox(Translate("i18n_chprio"),
                                    $"{(!string.IsNullOrEmpty(alert) ? $"{alert}" : "")}\n{Translate("i18n_alprio")}\n{Translate("i18n_proceed")}",
                                    MsgBoxIcons.Warning, MsgBoxButtons.YesNo) == MsgBoxResult.No) return;
            }
            await RunPoeWizard(new List<Command>() { Command.POWER_PRIORITY_PORT });
            Logger.Debug($"Change Power Priority on port {selectedPort.Name} completed on switch {swModel.Name}, S/N {swModel.SerialNumber}, model {swModel.Model}");
        }

        private async Task EnableHdmiMdi()
        {
            await RunPoeWizard(new List<Command>() { Command.POWER_HDMI_ENABLE, Command.LLDP_POWER_MDI_ENABLE, Command.LLDP_EXT_POWER_MDI_ENABLE }, 15);
            Logger.Debug($"Enable Power over HDMI on port {selectedPort.Name} completed on switch {swModel.Name}, S/N {swModel.SerialNumber}, model {swModel.Model}");
        }

        private async Task RunLastWizardActions()
        {
            await CheckDefaultMaxPower();
            reportResult.UpdateResult(selectedPort.Name, reportResult.GetReportResult(selectedPort.Name));
        }

        private async Task CheckDefaultMaxPower()
        {
            await RunPoeWizard(new List<Command>() { Command.CHECK_MAX_POWER });
            if (reportResult.GetCurrentReportResult(selectedPort.Name) == WizardResult.Warning)
            {
                string alert = reportResult.GetAlertDescription(selectedPort.Name);
                string msg = !string.IsNullOrEmpty(alert) ? alert : $"{Translate("i18n_dmaxpw")} {selectedPort.Name}";
                if (ShowMessageBox(Translate("i18n_tmaxpw"), $"{msg}\n{Translate("i18n_proceed")}", MsgBoxIcons.Warning, MsgBoxButtons.YesNo) == MsgBoxResult.Yes)
                {
                    await RunPoeWizard(new List<Command>() { Command.CHANGE_MAX_POWER });
                }
            }
        }

        private async Task RunPoeWizard(List<Command> cmdList, int waitSec = 15)
        {
            await Task.Run(() => restApiService.RunPoeWizard(selectedPort.Name, reportResult, cmdList, waitSec));
        }

        private async Task WaitAckProgress()
        {
            await Task.Run(() =>
            {
                DateTime startTime = DateTime.Now;
                while (!reportAck)
                {
                    if (GetTimeDuration(startTime) > 120) break;
                    Thread.Sleep(100);
                }
            });
        }

        private MsgBoxResult ShowMessageBox(string title, string message, MsgBoxIcons icon = MsgBoxIcons.Info, MsgBoxButtons buttons = MsgBoxButtons.Ok)
        {
            _infoBox.Visibility = Visibility.Collapsed;
            CustomMsgBox msgBox = new CustomMsgBox(this, buttons)
            {
                Header = title,
                Message = message,
                Img = icon
            };
            msgBox.ShowDialog();
            return msgBox.Result;
        }

        private void StartProgressBar(string barText)
        {
            try
            {
                _progressBar.IsIndeterminate = false;
                Utils.StartProgressBar(progress, barText);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private void UpdateProgressBar(double currVal, double totalVal)
        {
            try
            {
                Utils.UpdateProgressBar(progress, currVal, totalVal);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private void CloseProgressBar()
        {
            try
            {
                Utils.CloseProgressBar(progress);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private void ShowInfoBox(string message)
        {
            _infoBlock.Inlines.Clear();
            _infoBlock.Inlines.Add(message);
            int maxLen = MaxLineLen(message);
            if (maxLen > 60) _infoBox.Width = 450 + maxLen - 60;
            else _infoBox.Width = 400;
            _infoBox.Visibility = Visibility.Visible;
        }

        private void ShowProgress(string message, bool isIndeterminate = true)
        {
            _progressBar.Visibility = Visibility.Visible;
            _progressBar.IsIndeterminate = isIndeterminate;
            if (!isIndeterminate)
            {
                _progressBar.Value = 0;
            }
            _status.Text = message;
        }

        private void HideProgress()
        {
            _progressBar.Visibility = Visibility.Hidden;
            _status.Text = DEFAULT_APP_STATUS;
            _progressBar.Value = 0;
        }

        private void HideInfoBox()
        {
            _infoBox.Visibility = Visibility.Collapsed;
            _btnCancel.Visibility = Visibility.Collapsed;
            _btnCancel.IsEnabled = true;
        }

        private void UpdateConnectedState()
        {
            if (swModel.IsConnected) SetConnectedState();
            else SetDisconnectedState();
        }

        private void SetConnectedState()
        {
            try
            {
                DataContext = null;
                DataContext = swModel;
                Logger.Debug($"Data context set to {swModel.Name}");
                _comImg.Source = (ImageSource)currentDict["connected"];
                _switchAttributes.Text = $"{Translate("i18n_connTo")} {swModel.Name}";
                _btnConnect.Cursor = Cursors.Hand;
                _switchMenuItem.IsEnabled = false;
                _disconnectMenuItem.Visibility = Visibility.Visible;
                _tempStatus.Visibility = Visibility.Visible;
                _cpu.Visibility = Visibility.Visible;
                slotView = new SlotView(swModel);
                _slotsView.ItemsSource = slotView.Slots;
                Logger.Debug($"Slots view items source: {slotView.Slots.Count} slot(s)");
                if (slotView.Slots.Count == 1) //do not highlight if only one row
                {
                    _slotsView.CellStyle = currentDict["gridCellNoHilite"] as Style;
                }
                else
                {
                    _slotsView.CellStyle = currentDict["gridCell"] as Style;
                }
                _slotsView.SelectedIndex = selectedSlotIndex >= 0 && _slotsView.Items?.Count > selectedSlotIndex ? selectedSlotIndex : 0;
                _slotsView.Visibility = Visibility.Visible;
                _portList.Visibility = Visibility.Visible;
                _fpgaLbl.Visibility = string.IsNullOrEmpty(swModel.Fpga) ? Visibility.Collapsed : Visibility.Visible;
                _cpldLbl.Visibility = string.IsNullOrEmpty(swModel.Cpld) ? Visibility.Collapsed : Visibility.Visible;
                bool u = swModel.Uboot != "N/A";
                bool o = swModel.Onie != "N/A";
                _ubootLbl.Visibility = u | !o ? Visibility.Visible : Visibility.Collapsed;
                _onieLbl.Visibility = !u & o ? Visibility.Visible : Visibility.Collapsed;
                _uboot.Text = u | !o ? swModel.Uboot : swModel.Onie;
                _btnConnect.IsEnabled = true;
                _comImg.ToolTip = Translate("i18n_disc_tt");
                if (swModel.TemperatureStatus == ThresholdType.Danger)
                {
                    _tempWarn.Source = new BitmapImage(new Uri(@"Resources\danger.png", UriKind.Relative));
                }
                else
                {
                    _tempWarn.Source = new BitmapImage(new Uri(@"Resources\warning.png", UriKind.Relative));
                }
                EnableButtons();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        private void EnableButtons()
        {
            ChangeButtonVisibility(true);
            _rebootLabel.Content = Translate("i18n_reboot");
            ReselectPort();
            ReselectSlot();
            if (selectedPort == null)
            {
                _btnRunWiz.IsEnabled = false;
                _btnResetPort.IsEnabled = false;
                _btnTdr.IsEnabled = false;
            }
        }

        private async Task<string> GetSyncStatus(string title)
        {
            string cfgChanges = null;
            if (!string.IsNullOrEmpty(title)) ShowInfoBox($"{title}{WAITING}");
            await Task.Run(() => cfgChanges = restApiService.GetSyncStatus());
            DataContext = null;
            DataContext = swModel;
            HideInfoBox();
            return cfgChanges;
        }

        private bool AuthorizeWriteMemory(string title, string cfgChanges)
        {
            StringBuilder text = new StringBuilder(Translate("i18n_nosync"));
            if (!string.IsNullOrEmpty(cfgChanges))
            {
                text.Append($"\n{Translate("i18n_cfgch")}:");
                text.Append(cfgChanges);
            }
            else
            {
                text.Append($"\n{Translate("i18n_nocfgch")}");
            }
            text.Append($"\n{Translate("i18n_cfsave")}");
            return ShowMessageBox(title, text.ToString(), MsgBoxIcons.Warning, MsgBoxButtons.YesNo) == MsgBoxResult.Yes;
        }

        private void SetDisconnectedState()
        {
            lastIpAddr = swModel.IpAddress;
            lastPwd = swModel.Password;
            ClearMainWindowGui();
            _rebootLabel.Content = Translate("i18n_reboot");
            _comImg.Visibility = Visibility.Visible;
            _btnConnect.Visibility = Visibility.Visible;
            _switchMenuItem.IsEnabled = true;
            _uboot.Text = string.Empty;
            _ubootLbl.Visibility = Visibility.Visible;
            _onieLbl.Visibility = Visibility.Collapsed;
            _comImg.Source = (ImageSource)currentDict["disconnected"];
            _comImg.ToolTip = Translate("i18n_recon_tt");
            restApiService = null;
        }

        private void ClearMainWindowGui()
        {
            DisableButtons();
            DataContext = null;
            swModel = new SwitchModel();
            _disconnectMenuItem.Visibility = Visibility.Collapsed;
            _tempStatus.Visibility = Visibility.Hidden;
            _cpu.Visibility = Visibility.Hidden;
            _slotsView.Visibility = Visibility.Hidden;
            _portList.Visibility = Visibility.Hidden;
            _fpgaLbl.Visibility = Visibility.Visible;
            _cpldLbl.Visibility = Visibility.Collapsed;
            _uboot.Text = string.Empty;
            _switchAttributes.Text = null;
            selectedPort = null;
            selectedPortIndex = -1;
            selectedSlotIndex = -1;
            DataContext = swModel;
        }

        private void DisableButtons()
        {
            ChangeButtonVisibility(false);
            _btnRunWiz.IsEnabled = false;
            _btnResetPort.IsEnabled = false;
            _btnTdr.IsEnabled = false;
        }

        private void ChangeButtonVisibility(bool val)
        {
            _snapshotMenuItem.IsEnabled = val;
            _vcbootMenuItem.IsEnabled = val;
            _refreshSwitch.IsEnabled = val;
            _writeMemory.IsEnabled = val;
            _reboot.IsEnabled = val;
            _traffic.IsEnabled = val;
            _collectLogs.IsEnabled = val;
            _psMenuItem.IsEnabled = val;
            _searchDevMenuItem.IsEnabled = val;
            _vlanMenuItem.IsEnabled = val;
            _factoryRst.IsEnabled = val;
            _cfgMenuItem.IsEnabled = val;
            _upgradeMenuItem.IsEnabled = val;
            _cfgBackup.IsEnabled = val;
            _cfgRestore.IsEnabled = val;
        }

        private void ReselectPort()
        {
            if (selectedPort != null && _portList.Items?.Count > selectedPortIndex)
            {
                _portList.SelectionChanged -= PortSelection_Changed;
                _portList.SelectedItem = _portList.Items[selectedPortIndex];
                _portList.SelectionChanged += PortSelection_Changed;
                _btnRunWiz.IsEnabled = selectedPort.Poe != PoeStatus.NoPoe;
                _btnResetPort.IsEnabled = true;
                _btnTdr.IsEnabled = selectedPort.Poe != PoeStatus.NoPoe;

            }
        }

        private void ReselectSlot()
        {
            if (selectedSlot != null && selectedSlotIndex >= 0 && _slotsView.Items?.Count > selectedSlotIndex)
            {
                _slotsView.SelectionChanged -= SlotSelection_Changed;
                _slotsView.SelectedItem = _slotsView.Items[selectedSlotIndex];
                _slotsView.SelectionChanged += SlotSelection_Changed;
                _btnRunWiz.IsEnabled = selectedPort != null && selectedPort.Poe != PoeStatus.NoPoe;
                _btnResetPort.IsEnabled = true;
                _btnTdr.IsEnabled = selectedPort != null && selectedPort.Poe != PoeStatus.NoPoe;
            }
        }

        #endregion private methods
    }
}
