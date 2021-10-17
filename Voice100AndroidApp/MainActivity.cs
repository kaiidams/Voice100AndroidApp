using System;
using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Views;
using AndroidX.AppCompat.Widget;
using AndroidX.AppCompat.App;
using Google.Android.Material.FloatingActionButton;
using Google.Android.Material.Snackbar;
using System.IO;
using Android.Media;
using System.Threading;
using Voice100Sharp;
using Android.Graphics;

namespace VoiceAndroidApp
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        private const int SampleRate = 16000;
        private const int AudioBufferLength = 4096; // 256 msec
        private const string STTORTPath = "stt_en_conv_base_ctc-20210619.all.ort";
        private const string TTSAlignORTPath = "ttsalign_en_conv_base-20210808.all.ort";
        private const string TTSAudioORTPath = "ttsaudio_en_conv_base-20210811.all.ort";

        protected bool _isRecording;
        private Thread _recordingThread;
        private AudioRecord _audioRecorder;
        private SpectrogramView _spectrogramView;
        private GraphView _graphView;
        protected Handler _handler;
        private AppCompatButton _startRecordButton;
        private AppCompatButton _stopRecordButton;
        private AppCompatButton _startPlayButton;
        private AppCompatTextView _magnitudeText;
        private AppCompatTextView _recognitionText;
        private VoiceSession _voiceSession;
        private TTS _tts;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            Toolbar toolbar = FindViewById<Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
            fab.Click += FabOnClick;

            _spectrogramView = FindViewById<SpectrogramView>(Resource.Id.spectrogram);
            _graphView = FindViewById<GraphView>(Resource.Id.graph);
            _magnitudeText = FindViewById<AppCompatTextView>(Resource.Id.magnitude);
            _recognitionText = FindViewById<AppCompatTextView>(Resource.Id.recognition);

            _startRecordButton = FindViewById<AppCompatButton>(Resource.Id.start_recording);
            _startRecordButton.Click += StartRecordingClick;
            _stopRecordButton = FindViewById<AppCompatButton>(Resource.Id.stop_recording);
            _stopRecordButton.Click += StopRecordingClick;
            _startPlayButton = FindViewById<AppCompatButton>(Resource.Id.start_playing);
            _startPlayButton.Click += StartPlayingClick;
            _startRecordButton.Enabled = true;
            _stopRecordButton.Enabled = false;
            _startPlayButton.Enabled = true;

            byte[] ortData = ReadAssetInBytes(STTORTPath);
            _voiceSession = new VoiceSession(ortData);
            _voiceSession.OnDebugInfo += OnDebugInfo;
            _voiceSession.OnSpeechRecognition = OnSpeechRecognition;
            _tts = CreateTTS();
        }

        private TTS CreateTTS()
        {
            byte[] ttsAlignORTModel = ReadAssetInBytes(TTSAlignORTPath);
            byte[] ttsAudioORTModel = ReadAssetInBytes(TTSAudioORTPath);
            return new TTS(ttsAlignORTModel, ttsAudioORTModel);
        }

        private byte[] ReadAssetInBytes(string fileName)
        {
            using (var input = Assets.Open(fileName))
            {
                using (var memoryStream = new MemoryStream())
                {
                    input.CopyTo(memoryStream);
                    return memoryStream.ToArray();
                }
            }
        }

        protected override void OnPause()
        {
            base.OnPause();
            if (_isRecording)
            {
                StopRecording();
            }
        }

        private void OnDebugInfo(string text)
        {
            RunOnUiThread(() =>
            {
                _magnitudeText.Text = text;
            });
        }

        private void OnSpeechRecognition(short[] audio, float[] melspec, string text)
        {
            RunOnUiThread(() =>
            {
                _spectrogramView.AddSeparator(Color.Green);
                float[] s = new float[64];
                for (int i = 0; i < melspec.Length; i += 64)
                {
                    Array.Copy(melspec, i, s, 0, 64);
                    _spectrogramView.AddFrame(s);
                }

                for (int i = 0; i + 160 <= audio.Length; i += 160)
                {
                    double sum = 0.0;
                    for (int j = 0; j < 160; j++)
                    {
                        double v = audio[i + j] / 32768.0;
                        sum += v * v;
                    }
                    float db = (float)(10.0 * Math.Log10(sum / 160));
                    _graphView.AddValue(db);
                }

                _recognitionText.Text = text;
            });
        }

        private void StartRecordingClick(object sender, EventArgs e)
        {
            _startRecordButton.Enabled = false;
            _stopRecordButton.Enabled = true;
            _startPlayButton.Enabled = true;

            _audioRecorder = new AudioRecord(
                AudioSource.Mic,
                SampleRate,
                ChannelIn.Mono,
                Encoding.Pcm16bit,
                AudioBufferLength * sizeof(short)
            );
            _audioRecorder.StartRecording();

            _recordingThread = new Thread(() =>
            {
                var audioBuffer = new byte[AudioBufferLength * sizeof(short)];
                while (true)
                {
                    try
                    {
                        // Keep reading the buffer while audio input is available.
                        int read = _audioRecorder.Read(audioBuffer, 0, audioBuffer.Length);
                        // Write out the audio file.
                        if (read == 0)
                        {
                            break;
                        }
                        _voiceSession.AddAudioBytes(audioBuffer, read);
                    }
                    catch (Exception ex)
                    {
                        Console.Out.WriteLine(ex.Message);
                        break;
                    }
                }
            });
            _isRecording = true;
            _recordingThread.Start();
        }

        private void StopRecording()
        {
            _startRecordButton.Enabled = true;
            _stopRecordButton.Enabled = false;
            _startPlayButton.Enabled = true;

            _audioRecorder.Stop();
            _isRecording = false;
            _recordingThread.Join();
            _audioRecorder = null;
            _recordingThread = null;
        }

        private void StopRecordingClick(object sender, EventArgs e)
        {
            StopRecording();
        }

        private void StartPlayingClick(object sender, EventArgs e)
        {
            int OutputBufferSizeInBytes = 10 * 1024;

            var audioTrack = new AudioTrack.Builder()
                     .SetAudioAttributes(new AudioAttributes.Builder()
                              .SetUsage(AudioUsageKind.Assistant)
                              .SetContentType(AudioContentType.Speech)
                              .Build())
                     .SetAudioFormat(new AudioFormat.Builder()
                             .SetEncoding(Encoding.Pcm16bit)
                             .SetSampleRate(16000)
                             .SetChannelMask(ChannelOut.Mono)
                             .Build())
                     .SetBufferSizeInBytes(OutputBufferSizeInBytes)
                     .Build();
            audioTrack.Play();

            string text = "Beginnings are apt to be determinative and when reinforced by continuous applications of similar influence.";
            var y = _tts.Speak(text);
            int len = audioTrack.Write(y, 0, y.Length);
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        private void FabOnClick(object sender, EventArgs eventArgs)
        {
            View view = (View) sender;
            Snackbar.Make(view, "Replace with your own action", Snackbar.LengthLong)
                .SetAction("Action", (View.IOnClickListener)null).Show();
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
	}
}
