using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TexturePlugin
{
    public class PInvoke
    {
        [DllImport("textoolwrap")]
        public static extern uint DecodeByCrunchUnity(IntPtr data, IntPtr buf, int mode, uint width, uint height, uint byteSize);

        [DllImport("textoolwrap")]
        public static extern uint DecodeByPVRTexLib(IntPtr data, IntPtr buf, int mode, uint width, uint height);

        [DllImport("textoolwrap")]
        public static extern uint EncodeByCrunchUnity(IntPtr data, ref int checkoutId, int mode, int level, uint width, uint height, uint ver, int mips);

        [DllImport("textoolwrap")]
        public static extern bool PickUpAndFree(IntPtr outBuf, uint size, int id);

        [DllImport("textoolwrap")]
        public static extern uint EncodeByPVRTexLib(IntPtr data, IntPtr buf, int mode, int level, uint width, uint height);

        [DllImport("textoolwrap")]
        public static extern uint EncodeByISPC(IntPtr data, IntPtr buf, int mode, int level, uint width, uint height);
    }
}
