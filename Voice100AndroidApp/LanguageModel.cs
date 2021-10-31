using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Voice100AndroidApp
{
    public class LanguageModel : IDisposable
    {
        private static string[] ReadVocab(string path)
        {
            var vocab = new List<string>();
            using (var reader = File.OpenText(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string w = line.Trim();
                    vocab.Add(w);
                }
            }
            return vocab.ToArray();
        }

        private static string[] ReadVocabFromString(string text)
        {
            var vocab = new List<string>();
            foreach (string line in text.Split('\n'))
            {
                string w = line.Trim();
                vocab.Add(w);
            }
            return vocab.ToArray();
        }

        private readonly Random _random;
        private readonly string[] _i2w;
        private InferenceSession _session;
        private readonly double _temperature;
        private readonly DenseTensor<float> _c0;
        private readonly DenseTensor<float> _h0;
        private readonly DenseTensor<long> _input;

        private LanguageModel(double temperature)
        {
            _random = new Random();
            _temperature = temperature;
            _input = new DenseTensor<long>(new long[1] { 0 }, new int[] { 1, 1 });
            _c0 = new DenseTensor<float>(new float[1300], new int[] { 2, 1, 650 });
            _h0 = new DenseTensor<float>(new float[1300], new int[] { 2, 1, 650 });
        }

        public LanguageModel(string modelPath, string vocabPath, double temperature) : this(temperature)
        {
            _i2w = ReadVocab(vocabPath);
            _session = new InferenceSession(modelPath);
            long index = _random.Next(0, _i2w.Length);
            _input[0] = index;
        }

        public LanguageModel(byte[] model, string vocab, double temperature) : this(temperature)
        {
            _i2w = ReadVocabFromString(vocab);
            _session = new InferenceSession(model);
        }

        private int MultinomialWithTemperature(float[] output)
        {
            double[] weights = new double[output.Length];
            double weightsSum = 0.0;
            double a = 1 / _temperature;
            for (int i = 0; i < output.Length; i++)
            {
                double weight = Math.Exp(output[i] * a);
                weights[i] = weight;
                weightsSum += weight;
            }
            double t = _random.NextDouble() * weightsSum;
            double s = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                s += weights[i];
                if (t < s) return i;
            }
            return 0;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && _session != null)
            {
                _session.Dispose();
                _session = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public string Predict(int numWords)
        {
            var inputData = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input", _input),
                    NamedOnnxValue.CreateFromTensor("c0", _c0),
                    NamedOnnxValue.CreateFromTensor("h0", _h0),
                };
            var res = new StringBuilder();

            for (int i = 0; i < numWords; i++)
            {
                using (var outputData = _session.Run(inputData))
                {
                    var outputArray = outputData.ToArray();
                    var output = outputArray[0].AsTensor<float>();
                    int index = MultinomialWithTemperature(output.ToArray());
                    string token = _i2w[index];
                    if (token == "<eos>")
                    {
                        break;
                    }
                    res.Append(token);
                    _input[0] = index;
                    var c = outputArray[1].AsTensor<float>();
                    var h = outputArray[2].AsTensor<float>();
                    c.ToDenseTensor().Buffer.CopyTo(_c0.Buffer);
                    h.ToDenseTensor().Buffer.CopyTo(_h0.Buffer);
                }
            }

            return res.ToString().Replace('_', ' ');
        }
    }
}
