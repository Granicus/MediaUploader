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

namespace WpfApplication1
{
  /// <summary>
  /// Interaction logic for LoginWindow.xaml
  /// </summary>
  public partial class LoginWindow : Window
  {
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

    public string Host
    {
      get { return this.txtHost.Text; }
    }

    public string Login
    {
      get { return this.txtLogin.Text; }
    }

    public string Password
    {
      get { return this.txtPassword.Password; }
    }

  }
}
