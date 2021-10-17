using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Voice100Sharp
{
    public class TTS
    {
        private readonly Vocoder _vocoder;

        public TTS()
        {
            _vocoder = new Vocoder();
        }

        public byte[] Speak(byte[] _data)
        {
            int f0Length = 157213 / (1 + 257 + 1);
            var data = MemoryMarshal.Cast<byte, float>(_data);
            var f0 = data.Slice(0, f0Length);
            var logspc = data.Slice(f0Length, 257 * f0Length);
            var codeap = data.Slice((1 + 257) * f0Length, f0Length);
            var y = _vocoder.Decode(f0.ToArray(), logspc.ToArray(), codeap.ToArray());
            return MemoryMarshal.Cast<short, byte>(y).ToArray();
        }
    }
}