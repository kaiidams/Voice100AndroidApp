using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Voice100Sharp
{
    public class SpeechRecognizer : IDisposable
    {
        public delegate void DebugInfoEvent(string text);
        public delegate void SpeechRecognitionEvent(short[] audio, float[] melspec, string text);

        const int SampleRate = 16000;
        const int AudioBytesBufferLength = 10 * SampleRate * sizeof(short);
        const int VadWindowLength = 160;
        const double VoicedDecibelMinThreshold = -60.0;
        const double UnvoicedDecibelMaxThreshold = -30.0;
        const double ActivateThreshold = 0.7;
        const double DeactivateThreshold = 0.2;
        const int MinRepeatVoicedCount = 10;

        private readonly Encoder _encoder;
        private InferenceSession _inferSess;
        private readonly AudioFeatureExtractor _featureExtractor;

        private byte[] _audioBytesBuffer;
        private int _audioBytesBufferWriteOffset;

        private int _audioBufferVadOffset;
        private double _audioLevelExpMovingAverage;

        private bool _isVoiced;
        private double _unvoicedAverageDecibel;
        private double _voicedAverageDecibel;
        private int _zeroCrossing;

        private int _voicedRepeatCount;

        private bool _isActive;
        private int _audioBufferActiveOffset;

        private SpeechRecognizer()
        {
            _encoder = new Encoder();
            _featureExtractor = new AudioFeatureExtractor();
            _audioBytesBuffer = new byte[AudioBytesBufferLength];
            _audioBytesBufferWriteOffset = 0;
            _audioBufferVadOffset = 0;
            _audioLevelExpMovingAverage = 0.0;
            _unvoicedAverageDecibel = UnvoicedDecibelMaxThreshold;
            _voicedAverageDecibel = UnvoicedDecibelMaxThreshold;
            _voicedRepeatCount = 0;
            _audioBufferActiveOffset = 0;
        }

        public SpeechRecognizer(string onnxPath) : this()
        {
            _inferSess = new InferenceSession(onnxPath);
        }

        public SpeechRecognizer(byte[] onnxData) : this()
        {
#if false
            var so = new SessionOptions();
            uint NNAPI_FLAG_USE_FP16 = 0x001;
            uint NNAPI_FLAG_USE_NCHW = 0x002;
            uint NNAPI_FLAG_CPU_DISABLED = 0x004;
            so.AppendExecutionProvider_Nnapi(NNAPI_FLAG_USE_NCHW | NNAPI_FLAG_USE_FP16 | NNAPI_FLAG_CPU_DISABLED);
            _inferSess = new InferenceSession(onnxData, so);
#else
            _inferSess = new InferenceSession(onnxData);
#endif
        }

        public bool IsVoiced { get { return _isVoiced; } }
        public double AudioDecibel { get { return 10 * Math.Log10(_audioLevelExpMovingAverage + 1e-15); } }
        public bool IsActive { get { return _isActive; } }
        public DebugInfoEvent OnDebugInfo { get; set; }
        public SpeechRecognitionEvent OnSpeechRecognition { get; set; }

        public void AddAudioBytes(byte[] audioBytes, int audioBytesLength)
        {
            for (int sourceIndex = 0; sourceIndex < audioBytesLength;)
            {
                int copyLength = Math.Min(_audioBytesBuffer.Length - _audioBytesBufferWriteOffset, audioBytesLength - sourceIndex);
                Array.Copy(audioBytes, sourceIndex, _audioBytesBuffer, _audioBytesBufferWriteOffset, copyLength);
                sourceIndex += copyLength;
                _audioBytesBufferWriteOffset += copyLength;
                if (_audioBytesBufferWriteOffset >= _audioBytesBuffer.Length)
                {
                    _audioBytesBufferWriteOffset = 0;
                }
            }

            var audioBuffer = MemoryMarshal.Cast<byte, short>(_audioBytesBuffer);
            int audioBufferWriteOffset = _audioBytesBufferWriteOffset / sizeof(short);

            UpdateAudioLevel(audioBuffer, audioBufferWriteOffset);
        }

        private void UpdateAudioLevel(Span<short> audioBuffer, int audioBufferWriteOffset)
        {
            while (_audioBufferVadOffset / VadWindowLength != audioBufferWriteOffset / VadWindowLength)
            {
                double frameAudioLevel = FrameAudioLevel(audioBuffer, _audioBufferVadOffset, VadWindowLength);
                _zeroCrossing = FrameZeroCrossing(audioBuffer, _audioBufferVadOffset, VadWindowLength);
                UpdateVoiced(audioBuffer, frameAudioLevel);
                UpdateActive(audioBuffer);
            }
            DebugInfo();
        }

        private void DebugInfo()
        {
            string text = string.Format(
                "IsVoiced:{0} IsActive:{1} AudioDecibel:{2:##.#} {3:##.#} {4:##.#} {5} {6}",
                IsVoiced ? "X" : ".",
                IsActive ? "X" : ".",
                AudioDecibel,
                _voicedAverageDecibel,
                _unvoicedAverageDecibel,
                _zeroCrossing,
                _voicedRepeatCount);
            OnDebugInfo(text);
        }

        private void UpdateVoiced(Span<short> audioBuffer, double frameAudioLevel)
        {
            _audioLevelExpMovingAverage = _audioLevelExpMovingAverage * 0.9 + frameAudioLevel * 0.1;
            _audioBufferVadOffset += VadWindowLength;
            if (_audioBufferVadOffset >= audioBuffer.Length)
            {
                _audioBufferVadOffset = 0;
            }

            double audioDecibel = AudioDecibel;

            // Audio is voiced when the decibel is more than the average of unvoiced average
            // decibel and voiced average decibel.
            _isVoiced = 2 * audioDecibel > _unvoicedAverageDecibel + _voicedAverageDecibel;
            if (_isVoiced)
            {
                _voicedAverageDecibel = Math.Max(
                    _voicedAverageDecibel * 0.9 + audioDecibel * 0.1,
                    VoicedDecibelMinThreshold);
            }
            else
            {
                _unvoicedAverageDecibel = Math.Min(
                    _unvoicedAverageDecibel * 0.9 + audioDecibel * 0.1,
                    UnvoicedDecibelMaxThreshold);
            }
        }

        private void UpdateActive(Span<short> audioBuffer)
        {
            if (_isActive)
            {
                _voicedRepeatCount = IsVoiced ? 0 : (_voicedRepeatCount + 1);
                if (_voicedRepeatCount >= MinRepeatVoicedCount * 3)
                {
                    Console.WriteLine("Deactive");
                    _voicedRepeatCount = 0;
                    _isActive = false;
                    InvokeDeactivate(audioBuffer);
                }
            }
            else
            {
                _voicedRepeatCount = IsVoiced ? (_voicedRepeatCount + 1) : 0;
                if (_voicedRepeatCount >= MinRepeatVoicedCount)
                {
                    Console.WriteLine("Active");
                    _voicedRepeatCount = 0;
                    _isActive = true;
                    _audioBufferActiveOffset = _audioBufferVadOffset - 3 * MinRepeatVoicedCount * VadWindowLength;
                    while (_audioBufferActiveOffset < 0)
                    {
                        _audioBufferActiveOffset += audioBuffer.Length;
                    }
                }
            }
        }

        private void InvokeDeactivate(Span<short> audioBuffer)
        {
            int _audioBufferDeactiveOffset = _audioBufferVadOffset;
            int audioLength = _audioBufferDeactiveOffset - _audioBufferActiveOffset;
            while (audioLength < 0)
            {
                audioLength += audioBuffer.Length;
            }

            // Make a short buffer
            short[] audio = new short[audioLength];
            int audioIndex = _audioBufferActiveOffset;
            for (int i = 0; i < audioLength; i++)
            {
                audio[i] = audioBuffer[audioIndex++];
                if (audioIndex >= audioBuffer.Length) audioIndex = 0;
            }

            audioIndex = _audioBufferActiveOffset;
            short audioMaxShortValue = 0;
            for (int i = 0; i < audioLength; i++)
            {
                short value = Math.Abs(audioBuffer[audioIndex++]);
                if (audioMaxShortValue < value) audioMaxShortValue = value;
                if (audioIndex >= audioBuffer.Length) audioIndex = 0;
            }
            double audioScale = 0.8 / audioMaxShortValue;

            float[] audioFloat = new float[audio.Length];
            int max = 0;
            for (int i = 0; i < audioFloat.Length; i++)
            {
                audioFloat[i] = audio[i];
                max = Math.Max(max, Math.Abs(audio[i]));
            }
            for (int i = 0; i < audioFloat.Length; i++)
            {
                audioFloat[i] = (float)(0.8 * audioFloat[i] / max);
            }

            float[] melspec = new float[64 * ((audioLength - 400) / 160 + 1)];
            int melspecOffset = 0;
#if true
            for (int i = 0; i + 400 <= audioFloat.Length; i += 160)
            {
                _featureExtractor.MelSpectrogram(audioFloat, i, melspec, melspecOffset);
                melspecOffset += 64;
            }
#else
            while ((_audioBufferActiveOffset + 400) % audioBuffer.Length <= _audioBufferDeactiveOffset)
            {
                _featureExtractor.MelSpectrogram(audioBuffer, _audioBufferActiveOffset, audioScale, melspec, melspecOffset);
                melspecOffset += 64;
                _audioBufferActiveOffset += 160;
                while (_audioBufferActiveOffset >= audioBuffer.Length) _audioBufferActiveOffset -= audioBuffer.Length;
            }
#endif

            AnalyzeAudio(audio, melspec);
        }

        private void AnalyzeAudio(short[] audio, float[] melspec)
        {
            var container = new List<NamedOnnxValue>();
            int[] melspecLength = new int[1] { melspec.Length };
            var audioData = new DenseTensor<float>(melspec, new int[3] { 1, melspec.Length / 64, 64 });
            container.Add(NamedOnnxValue.CreateFromTensor("audio", audioData));
            using var res = _inferSess.Run(container, new string[] { "logits" });
            foreach (var score in res)
            {
                var s = score.AsTensor<float>();
                long[] pred = new long[s.Dimensions[1]];
                for (int l = 0; l < pred.Length; l++)
                {
                    int k = -1;
                    float m = -10000.0f;
                    for (int j = 0; j < s.Dimensions[2]; j++)
                    {
                        if (m < s[0, l, j])
                        {
                            k = j;
                            m = s[0, l, j];
                        }
                    }
                    pred[l] = k;
                }

                string text = _encoder.Decode(pred);
                text = _encoder.MergeRepeated(text);

                OnSpeechRecognition(audio, melspec, text);
            }
        }

        private static double FrameAudioLevel(Span<short> audio, int offset, int length)
        {
            long mag = 0;
            for (int i = offset; i < offset + length; i++)
            {
                long v = audio[i];
                mag += v * v;
            }
            return mag / (length * 32768.0 * 32768.0);
        }


        private static int FrameZeroCrossing(Span<short> audio, int offset, int length)
        {
            double average = 0.0;
            for (int i = offset; i < offset + length; i++)
            {
                average += audio[i];
            }
            average /= length;

            int zeroCrossCount = 0;
            for (int i = offset; i < offset + length - 1; i++)
            {
                if (audio[i] >= average && audio[i + 1] < average || audio[i + 1] >= average && audio[i] < average)
                {
                    zeroCrossCount++;
                }
            }

            return zeroCrossCount;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && _inferSess != null)
            {
                _inferSess.Dispose();
                _inferSess = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
