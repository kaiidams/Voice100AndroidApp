using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VoiceAndroidApp
{
    public class SpectrumView : View
    {
        protected Paint _paint;
        private float[] _spectrum;
        private float _maxDb;
        private float _minDb;

        public SpectrumView(Context context) : base(context)
        {
            Init();
        }

        public SpectrumView(Context context, IAttributeSet attrs) : base(context, attrs)
        {
            Init();
        }

        public SpectrumView(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
        {
            Init();
        }

        public int FFTSize {
            get { return _spectrum.Length; }
            set
            {
                _spectrum = new float[value];
            }
        }

        public float[] Spectrum { get { return _spectrum; } }

        private void Init()
        {
            _maxDb = 0.0f;
            _minDb = -80.0f;
            _paint = new Paint();
            _paint.Color = Color.Green;
            _spectrum = new float[257];
            var rng = new Random();
            for (int i = 0; i < _spectrum.Length; i++)
            {
                _spectrum[i] = (float)rng.NextDouble();
            }
        }

        protected override void OnDraw(Canvas canvas)
        {
            int width = canvas.Width;
            int height = canvas.Height;
            for (int i = 0; i < _spectrum.Length; i++)
            {
                canvas.DrawRect(
                    (float)(i * width) / _spectrum.Length,
                    -(_spectrum[i] * height) / 200.0f,
                    (float)((i + 1) * width) / _spectrum.Length,
                    height,
                    _paint);
            }
            canvas.DrawLine(0, 0, 200, 200, _paint);
            canvas.DrawLine(200, 0, 0, 200, _paint);
        }
    }
}