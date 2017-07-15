using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using XFileSystem;

namespace 文件管理器
{
    class Controller
    {
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

            //判断文件类型
            Data.Type type = Data.Type.File;
            switch (s[1])
            {
                case "flac":
                    type = Data.Type.Sound;
                    break;
                case "mp3":
                    type = Data.Type.Sound;
                    break;
                case "txt":
                    type = Data.Type.Text;
                    break;
                case "jpg":
                    type = Data.Type.Image;
                    break;
                case "png":
                    type = Data.Type.Image;
                    break;
                default:
                    break;
            }
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
            var content = ls.ToArray();

            var loc = myFileSystem.AddDir(Data.current, Data.DriveName, Data.Volume);
            XFileSystem.FileInfo fileinfo = new XFileSystem.FileInfo()
            {
                AccessCode = 255,
                Name = s[0],
                Author = "xu",//改系统名
                LastEditor = "xu",
                AccessMode = 255,
                Extension = s[1],
                Type = (byte)type,
                Location = myFileSystem.transfer(Data.Volume.SectorStart, loc, Data.Volume.Cluster)
            };
            myFileSystem.WriteFile(Data.DriveName, Data.Volume, ref fileinfo, content);
            myFileSystem.SetFileInfo(Data.DriveName, fileinfo);
            return;
        }

        /// <summary>
        /// 新建文件夹
        /// </summary>
        /// <param name="dirname">新文件夹名</param>
        public static void AddNewDir()
        {
            var loc = myFileSystem.AddDir(Data.current, Data.DriveName, Data.Volume);
            DirectInfo dirinfo = new DirectInfo()
            {
                AccessCode = 255,
                Name = "新建文件夹",
                Author = "xu",//改系统名
                LastEditor = "xu",
                AccessMode = 255,
                Type = 0,
                Location = myFileSystem.transfer(Data.Volume.SectorStart, loc, Data.Volume.Cluster)
            };
            myFileSystem.WriteIn(Data.DriveName, Data.Volume, loc, new byte[Data.Volume.Cluster]);
            myFileSystem.SetFileInfo(Data.DriveName, dirinfo);
        }

        /// <summary>
        /// 载入目录信息
        /// </summary>
        public static void LoadDirContent()
        {
            Data.list.Clear();
            var res = myFileSystem.GetDirContent(Data.DriveName, Data.current.Location, Data.Volume);
            foreach (var item in res)
            {
                if (item.Type == 0)
                {
                    Data.list.Add(new DisplayContent
                    {
                        Name = item.Name,
                        Icon = "\xE838",
                        Info = item,
                    });
                }
                else if (item.Type == 1)
                {
                    Data.list.Add(new DisplayContent
                    {
                        Name = item.Name,
                        Icon = "\xE7c3",
                        Info = item,
                    });
                }
                else if (item.Type == (byte)Data.Type.Sound)
                {
                    Data.list.Add(new DisplayContent
                    {
                        Name = item.Name,
                        Icon = "\xE189",
                        Info = item,
                    });
                }
                else if (item.Type == (byte)Data.Type.Text)
                {
                    Data.list.Add(new DisplayContent
                    {
                        Name = item.Name,
                        Icon = "\xE7Bc",
                        Info = item,
                    });
                }
                else if (item.Type == (byte)Data.Type.Image)
                {
                    Data.list.Add(new DisplayContent
                    {
                        Name = item.Name,
                        Icon = "\xEb9f",
                        Info = item,
                    });
                }
            }
        }

        /// <summary>
        /// 导出
        /// </summary>
        /// <param name="fileinfo"></param>
        public static void Export(FATInfo fileinfo)
        {
            var info = myFileSystem.GetFileInfoA(Data.DriveName, fileinfo.Location, Data.Volume.Cluster);
            var res = myFileSystem.ReadFile(Data.DriveName, Data.Volume, info);

            //保存
            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "|*." + info.Extension;
            saveFileDialog.FileName = info.Name + "." + info.Extension;
            saveFileDialog.RestoreDirectory = true;
            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Stream fs = saveFileDialog.OpenFile();
                BinaryWriter writer = new BinaryWriter(fs);
                writer.Write(res.ToArray());
                writer.Close();
            }
            return;
        }

        /// <summary>
        /// 删除
        /// </summary>
        /// <param name="fileinfo"></param>
        public static void Delete(FATInfo fileinfo)
        {
            var info = myFileSystem.GetFileInfoA(Data.DriveName, fileinfo.Location, Data.Volume.Cluster);
            var res=myFileSystem.DeleteFile(Data.DriveName, Data.Volume, info);
            res=myFileSystem.DeleteInDir(Data.DriveName, Data.Volume, info.Location,Data.current);
        }

        /// <summary>
        /// 重命名
        /// </summary>
        /// <param name="fileinfo"></param>
        /// <param name="name"></param>
        public static void ChangeName(FATInfo fileinfo,string name)
        {
            if (fileinfo.Type==0) //目录型
            {
                var info = myFileSystem.GetFileInfo(Data.DriveName, fileinfo.Location, Data.Volume.Cluster) as DirectInfo;
                info.Name = name;
                myFileSystem.SetFileInfo(Data.DriveName, info);
                return;
            }
            else //实体文件型
            {
                var info = myFileSystem.GetFileInfoA(Data.DriveName, fileinfo.Location, Data.Volume.Cluster);
                info.Name = name;
                myFileSystem.SetFileInfo(Data.DriveName, info);
                return;
            }           
        }
    }
}
