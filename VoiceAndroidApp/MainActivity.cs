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
        private const double MaxWaveformLength = 10.0f; // 10 sec
        private const int AudioBufferLength = 1024; // 64 msec

        protected bool _isRecording;
        private Thread _recordingThread;
        private AudioRecord _audioRecorder;
        private AudioFeatureExtractor _melSpectrogram;
        private SpectrogramView _spectrogramView;
        private GraphView _graphView;
        protected Handler _handler;
        private AppCompatButton _startRecordButton;
        private AppCompatButton _stopRecordButton;
        private AppCompatButton _startPlayButton;
        private AppCompatTextView _magnitudeText;
        private AppCompatTextView _recognitionText;
        private VoiceSession _voiceSession;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            Toolbar toolbar = FindViewById<Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
            fab.Click += FabOnClick;

            _melSpectrogram = new AudioFeatureExtractor();
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
            _startPlayButton.Enabled = false;

            string ortFile = "stt_en_conv_base_ctc-20210617.basic.ort";

            using (var input = Assets.Open(ortFile))
            {
                byte[] buffer = new byte[20000000];
                int len = input.Read(buffer);
                byte[] ortData = buffer.AsSpan(0, len).ToArray();
                _voiceSession = new VoiceSession(ortData);
                _voiceSession.OnDebugInfo += OnDebugInfo;
                _voiceSession.OnSpeechRecognition = OnSpeechRecognition;
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

                _recognitionText.Text = text;
            });
        }

        private void StartRecordingClick(object sender, EventArgs e)
        {
            _startRecordButton.Enabled = false;
            _stopRecordButton.Enabled = true;
            _startPlayButton.Enabled = false;

            _audioRecorder = new AudioRecord(
                AudioSource.Mic,
                SampleRate,
                ChannelIn.Mono,
                Android.Media.Encoding.Pcm16bit,
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

        private void StopRecordingClick(object sender, EventArgs e)
        {
            _startRecordButton.Enabled = true;
            _stopRecordButton.Enabled = false;
            _startPlayButton.Enabled = false;

            _audioRecorder.Stop();
            _isRecording = false;
            _recordingThread.Join();
        }

        private void StartPlayingClick(object sender, EventArgs e)
        {
            byte[] _audioBuffer = null;
            AudioTrack audioTrack = new AudioTrack(
              // Stream type
              Android.Media.Stream.Music,
              // Frequency
              16000,
              // Mono or stereo
              ChannelOut.Mono,
              // Audio encoding
              Android.Media.Encoding.Pcm16bit,
              // Length of the audio clip.
              _audioBuffer.Length,
              // Mode. Stream or static.
              AudioTrackMode.Stream);

            audioTrack.Play();
            audioTrack.Write(_audioBuffer, 0, _audioBuffer.Length);
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
