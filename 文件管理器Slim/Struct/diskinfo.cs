using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace 文件管理器Slim.Struct
{
    class diskinfo
    {
        public string diskname { get; set; }
        public UInt32 sectors { get; set; }
        public string model { get; set; }
        public UInt32 partition { get; set; }
    }
}
