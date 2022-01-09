using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Voice100AndroidApp
{
    internal struct ModelInfo
    {
        public readonly string URL { get; }
        public readonly string FileName { get; }

        public ModelInfo(string url, string fileName)
        {
            URL = url;
            FileName = fileName;
        }
    }
}