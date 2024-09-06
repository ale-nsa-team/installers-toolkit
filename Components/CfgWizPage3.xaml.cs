﻿using PoEWizard.Device;
using System.Windows;
using System.Windows.Controls;
using static PoEWizard.Data.Constants;

namespace PoEWizard.Components
{
    /// <summary>
    /// Interaction logic for CfgWizPage3.xaml
    /// </summary>
    public partial class CfgWizPage3 : Page
    {
        private readonly FeatureModel features;

        public CfgWizPage3(FeatureModel features)
        {
            InitializeComponent();
            if (MainWindow.theme == ThemeType.Dark)
            {
                Resources.MergedDictionaries.Remove(Resources.MergedDictionaries[0]);
            }
            else
            {
                Resources.MergedDictionaries.Remove(Resources.MergedDictionaries[1]);
            }

            this.features = features;
            DataContext = features;
        }

        private void PoE_Changed(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            if (cb.IsKeyboardFocusWithin)
            {
               if (cb.IsChecked == false)
                {
                    CustomMsgBox dlg = new CustomMsgBox(MainWindow.Instance, MsgBoxButtons.YesNo)
                    {
                        Header = "PoE",
                        Message = "This operation will turn off power on all PoE ports\nDo you want to continue?",
                        Img = MsgBoxIcons.Warning
                    };
                    if (dlg.ShowDialog() == false)
                    {
                        cb.IsChecked = false;
                    }
                }
            }
        }
    }
}
