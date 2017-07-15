using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using XFileSystem;

namespace 文件管理器
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            SelectVolumeWindow svw = new SelectVolumeWindow();
            svw.ShowDialog();
            InitializeComponent();
            init();
            return;
        }

        void init()
        {
            Data.Volume = myFileSystem.GetVolumeInfo(Data.DriveName, Data.StartSector);
            fileListBox.ItemsSource = Data.list;
            Data.current = myFileSystem.GetFileInfo(Data.DriveName,
                myFileSystem.transfer(Data.StartSector, 1, Data.Volume.Cluster),
                Data.Volume.Cluster
                ) as DirectInfo;
            Controller.LoadDirContent();
        }

        /// <summary>
        /// 导入文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ImportFile_Click(object sender, RoutedEventArgs e)
        {
               Controller.AddFile();
               Controller.LoadDirContent();
        }

        /// <summary>
        /// 新建文件夹
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CreateNewFolder_Click(object sender, RoutedEventArgs e)
        {
            Controller.AddNewDir();
            Controller.LoadDirContent();
        }

        /// <summary>
        /// 导出文件或打开文件夹
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void fileListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var tmp = fileListBox.SelectedItem as DisplayContent;
            if (tmp == null) return;
            if (tmp.Info.Type != 0)
            {
                Controller.Export(tmp.Info);
                return;
            }
            else
            {
                Data.naviStack.Push(Data.current);
                Data.current = myFileSystem.GetFileInfo(Data.DriveName, tmp.Info.Location, Data.Volume.Cluster) as DirectInfo;
                Controller.LoadDirContent();
                return;
            }
        }

        /// <summary>
        /// 导出文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ExportFile_Click(object sender, RoutedEventArgs e)
        {
            var tmp = fileListBox.SelectedItem as DisplayContent;
            if (tmp == null) return;
            if (tmp.Info.Type != 0)
            {
                Controller.Export(tmp.Info);
                return;
            }
            return;
        }

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            var tmp = fileListBox.SelectedItem as DisplayContent;
            if (tmp == null) return;
            if (tmp.Info.Type != 0) 
            {
                Controller.Delete(tmp.Info);
                Controller.LoadDirContent();
                return;
            }
        }

        /// <summary>
        /// 重命名
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            var tmp = fileListBox.SelectedItem as DisplayContent;
            if (tmp == null) return;
            InputWindow iw = new InputWindow();
            if (iw.ShowDialog() == false) return;
            Controller.ChangeName(tmp.Info, iw.Filename);
            Controller.LoadDirContent();
        }

        private void backButton_Click(object sender, RoutedEventArgs e)
        {
            if (Data.naviStack.Count == 0)
            {
                return;
            }
            Data.current = Data.naviStack.Pop();
            Controller.LoadDirContent();
        }
    }
    public class DisplayContent
    {
        public string Icon { get; set; }
        public string Name { get; set; }
        public FATInfo Info { get; set; }
    }
}
