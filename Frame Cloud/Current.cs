using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrameCloud
{
    public static class Current
    {
        public static bool Debug { get; private set; }

        public static void SetDebugState(bool state)
        {
            Debug = state;
        }
    }
}
