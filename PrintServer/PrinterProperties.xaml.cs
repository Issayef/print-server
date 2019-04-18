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

namespace PrintServer
{
    /// <summary>
    /// Логика взаимодействия для PrinterProperties.xaml
    /// </summary>
    public partial class PrinterProperties : Window
    {
        public PrinterProperties()
        {
            InitializeComponent();
            this.SetPosition(position);
            
        }
        PrintServerPostion position = PrintServerPostion.tpRightBottom;
        private void SetPosition(PrintServerPostion value)
        {
            double l = SystemParameters.WorkArea.Left;
            double t = SystemParameters.WorkArea.Top;
            double w = SystemParameters.WorkArea.Width;
            double h = SystemParameters.WorkArea.Height;
            double x = SystemParameters.WorkArea.X;
            double y = SystemParameters.WorkArea.Y;

            switch (value)
            {
                case PrintServerPostion.tpLeftTop:
                    Left = l;
                    Top = t;
                    break;

                case PrintServerPostion.tpRightTop:
                    Left = w - Width + x;
                    Top = t;
                    break;

                case PrintServerPostion.tpLeftBottom:
                    Left = l;
                    Top = h - Height + y;
                    break;

                case PrintServerPostion.tpRightBottom:
                    Left = w - Width + x;
                    Top = h - Height + y;
                    break;
                case PrintServerPostion.tpScreenSenter:
                    Left = (w - Width) / 2 + x;
                    Top = (h - Height) / 2 + y;
                    break;
            }

            this.position = value;
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void check_book_Checked(object sender, RoutedEventArgs e)
        {
            check_album.IsChecked = false;
        }

        private void chek_album_Checked(object sender, RoutedEventArgs e)
        {
            check_book.IsChecked = false;
        }

        private void check_colored_Checked(object sender, RoutedEventArgs e)
        {
            check_noncolored.IsChecked = false;
        }

        private void check_noncolored_Checked(object sender, RoutedEventArgs e)
        {
            check_colored.IsChecked = false;
        }
    }
}
