
using System.Windows;
using 文件管理器Slim.XFSlim;


namespace 文件管理器Slim
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            SelectVolumeWindow svw = new SelectVolumeWindow();
            svw.ShowDialog();

            ///打开用于XFSlim的窗口
            XFSlimWindow xfssw = new XFSlimWindow();
            xfssw.ShowDialog();
            this.Close();
        }
    }
}
