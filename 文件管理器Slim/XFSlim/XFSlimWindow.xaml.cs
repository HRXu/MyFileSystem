using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using XFileSystemSlim;

namespace 文件管理器Slim.XFSlim
{
    /// <summary>
    /// XFSlimWindow.xaml 的交互逻辑
    /// </summary>
    public partial class XFSlimWindow : Window
    {
        public XFSlimWindow()
        {
            InitializeComponent();
            this.fileListBox.ItemsSource = Manager.CurrentDirFilesList;

            Manager.Init();
            Manager.LoadDirContent();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            Manager.AddFile();
            Manager.LoadDirContent();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            Manager.DeleteFile((fileListBox.SelectedItem as DisplayContent).Info);
            Manager.LoadDirContent();
        }
    }
}
