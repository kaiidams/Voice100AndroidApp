using System;
using System.Runtime.InteropServices;

namespace Voice100Sharp
{
    internal class WebRtcVad : IDisposable
    {
#if true
        const string DllName = "libwebrtc_vad.so";
#else
        const string DllName = "__Internal";
#endif
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr WebRtcVad_Create();
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        static extern void WebRtcVad_Free(IntPtr handle);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        static extern int WebRtcVad_Init(IntPtr handle);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        static extern int WebRtcVad_set_mode(IntPtr handle, int mode);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        static extern int WebRtcVad_Process(IntPtr handle,
                              int fs,
                              short[] audio_frame,
                              IntPtr frame_length);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        static extern int WebRtcVad_ValidRateAndFrameLength(int rate, IntPtr frame_length);

        private IntPtr _handle;

        public WebRtcVad()
        {
            _handle = WebRtcVad_Create();
            int ret = WebRtcVad_Init(_handle);
            if (ret != 0) throw new Exception();
        }

        public void SetMode(int mode)
        {
            int ret = WebRtcVad_set_mode(_handle, mode);
            if (ret != 0) throw new Exception();
        }

        public bool Process(int fs, short[] audioFrame, int frameLength)
        {
            int ret = WebRtcVad_Process(_handle, fs, audioFrame, (IntPtr)frameLength);
            Console.WriteLine(ret);
            if (ret == 1) return true;
            if (ret == 0) return false;
            throw new Exception();
        }

        public bool ValidRateAndFrameLength(int rate, int frameLength)
        {
            int ret = WebRtcVad_ValidRateAndFrameLength(rate, (IntPtr)frameLength);
            return ret == 0;
        }

        public void Dispose()
        {            
            if (_handle != IntPtr.Zero)
            {
                WebRtcVad_Free(_handle);
                _handle = IntPtr.Zero;
            }
        }
    }
}