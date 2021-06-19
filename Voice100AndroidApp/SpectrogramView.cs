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
    public class SpectrogramView : View
    {
        private Bitmap _bitmap;
        private int[] _colors;
        private int _timeFrameLength;
        private int _spectrumLength;
        private float _maxValue;
        private float _minValue;

        public SpectrogramView(Context context) : base(context)
        {
            Init();
        }

        public SpectrogramView(Context context, IAttributeSet attrs) : base(context, attrs)
        {
            Init();
        }

        public SpectrogramView(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
        {
            Init();
        }

        private void Init()
        {
            _maxValue = 5.0f;
            _minValue = (float)(Math.Log(1e-4));
            UpdateBitmap(256, 64);
        }

        private void UpdateBitmap(int timeFrameLength, int spectrumLength)
        {
            _timeFrameLength = timeFrameLength;
            _spectrumLength = spectrumLength;
            _bitmap = Bitmap.CreateBitmap(_timeFrameLength, _spectrumLength, Bitmap.Config.Argb8888);
            _colors = new int[_timeFrameLength * _spectrumLength];
            Array.Fill(_colors, MagmaColorMap.Rgb(0.0f));
        }

        public void AddFrame(float[] frame)
        {
            Array.Copy(_colors, 1, _colors, 0, _colors.Length - 1);
            float maxData = -200.0f;
            for (int i = 0; i < _spectrumLength; i++)
            {
                float v = (frame[i] - _minValue) / (_maxValue - _minValue);
                if (frame[i] > maxData) maxData = frame[i];
                _colors[_timeFrameLength * (_spectrumLength - i) - 1] = MagmaColorMap.Rgb(v);
            }
            Invalidate();
        }

        public void AddSeparator(int color)
        {
            Array.Copy(_colors, 1, _colors, 0, _colors.Length - 1);
            for (int i = 0; i < _spectrumLength; i++)
            {
                _colors[_timeFrameLength * (_spectrumLength - i) - 1] = color;
            }
            Invalidate();
        }

        protected override void OnDraw(Canvas canvas)
        {
            int width = canvas.Width;
            int height = canvas.Height;
#if false
            var colors = new int[128 * 128];
            for (int i = 0; i < _bitmap.Width; i++)
            {
                for (int j = 0; j < _bitmap.Height; j++)
                {
                    colors[i * 128 + j] = Color.Rgb(i * 2, j * 2, 0);
                }
            }
            _bitmap.SetPixels(colors, 0, 128, 0, 0, 128, 128);
#endif
            _bitmap.SetPixels(_colors, 0, _timeFrameLength, 0, 0, _timeFrameLength, _spectrumLength);
            var src = new Rect(0, 0, _timeFrameLength, _spectrumLength);
            var dst = new Rect(0, 0, width, height);
            canvas.DrawBitmap(_bitmap, src, dst, null);
        }
    }
}