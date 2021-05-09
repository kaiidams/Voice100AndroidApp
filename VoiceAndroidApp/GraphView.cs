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
    class GraphView : View
    {
        protected Paint _paint;
        private float[] _history;
        private float _maxValue;
        private float _minValue;

        public GraphView(Context context) : base(context)
        {
            Init();
        }

        public GraphView(Context context, IAttributeSet attrs) : base(context, attrs)
        {
            Init();
        }

        public GraphView(Context context, IAttributeSet attrs, int defStyle) : base(context, attrs, defStyle)
        {
            Init();
        }

        private void Init()
        {
            _maxValue = 0.0f;
            _minValue = -80.0f;
            _history = new float[256];
            _paint = new Paint();
            _paint.SetStyle(Paint.Style.Stroke);
            _paint.Color = Color.Black;
            for (int i = 0; i < _history.Length; i++)
            {
                _history[i] = 0.0f;
            }
        }

        public void AddValue(float value)
        {
            Array.Copy(_history, 1, _history, 0, _history.Length - 1);
            _history[_history.Length - 1] = value;
            Invalidate();
        }

        protected override void OnDraw(Canvas canvas)
        {
            int width = canvas.Width;
            int height = canvas.Height;
            var path = new Path();
            for (int i = 0; i < _history.Length; i++)
            {
                float v = _history[i];
                float x = width * (i + 0.5f) / _history.Length;
                float y = height * (1 - (v - _minValue) / (_maxValue - _minValue));
                if (i == 0)
                {
                    path.MoveTo(x, y);
                }
                else
                {
                    path.LineTo(x, y);
                }
            }
            canvas.DrawPath(path, _paint);
        }
    }
}