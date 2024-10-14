﻿using PoEWizard.Data;
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using static PoEWizard.Data.Constants;

namespace PoEWizard.Components
{
    /// <summary>
    /// Interaction logic for PassCode.xaml
    /// </summary>
    public partial class PassCode : Window
    {
        public string SavedPassword;
        public string Password { get; set; }
        
        public PassCode(Window owner)
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

            DataContext = this;
            this.Owner = owner;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SavedPassword = GetPassword();
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            _pwd.Focus();
        }

        private void Pwd_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) BtnOk_Click(sender, e);
            if (e.Key == Key.Escape) BtnCancel_Click(sender, e);
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void ChgPwd_Click(object sender, RoutedEventArgs e)
        {
            ChangePwd cp = new ChangePwd(this.Owner, SavedPassword);
            if (cp.ShowDialog() == true && cp.NewPwd != SavedPassword) SavePassword(cp.NewPwd);
        }

        private string GetPassword()
        {
            string filepath = Path.Combine(MainWindow.DataPath, "app.cfg");
            if (File.Exists(filepath))
            {
                string encPwd = File.ReadAllText(filepath);
                if (!string.IsNullOrEmpty(encPwd)) return Utils.DecryptString(encPwd);
            }
            return Constants.DEFAULT_PASS_CODE;
        }

        private void SavePassword(string newpwd)
        {
            try
            {
                if (newpwd != null)
                {
                    string filepath = Path.Combine(MainWindow.DataPath, "app.cfg");
                    string np = Utils.EncryptString(newpwd);
                    File.WriteAllText(filepath, np);
                    SavedPassword = newpwd;
                    this.DataContext = null;
                    Password = newpwd;
                    this.DataContext = this;
                }
            }
            catch (Exception ex)
            {
                CustomMsgBox cm = new CustomMsgBox(this.Owner)
                {
                    Title = (string)MainWindow.Strings["i18n_chcode"],
                    Message = $"{(string)MainWindow.Strings["i18n_codeErr"]}: {ex.Message}"
                };
                cm.ShowDialog();
            }
        }
    }
}
