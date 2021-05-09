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

namespace VoiceAndroidApp
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        protected bool _isRecording;
        private Thread _recordingThread;
        private byte[] _audioBuffer;
        private AudioRecord _audRecorder;
        private MelSpectrogram _melSpectrogram;
        private SpectrogramView _spectrogramView;
        private GraphView _graphView;
        protected Handler _handler;
        private AppCompatButton _startRecordButton;
        private AppCompatButton _stopRecordButton;
        private AppCompatButton _startPlayButton;
        private AppCompatTextView _magnitudeText;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            Toolbar toolbar = FindViewById<Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
            fab.Click += FabOnClick;

            _melSpectrogram = new MelSpectrogram();
            _spectrogramView = FindViewById<SpectrogramView>(Resource.Id.spectrogram);
            _graphView = FindViewById<GraphView>(Resource.Id.graph);
            _magnitudeText = FindViewById<AppCompatTextView>(Resource.Id.magnitude);

            _startRecordButton = FindViewById<AppCompatButton>(Resource.Id.start_recording);
            _startRecordButton.Click += StartRecordingClick;
            _stopRecordButton = FindViewById<AppCompatButton>(Resource.Id.stop_recording);
            _stopRecordButton.Click += StopRecordingClick;
            _startPlayButton = FindViewById<AppCompatButton>(Resource.Id.start_playing);
            _startPlayButton.Click += StartPlayingClick;
            _startRecordButton.Enabled = true;
            _stopRecordButton.Enabled = false;
            _startPlayButton.Enabled = false;
        }

        private void StartRecordingClick(object sender, EventArgs e)
        {
            _startRecordButton.Enabled = false;
            _stopRecordButton.Enabled = true;
            _startPlayButton.Enabled = false;

            _audioBuffer = new byte[1024 * 2];
            _audRecorder = new AudioRecord(
              // Hardware source of recording.
              AudioSource.Mic,
              // Frequency
              16000,
              // Mono or stereo
              ChannelIn.Mono,
              // Audio encoding
              Android.Media.Encoding.Pcm16bit,
              // Length of the audio clip.
              _audioBuffer.Length
            );
            _audRecorder.StartRecording();

            _recordingThread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        // Keep reading the buffer while there is audio input.
                        int read = _audRecorder.Read(_audioBuffer, 0, _audioBuffer.Length);
                        // Write out the audio file.
                        if (read == 0)
                        {
                            break;
                        }
                        var int16waveform = new short[1024];
                        Buffer.BlockCopy(_audioBuffer, 0, int16waveform, 0, _audioBuffer.Length);
                        var waveform = new float[1024];
                        for (int i = 0; i < int16waveform.Length; i++) waveform[i] = int16waveform[i] / 32767.0f;
                        RunOnUiThread(() => {
                            float[] spec = new float[257];
                            _melSpectrogram.Spectrogram(waveform, 0, spec, 0);
                            _spectrogramView.AddFrame(spec);
                            double mag = 0.0f;
                            for (int i = 0; i < waveform.Length; i++)
                            {
                                mag += waveform[i] * waveform[i];
                            }
                            mag = 10 * Math.Log10(mag / waveform.Length);
                            _graphView.AddValue((float)mag);

                            _magnitudeText.Text = mag.ToString();
                        });
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

            _audRecorder.Stop();
            _isRecording = false;
            _recordingThread.Join();
        }

        private void StartPlayingClick(object sender, EventArgs e)
        {
            AudioTrack audioTrack = new AudioTrack(
              // Stream type
              Android.Media.Stream.Music,
              // Frequency
              11025,
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
