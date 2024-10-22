using PoEWizard.Device;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using static PoEWizard.Data.Constants;
using static PoEWizard.Data.Utils;

namespace PoEWizard.Components
{
    /// <summary>
    /// Interaction logic for SearchPort.xaml
    /// </summary>
    public partial class SearchPort : Window
    {
        public class PortViewModel
        {
            public PortModel Port { get; set; }
            public string SearchText { get; set; }

            public PortViewModel(PortModel port, string searchText)
            {
                this.Port = port;
                this.SearchText = searchText;
            }
        }

        private List<string> deviceMacList = new List<string>();
        private PortModel currPort = null;
        private readonly string any;

        public string SearchText { get; set; }
        public string DeviceMac => $"{(!string.IsNullOrEmpty(this.SearchText) ? $"\"{this.SearchText}\"" : any)}";
        public ObservableCollection<PortViewModel> PortsFound { get; set; }
        public PortModel SelectedPort { get; set; }
        public bool IsMacAddress { get; set; }
        public int NbMacAddressesFound => !string.IsNullOrEmpty(this.SearchText) ? deviceMacList.Count : this.NbTotalMacAddressesFound;
        public int NbPortsFound { get; set; }
        public int NbTotalMacAddressesFound { get; set; }

        public SearchPort(SwitchModel device, string macAddress)
        {
            this.SearchText = !string.IsNullOrEmpty(macAddress) ? macAddress.ToLower().Trim() : string.Empty;
            InitializeComponent();
            DataContext = this;
            if (MainWindow.Theme == ThemeType.Dark)
            {
                Resources.MergedDictionaries.Remove(Resources.MergedDictionaries[0]);
            }
            else
            {
                Resources.MergedDictionaries.Remove(Resources.MergedDictionaries[1]);
            }
            Resources.MergedDictionaries.Remove(Resources.MergedDictionaries[1]);
            Resources.MergedDictionaries.Add(MainWindow.Strings);

            any = (string)MainWindow.Strings["i18n_any"];
            this.SelectedPort = null;
            SearchMacAddress(device, macAddress);
            if (this.PortsFound.Count == 1) this.SelectedPort = this.PortsFound[0].Port;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            this.MouseDown += delegate { this.DragMove(); };

            this.Height = this._portsListView.ActualHeight + 115;
            this.Top = this.Owner.Height > this.Height ? this.Owner.Top + (this.Owner.Height - this.Height) / 2 : this.Top;
        }

        private void SearchMacAddress(SwitchModel switchModel, string macAddr)
        {
            this.IsMacAddress = IsValidMacSequence(this.SearchText);
            this.PortsFound = new ObservableCollection<PortViewModel>();
            this.NbTotalMacAddressesFound = 0;
            deviceMacList = new List<string>();
            foreach (var chas in switchModel.ChassisList)
            {
                foreach (var slot in chas.Slots)
                {
                    foreach (var port in slot.Ports)
                    {
                        this.currPort = port;
                        int foundCnt = 0;
                        if (port.EndPointDevicesList?.Count > 0)
                        {
                            foreach (EndPointDeviceModel device in port.EndPointDevicesList)
                            {
                                if (string.IsNullOrEmpty(device.MacAddress)) continue;
                                if (!device.MacAddress.Contains(","))
                                {
                                    if (IsDevicePortFound(device.MacAddress, device))
                                    {
                                        AddToDevicesList(device.MacAddress);
                                        foundCnt++;
                                    }
                                }
                                else
                                {
                                    if (AddMacFound(new List<string>(device.MacAddress.Split(',')))) foundCnt++;
                                }
                            }
                            if (foundCnt > 0 && this.PortsFound.FirstOrDefault(p => p.Port == port) == null)
                            {
                                this.PortsFound.Add(new PortViewModel(port, SearchText));
                                this.NbTotalMacAddressesFound += port.MacList.Count;
                            }
                        }
                    }
                }
            }
            this.NbPortsFound = this.PortsFound.Count;
        }

        private bool AddMacFound(List<string> macList)
        {
            if (macList.Count == 0) return false;
            if (string.IsNullOrEmpty(this.SearchText)) return true;
            bool found = false;
            foreach (string macAddr in macList)
            {
                if (IsDevicePortFound(macAddr))
                {
                    found = true;
                    AddToDevicesList(macAddr);
                }
            }
            return found;
        }

        private void AddToDevicesList(string macAddr)
        {
            if (!this.deviceMacList.Contains($"{this.currPort.Name}-{macAddr}")) this.deviceMacList.Add($"{this.currPort.Name}-{macAddr}");
        }

        private bool IsDevicePortFound(string macAddr, EndPointDeviceModel device = null)
        {
            if (string.IsNullOrEmpty(macAddr)) return false;
            if (string.IsNullOrEmpty(this.SearchText)) return true;
            if (this.IsMacAddress)
            {
                return macAddr.StartsWith(this.SearchText);
            }
            else
            {
                if (device != null)
                {
                    string nameVendor = device.Name.ToLower();
                    if (nameVendor.Contains(this.SearchText)) return true;
                    nameVendor = device.Vendor.ToLower();
                    if (nameVendor.Contains(this.SearchText)) return true;
                }
                return GetVendorName(macAddr).ToLower().Contains(this.SearchText);
            }
        }

        private void PortSelection_Changed(Object sender, RoutedEventArgs e)
        {
            if (_portsListView.SelectedItem is PortModel port)
            {
                SelectedPort = port;
            }
        }

        private void Mouse_DoubleClick(Object sender, RoutedEventArgs e)
        {
            if (_portsListView.SelectedItem is PortViewModel port)
            {
                SelectedPort = port.Port;
                this.Close();
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
