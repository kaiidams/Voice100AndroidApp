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
        private string _filePath;
        private MediaPlayer _mediaPlayer;
        private MediaRecorder _mediaRecorder;
        protected bool _isRecording;
        private Thread _recordingThread;
        private byte[] _audioBuffer;
        private AudioRecord _audRecorder;
        private MelSpectrogram _melSpectrogram;
        private SpectrumView _spectrumView;
        private SpectrogramView _spectrogramView;
        protected Handler _handler;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            Toolbar toolbar = FindViewById<Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
            fab.Click += FabOnClick;

            MediaInit();

            AppCompatButton button = FindViewById<AppCompatButton>(Resource.Id.start_recording);
            button.Click += StartRecordingClick;
            button = FindViewById<AppCompatButton>(Resource.Id.stop_recording);
            button.Click += StopRecordingClick;
            button = FindViewById<AppCompatButton>(Resource.Id.start_playing);
            button.Click += StartPlayingClick;

            button = FindViewById<AppCompatButton>(Resource.Id.start_recording_low);
            button.Click += StartRecordingLowClick;
            button = FindViewById<AppCompatButton>(Resource.Id.stop_recording_low);
            button.Click += StopRecordingLowClick;
            button = FindViewById<AppCompatButton>(Resource.Id.start_playing_low);
            button.Click += StartPlayingLowClick;

            _spectrogramView = FindViewById<SpectrogramView>(Resource.Id.spectrogram);
            _spectrumView = FindViewById<SpectrumView>(Resource.Id.spectrum);
        }

        private void MediaInit()
        {
            _filePath = Path.Combine(CacheDir.AbsolutePath, "test.wav");
            using (var outputStream = File.OpenWrite(_filePath))
            using (var stream = Assets.Open("test.wav"))
            {
                stream.CopyTo(outputStream);
            }
            _mediaRecorder = new MediaRecorder();
            _mediaPlayer = new MediaPlayer();
            _melSpectrogram = new MelSpectrogram();
        }

        private void StartRecordingClick(object sender, EventArgs e)
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
            _mediaRecorder.Reset();
            _mediaRecorder.SetAudioSource(AudioSource.Mic);
            _mediaRecorder.SetOutputFormat(OutputFormat.ThreeGpp);
            _mediaRecorder.SetAudioEncoder(AudioEncoder.AmrNb);
            // Initialized state.
            _mediaRecorder.SetOutputFile(_filePath);
            // DataSourceConfigured state.
            _mediaRecorder.Prepare(); // Prepared state
            _mediaRecorder.Start(); // Recording state.
        }

        private void StopRecordingClick(object sender, EventArgs e)
        {
            _mediaRecorder.Stop();
        }

        private void StartPlayingClick(object sender, EventArgs e)
        {
            _mediaPlayer.Reset();
            _mediaPlayer.SetDataSource(_filePath);
            _mediaPlayer.Prepare();
            _mediaPlayer.Start();
        }

        private void StartRecordingLowClick(object sender, EventArgs e)
        {
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
                            float maxValue = -1000.0f;
                            for (int i = 0; i < spec.Length; i++)
                            {
                                if (maxValue < spec[i]) maxValue = spec[i];
                            }
                            for (int i = 0; i < spec.Length; i++)
                            {
                                spec[i] -= maxValue;
                            }
                            Array.Copy(spec, _spectrumView.Spectrum, spec.Length);
                            _spectrumView.Invalidate();
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

        private void StopRecordingLowClick(object sender, EventArgs e)
        {
            _audRecorder.Stop();
            _isRecording = false;
            _recordingThread.Join();
        }

        private void StartPlayingLowClick(object sender, EventArgs e)
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
