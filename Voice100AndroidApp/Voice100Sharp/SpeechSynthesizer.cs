using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Voice100Sharp
{
    public class SpeechSynthesizer : IDisposable
    {
        private readonly Encoder _encoder;
        private readonly Vocoder _vocoder;
        private InferenceSession _ttsAlignInferSess;
        private InferenceSession _ttsAudioInferSess;

        public SpeechSynthesizer(byte[] ttsAlignORTModel, byte[] ttsAudioORTModel)
        {
            _encoder = new Encoder();
            _vocoder = new Vocoder();
            _ttsAlignInferSess = new InferenceSession(ttsAlignORTModel);
            _ttsAudioInferSess = new InferenceSession(ttsAudioORTModel);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_ttsAlignInferSess != null)
                {
                    _ttsAlignInferSess.Dispose();
                    _ttsAlignInferSess = null;
                }
                if (_ttsAudioInferSess != null)
                {
                    _ttsAudioInferSess.Dispose();
                    _ttsAudioInferSess = null;
                }
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public byte[] Speak(string text)
        {
            long[] encoded = _encoder.Encode(text);
            if (encoded.Length == 0) return new byte[0];
            long[] aligned = Align(encoded);
            var y = Predict(aligned);
            return MemoryMarshal.Cast<short, byte>(y).ToArray();
        }

        private long[] Align(long[] encoded)
        {
            var container = new List<NamedOnnxValue>();
            var encodedData = new DenseTensor<long>(encoded, new int[2] { 1, encoded.Length });
            container.Add(NamedOnnxValue.CreateFromTensor("text", encodedData));
            using var res = _ttsAlignInferSess.Run(container, new string[] { "align" });
            var logAlign = res.First().AsTensor<float>();
            var align = new double[logAlign.Dimensions[1], 2];
            for (int i = 0; i < align.GetLength(0); i++)
            {
                align[i, 0] = Math.Exp(Math.Max(0, logAlign[0, i, 0])) - 1;
                align[i, 1] = Math.Exp(Math.Max(0, logAlign[0, i, 1])) - 1;
            }
            return MakeAlignText(encoded, align);
        }

        private long[] MakeAlignText(long[] encoded, double[,] align, int head = 5, int tail = 5)
        {
            double t = head + tail;
            for (int i = 0; i < align.GetLength(0); i++)
            {
                t += align[i, 0] + align[i, 1];
            }
            int alignedLength = (int)t;
            long[] aligned = new long[alignedLength];
            t = head;
            for (int i = 0; i < align.GetLength(0); i++)
            {
                t += align[i, 0];
                int s = (int)Math.Round(t);
                t += align[i, 1];
                int e = (int)Math.Round(t);
                if (s == e) s = Math.Max(0, s - 1);
                for (int j = s; j < e; j++)
                {
                    aligned[j] = encoded[i];
                }
            }
            return aligned;
        }

        private short[] Predict(long[] aligned)
        {
            var container = new List<NamedOnnxValue>();
            var alignedData = new DenseTensor<long>(aligned, new int[2] { 1, aligned.Length });
            container.Add(NamedOnnxValue.CreateFromTensor("aligntext", alignedData));
            using var output = _ttsAudioInferSess.Run(container, new string[] { "f0", "logspc", "codeap" });
            var outputArray = output.ToArray();
            float[] f0 = outputArray[0].AsTensor<float>().ToArray();
            float[] logspc = outputArray[1].AsTensor<float>().ToArray();
            float[] codeap = outputArray[2].AsTensor<float>().ToArray();
            return _vocoder.Decode(f0, logspc, codeap);
        }
    }
}