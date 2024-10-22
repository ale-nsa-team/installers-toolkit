using PoEWizard.Data;
using System.Windows;
using System.Windows.Input;
using static PoEWizard.Data.Constants;

namespace PoEWizard.Components
{
    /// <summary>
    /// Interaction logic for ChangePwd.xaml
    /// </summary>
    public partial class ChangePwd : Window
    {
        private string pwd;
        public string CurrPwd { get; set; }
        public string NewPwd { get; set; }
        public string CfrmPwd { get; set; }
        
        public ChangePwd(Window owner, string pwd)
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

            this.pwd = pwd;
            DataContext = this;
            this.Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            _currpwd.Focus();
        }

        private void Pwd_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) BtnOk_Click(sender, e);
            if (e.Key == Key.Escape) BtnCancel_Click(sender, e);
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            if (CurrPwd != pwd) DisplayWarning(Utils.Translate("i18n_invCpwd"));
            else if (NewPwd != CfrmPwd) DisplayWarning(Utils.Translate("i18n_noMatch"));
            else if (string.IsNullOrWhiteSpace(NewPwd)) DisplayWarning(Utils.Translate("i18n_emptyPwd"));
            else
            {
                this.DialogResult = true;
                this.Close();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void DisplayWarning(string message)
        {
            CustomMsgBox mb = new CustomMsgBox(this.Owner, MsgBoxButtons.Ok)
            {
                Title = "Change Password",
                Message = message,
                Img = MsgBoxIcons.Warning
            };
            mb.ShowDialog();
        }
    }
}
