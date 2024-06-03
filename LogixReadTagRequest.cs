using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Logix
{
    public class LogixReadTagRequest
    {
        public Logix.TagTypes DataType;
        public string TagName;

        public LogixReadTagRequest(Logix.TagTypes dataType, string tagName)
        {
            DataType = dataType;
            TagName = tagName;
        }
    }
}
