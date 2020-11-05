using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Imazen.Common.Instrumentation.Support
{
    struct SegmentPrecision
    {
        /// <summary>
        /// Inclusive (microseconds, 1 millionth of a second)
        /// </summary>
        public long Above { get; set; }
        public long Loss { get; set; }
    }

}
