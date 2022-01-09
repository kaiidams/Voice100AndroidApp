using System;
using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Views;
using AndroidX.AppCompat.Widget;
using AndroidX.AppCompat.App;
using Google.Android.Material.FloatingActionButton;
using System.IO;
using Android.Media;
using System.Threading;
using Voice100Sharp;
using Android.Graphics;
using Xamarin.Essentials;
using System.Threading.Tasks;
using Android;
using Android.Content.PM;
using System.Net.Http;
using System.Collections.Generic;

namespace Voice100AndroidApp
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        private const int SampleRate = 16000;
        private const int AudioBufferLength = 4096; // 256 msec
        private const int RecordAudioPermission = 1;

        private const int STT = 0;
        private const int TTSAlign = 1;
        private const int TTSAudio = 2;
        private const int NumModels = 3;

        private static IDictionary<string, ModelInfo[]> ModelInfoDict = new Dictionary<string, ModelInfo[]> {
            {
                "en",
                new ModelInfo[]
                {
                    new ModelInfo(
                        "https://github.com/kaiidams/Voice100AndroidApp/releases/download/v0.5/stt_en_conv_base_ctc-20211125.all.ort",
                        "stt_en_conv_base_ctc-20211125.all.ort"),
                    new ModelInfo(
                        "https://github.com/kaiidams/Voice100AndroidApp/releases/download/v0.5/ttsalign_en_conv_base-20210808.all.ort",
                        "ttsalign_en_conv_base-20210808.all.ort"),
                    new ModelInfo(
                        "https://github.com/kaiidams/Voice100AndroidApp/releases/download/v0.5/ttsaudio_en_conv_base-20210811.all.ort",
                        "ttsaudio_en_conv_base-20210811.all.ort")
                }
            },
            {
                "ja",
                new ModelInfo[]
                {
                    new ModelInfo(
                        "https://github.com/kaiidams/Voice100AndroidApp/releases/download/v0.5/stt_ja_conv_base_ctc-20211127.all.ort",
                        "stt_ja_conv_base_ctc-20211127.all.ort"),
                    new ModelInfo(
                        "https://github.com/kaiidams/Voice100AndroidApp/releases/download/v0.5/ttsalign_ja_conv_base-20211118.all.ort",
                        "ttsalign_ja_conv_base-20211118.all.ort"),
                    new ModelInfo(
                        "https://github.com/kaiidams/Voice100AndroidApp/releases/download/v0.5/ttsaudio_ja_conv_base-20211118.all.ort",
                        "ttsaudio_ja_conv_base-20211118.all.ort")
                }
            }
        };

        protected bool _isRecording;
        private Thread _recordingThread;
        protected bool _isPlaying;
        private Thread _playingThread;
        private AudioRecord _audioRecorder;
        private SpectrogramView _spectrogramView;
        private GraphView _graphView;
        protected Handler _handler;
        private AppCompatTextView _statusText;
        private AppCompatButton _startRecordButton;
        private AppCompatButton _stopRecordButton;
        private AppCompatButton _startPlayButton;
        private AppCompatButton _stopPlayButton;
        private AppCompatTextView _magnitudeText;
        private AppCompatTextView _recognitionText;
        private AppCompatEditText _inputTextEditText;
        private SpeechRecognizer _speechRecognizer;
        private SpeechSynthesizer _speechSynthesizer;
        private Task _downloadingTask;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            _statusText = FindViewById<AppCompatTextView>(Resource.Id.status);

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
            _stopPlayButton = FindViewById<AppCompatButton>(Resource.Id.stop_playing);
            _stopPlayButton.Click += StopPlayingClick;

            _inputTextEditText = FindViewById<AppCompatEditText>(Resource.Id.input_text);

            _speechRecognizer = null;
            _speechSynthesizer = null;
        }

        private string GetModelFilePath(ModelInfo modelInfo)
        {
            return System.IO.Path.Combine(CacheDir.Path, modelInfo.FileName);
        }

        private string GetModelFilePath(int modelType)
        {
            var modelInfo = GetModelInfo(modelType);
            return System.IO.Path.Combine(CacheDir.Path, modelInfo.FileName);
        }

        private ModelInfo GetModelInfo(int modelType)
        {
            string lang = GetString(Resource.String.model_language);
            return ModelInfoDict[lang][modelType];
        }

        private SpeechRecognizer CreateSTT()
        {
            string filePath = GetModelFilePath(STT);
            return new SpeechRecognizer(filePath);
        }

        private SpeechSynthesizer CreateTTS()
        {
            string alignFilePath = GetModelFilePath(TTSAlign);
            string audioFilePath = GetModelFilePath(TTSAudio);
            return new SpeechSynthesizer(alignFilePath, audioFilePath);
        }

        protected override void OnResume()
        {
            base.OnResume();
            if (_downloadingTask == null)
            {
                _downloadingTask = Task.Run(DownloadAllModels);
            }
            UpdateButtons();
        }

        private async Task DownloadAllModels()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    string lang = GetString(Resource.String.model_language);
                    var modelInfoList = ModelInfoDict[lang];
                    for (int i = 0; i < NumModels; i++)
                    {
                        _statusText.Text = string.Format(
                            GetString(Resource.String.downloading), i + 1, modelInfoList.Length);
                        await DownloadOneModel(client, modelInfoList[i]);
                    }
                }
                OnModelDownloaded();
            }
            catch (Exception ex)
            {
                _statusText.Text = "Error";
            }
            finally
            {
                _downloadingTask = null;
            }
        }

        private void OnModelDownloaded()
        {
            _speechRecognizer = CreateSTT();
            _speechRecognizer.OnDebugInfo += OnDebugInfo;
            _speechRecognizer.OnSpeechRecognition = OnSpeechRecognition;
            _speechSynthesizer = CreateTTS();
            _statusText.Text = "";
            UpdateButtons();
        }

        private async Task DownloadOneModel(HttpClient client, ModelInfo modelInfo)
        {
            string filePath = GetModelFilePath(modelInfo);
            if (!File.Exists(filePath) || new FileInfo(filePath).Length < 1 * 1024 * 1024)
            {
                try
                {
                    using (var writer = File.OpenWrite(filePath))
                    {
                        using (var response = await client.GetAsync(modelInfo.URL))
                        {
                            await response.Content.CopyToAsync(writer);
                        }
                    }
                    if (new FileInfo(filePath).Length < 1 * 1024 * 1024)
                    {
                        throw new IOException("File is too small.");
                    }
                }
                catch (IOException)
                {
                    File.Delete(filePath);
                    throw;
                }
            }
        }

        protected override void OnPause()
        {
            base.OnPause();
            StopRecording();
            StopPlaying();
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            _startRecordButton.Enabled = _speechRecognizer != null && !_isRecording;
            _stopRecordButton.Enabled = _speechRecognizer != null && _isRecording;
            _startPlayButton.Enabled = _speechSynthesizer != null && !_isPlaying;
            _stopPlayButton.Enabled = _speechSynthesizer != null && _isPlaying;
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
            if (CheckSelfPermission(Manifest.Permission.RecordAudio) != Permission.Granted)
            {
                RequestPermissions(
                   new[] { Manifest.Permission.RecordAudio },
                   RecordAudioPermission);
            }
            else
            {
                StartRecording();
                UpdateButtons();
            }
        }

        public override void OnRequestPermissionsResult(
            int requestCode,
            string[] permissions,
            Permission[] grantResults)
        {
            switch (requestCode)
            {
                case RecordAudioPermission:
                    if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
                    {
                        StartRecording();
                        UpdateButtons();
                    }
                    else
                    {
                        Android.Widget.Toast.MakeText(
                            this,
                            Resource.String.audio_recording_permission_denied,
                            Android.Widget.ToastLength.Long).Show();
                    }
                    break;
            }
        }

        private void StartRecording()
        {
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
                        _speechRecognizer.AddAudioBytes(audioBuffer, read);
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
            StopRecording();
            UpdateButtons();
        }

        private void StopRecording()
        {
            if (_isRecording)
            {
                _audioRecorder.Stop();
                _isRecording = false;
                _recordingThread.Join();
                _audioRecorder = null;
                _recordingThread = null;
            }
        }

        private void StartPlayingClick(object sender, EventArgs e)
        {
            int OutputBufferSizeInBytes = 10 * 1024;

            string text = _inputTextEditText.Text;

            var audioTrack = new AudioTrack.Builder()
                     .SetAudioAttributes(new AudioAttributes.Builder()
                              .SetUsage(AudioUsageKind.Assistant)
                              .SetContentType(AudioContentType.Speech)
                              .Build())
                     .SetAudioFormat(new AudioFormat.Builder()
                             .SetEncoding(Encoding.Pcm16bit)
                             .SetSampleRate(SampleRate)
                             .SetChannelMask(ChannelOut.Mono)
                             .Build())
                     .SetBufferSizeInBytes(OutputBufferSizeInBytes)
                     .Build();
            audioTrack.Play();

            _playingThread = new Thread(() =>
            {
                var y = _speechSynthesizer.Speak(text);
                for (int i = 0; i < y.Length && _isPlaying;)
                {
                    int bytesToWrite = Math.Min(y.Length - i, 4096);
                    int bytesWritten = audioTrack.Write(y, i, bytesToWrite);
                    if (bytesWritten < 0) break;
                    i += bytesWritten;
                }

                RunOnUiThread(() =>
                {
                    StopPlaying();
                    UpdateButtons();
                });
            });

            _isPlaying = true;
            _playingThread.Start();
            UpdateButtons();
        }

        private void StopPlaying()
        {
            if (_isPlaying)
            {
                _isPlaying = false;
                _playingThread.Join();
                _playingThread = null;
            }
        }

        private void StopPlayingClick(object sender, EventArgs e)
        {
            StopPlaying();
            UpdateButtons();
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
                Android.Widget.Toast.MakeText(
                    this,
                    Resource.String.not_implemented,
                    Android.Widget.ToastLength.Long).Show();
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        private async void FabOnClick(object sender, EventArgs eventArgs)
        {
            try
            {
                var uri = new Uri("https://github.com/kaiidams/Voice100AndroidApp.git");
                await Browser.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
            }
            catch (Exception)
            {
            }
        }
	}
}
