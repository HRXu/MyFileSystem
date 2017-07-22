using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using XFileSystemSlim;

namespace 文件管理器Slim.XFSlim
{
    class Manager
    {
        static public ObservableCollection<DisplayContent> CurrentDirFilesList = new ObservableCollection<DisplayContent>();

        /// <summary>
        /// 当前目录
        /// </summary>
        static XFileSystemSlim.FileInfo currentDir;
        /// <summary>
        /// 卷信息
        /// </summary>
        static XFileSystemSlim.VolumeInfo volume;

        /// <summary>
        /// 初始化
        /// </summary>
        public static void Init()
        {
            currentDir= Operation.GetFileInfo(Data.DriveName, Data.Start);
            volume = new VolumeInfo()
            {
                freetableStart = Data.Start+1,//空闲表位于第1簇，根目录位于第0簇
                start = Data.Start
            };
        }
        /// <summary>
        /// 载入当前目录信息
        /// </summary>
        public static void LoadDirContent()
        {
            Manager.CurrentDirFilesList.Clear();

            var res = Operation.GetContent(Data.DriveName, currentDir);
            foreach (var block in res)
            {
                XFileSystemSlim.FileInfo f = Operation.GetFileInfo(Data.DriveName, block);
                if (f.Type == 0)
                {
                    CurrentDirFilesList.Add(new DisplayContent
                    {
                        Name = f.Name,
                        Icon = "\xE838",
                        Info = f,
                    });
                }
                else if (f.Type == 1)
                {
                    CurrentDirFilesList.Add(new DisplayContent
                    {
                        Name = f.Name,
                        Icon = "\xE7c3",
                        Info = f,
                    });
                }
            }
        }
        /// <summary>
        /// 添加文件
        /// </summary>
        public static void AddFile()
        {
            var openfiledialog = new OpenFileDialog();
            var res = openfiledialog.ShowDialog();

            if (res == System.Windows.Forms.DialogResult.Cancel)
            {
                return;
            }

            Stream fs = openfiledialog.OpenFile();
            BinaryReader sr = new BinaryReader(fs);

            //返回文件名和文件类型
            var s = openfiledialog.SafeFileName.Split('.');

            List<byte> ls = new List<byte>();
            try
            {
                while (true)
                {
                    ls.Add(sr.ReadByte());
                }
            }
            catch (Exception)
            {
                ;
            }
            //文件内容
            var content = ls.ToArray();

            var loc = Operation.AddFile(currentDir, Data.DriveName, volume);
            //var loc = myFileSystem.AddDir(Data.current, Data.DriveName, Data.Volume);
            XFileSystemSlim.FileInfo fileinfo = new XFileSystemSlim.FileInfo()
            {
                Name = s[0],
                Extension = s[1],
                Location = loc
            };
            Operation.WriteFile(Data.DriveName, volume,ref fileinfo, content);
            Operation.SetFileInfo(Data.DriveName, fileinfo);
            return;
        }
    }
    /// <summary>
    /// 用于存储的结构体，图标等
    /// </summary>
    public class DisplayContent
    {
        public string Icon { get; set; }
        public string Name { get; set; }
        public XFileSystemSlim.FileInfo Info { get; set; }
    }
}
