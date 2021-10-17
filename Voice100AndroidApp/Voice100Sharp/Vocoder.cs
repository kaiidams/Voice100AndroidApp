using System.Runtime.InteropServices;

namespace Voice100Sharp
{
    internal class Vocoder
    {
#if true
        const string DllName = "libvoice100_native.so";
#else
        const string DllName = "__Internal";
#endif
        public const int FFTSize = 512;
        public const double FramePeriod = 10.0;
        public const float LogOffset = 1e-15f;
        public int SampleRate = 16000;

        [DllImport(DllName)]
        private static extern int Voice100Sharp_VocoderDecode(
            float[] f0, float[] logspc, float[] codedap, int f0_length,
            int fft_size, double frame_period, int fs, float log_offset, short[] y, int y_length);

        public Vocoder()
        {
        }

        public short[] Decode(float[] f0, float[] logspc, float[] codeap)
        {
            int yLength = Voice100Sharp_VocoderDecode(f0, logspc, codeap, f0.Length,
                FFTSize, FramePeriod, SampleRate, LogOffset, null, 0);
            short[] y = new short[yLength];
            Voice100Sharp_VocoderDecode(f0, logspc, codeap, f0.Length,
                FFTSize, FramePeriod, SampleRate, LogOffset, y, yLength);
            return y;
        }
    }
}