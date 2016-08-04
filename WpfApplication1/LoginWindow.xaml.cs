using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Granicus.MediaManager.SDK;
using System.Configuration;
using System.Diagnostics;
using System.Windows.Navigation;

namespace WpfApplication1
{
  /// <summary>
  /// Interaction logic for LoginWindow.xaml
  /// </summary>
  public partial class LoginWindow : Window
  {
        private string _host = "";

    public LoginWindow()
    {
      InitializeComponent();
    }

    private void btnCancel_Click(object sender, RoutedEventArgs e)
    {
      DialogResult = false;
    }

    private void btnLogin_Click(object sender, RoutedEventArgs e)
    {
      DialogResult = true;
    }

        private void ComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            var comboBox = sender as ComboBox;

            // set the list of domains from the app config file
            try
            {
                var appSettings = ConfigurationManager.AppSettings;
                string domains = appSettings["domains"];

                if (domains == null)
                {
                    domains = "www.granicus.com";
                }
                List<string> listDomains = new List<string>();
                listDomains.AddRange(domains.Split(','));
                comboBox.ItemsSource = listDomains;
            }
            catch
            {
                comboBox.ItemsSource = new List<string>() { "www.granicus.com" };
            }

            comboBox.SelectedIndex = 0;
            _host = comboBox.SelectedItem as string;
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // ... Get the ComboBox.
            var comboBox = sender as ComboBox;

            // ... Set SelectedItem as Window Title.
            _host = comboBox.SelectedItem as string;
        }


        public string Host
    {
      get { return _host; }
    }

    public string Login
    {
      get { return this.txtLogin.Text; }
    }

    public string Password
    {
      get { return this.txtPassword.Password; }
    }

        public void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo("http://" + _host));
            e.Handled = true;
        }

    }
}
