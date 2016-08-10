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

namespace GranicusMediaUploader
{
    /// <summary>
    /// Interaction logic for Results.xaml
    /// </summary>
    public partial class Results : Window
    {
        public Results()
        {
            InitializeComponent();
        }

        public string Text
        {
            get
            {
                TextBox t = (TextBox)this.FindName("resultsText");
                return t.Text;
            }
            set
            {
                TextBox t = (TextBox)this.FindName("resultsText");
                t.Text = value;
            }
        }
    }
}
