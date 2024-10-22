using PoEWizard.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using static PoEWizard.Data.Constants;

namespace PoEWizard.Components
{
    /// <summary>
    /// Interaction logic for Login.xaml
    /// </summary>
    public partial class Login : Window
    {
        private readonly ImageSource eye_open;
        private readonly ImageSource eye_closed;
        private readonly Config cfg;

        public string IpAddress { get; set; }
        public List<string> IpList { get; set; }
        public string User { get; set; }
        public string Password { get; set; }

        public Login(String user, Config config)
        {
            InitializeComponent();
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

            eye_open = (ImageSource)Resources.MergedDictionaries[0]["eye_open"];
            eye_closed = (ImageSource)Resources.MergedDictionaries[0]["eye_closed"];

            IpList = new List<string>();
            cfg = config;
            string swList = cfg.Get("switches");
            if ( swList != null)
            {
                IpList = swList.Split(',').ToList();
            }
 
            this.DataContext = this;
            _ipAddress.ItemsSource = IpList;
            User = user;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (HasErrors()) return;
            if (!IpList.Contains(IpAddress))
            {
                if (IpList.Count >= MAX_LIST_SIZE)
                {
                    IpList.RemoveAt(IpList.Count - 1);
                }
                IpList.Add(IpAddress);
                IpList.Sort();
                cfg.Set("switches", string.Join(",", IpList));
            }
            this.DialogResult = true;
            this.Close();
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(IpAddress))
            {
                IpAddress = IpList.ElementAtOrDefault(0) ?? string.Empty;
                _ipAddress.Text = IpAddress;
            }
            _ipAddress.Focus();
        }

        private void TextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !HasErrors()) BtnOk_Click(sender, e);
        }

        private void MaskedPwdEnter(object sender, RoutedEventArgs e)
        {
            PasswordBox pb = sender as PasswordBox;
            e.Handled = true;
            pb.Focus();
            pb.SelectAll();
        } 

        private void ClearPwdEnter(object sender, RoutedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            e.Handled = true;
            tb.Focus();
            tb.SelectAll();
        }

        private void ShowPwd(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn?.Content is Image img)
            {
                bool isEnabled = img.Source == eye_closed;
                img.Source = isEnabled ? eye_open : eye_closed;
                if (isEnabled)
                {
                    _clearPwd.Visibility = Visibility.Hidden;
                    _maskedPwd.Visibility = Visibility.Visible;
                    _maskedPwd.Password = Password;
                }
                else
                {
                    _clearPwd.Visibility = Visibility.Visible;
                    _maskedPwd.Visibility = Visibility.Hidden;
                    _clearPwd.Text = Password;

                }
            }
        }

        private bool HasErrors()
        {
            bool ipErr = string.IsNullOrEmpty(_ipAddress.Text) || _ipAddress.GetBindingExpression(ComboBox.TextProperty).HasError;
            bool nameErr = string.IsNullOrEmpty(_username.Text.Trim());
            bool pwdErr = string.IsNullOrEmpty(_clearPwd.Text.Trim());

            return ipErr || nameErr || pwdErr;
        } 

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
