# Voice100 Xamarin Android App

Voice100 Android App is a TTS/ASR sample app that uses 
[ONNX Runtime](https://github.com/microsoft/onnxruntime/),
[WORLD](https://github.com/mmorise/World)
and [Voice100](https://github.com/kaiidams/voice100) neural TTS/ASR models
on [Xamarin](https://dotnet.microsoft.com/apps/xamarin).
Inference of Voice100 is low cost as its models are tiny and only depend
on CNN without recursion.

[Download APK](https://github.com/kaiidams/Voice100AndroidApp/releases/download/v0.5/voice100androidapp-signed.apk)
API level 27 (Oreo) is required.

[Download sample audio](sample.wav)

![Voice100 Android App](voice100androidapp.png)

## Building

### Pre-requisites

- Visual Studio 2019
- Xamarin.Android
- Android NDK r21+ if you buil WORLD 
([Can be installed from Visual Studio](https://docs.microsoft.com/en-us/xamarin/android/get-started/installation/android-sdk))
- Android device with API level 27 (Oreo) or more.

### Building WORLD and wrapper

[WORLD](https://github.com/mmorise/World.git) is a efficient vocoder library. Here
we build `libworld.a` (The static library of WORLD) and `libvoice100_native.so`
(A wrapper shared library for C#).

You can skip this process. `libvoice100_native.so` for `armeabi-v7a`, `arm64-v8a`, `x86`, `x86_64`
are already pre-build in this repository.

Building `libworld.a` is straight forward with
Cmake and Android NDK. Build `libworld.a` for all the architectures you need.

```
set ANDROID_SDK=%USERPROFILE%\AppData\Local\Android\Sdk
set CMAKE=%ANDROID_SDK%\cmake\3.10.2.4988404\bin\cmake.exe
set CMAKE_TOOLCHAIN_FILE=%ANDROID_SDK%\ndk\21.4.7075529\build\cmake\android.toolchain.cmake
rem set ANDROID_ABI=armeabi-v7a
set ANDROID_ABI=arm64-v8a
rem set ANDROID_ABI=x86
rem set ANDROID_ABI=x86_64

git clone https://github.com/mmorise/World.git
cd World
md build\%ANDROID_ABI%-android
cd build\%ANDROID_ABI%-android
%CMAKE% ^
    -DCMAKE_TOOLCHAIN_FILE=%CMAKE_TOOLCHAIN_FILE% ^
    -G Ninja ^
    -DANDROID_ABI=%ANDROID_ABI% ^
    -DANDROID_NATIVE_API_LEVEL=26 ^
    ..\..
ninja
```

Then go to this repository and build `libvoice100_native.so`.

```
set WORLD_ROOT=...
cd native
md build\%ANDROID_ABI%-android
cd build\%ANDROID_ABI%-android
%CMAKE% ^
    -DCMAKE_TOOLCHAIN_FILE=%CMAKE_TOOLCHAIN_FILE% ^
    -G Ninja ^
    -DANDROID_ABI=%ANDROID_ABI% ^
    -DANDROID_NATIVE_API_LEVEL=26 ^
    -DWORLD_INC=%WORLD_ROOT%\src ^
    -DWORLD_LIB=%WORLD_ROOT%\build\%ANDROID_ABI%-android ^
    ..\..
ninja
copy libvoice100_native.so ..\..\..\Voice100AndroidApp\lib\%ANDROID_ABI%
```

### Prepare the model used in the application

We convert ONNX file from
[Voice100 Runtime](https://github.com/kaiidams/voice100-runtime).
The files can be downloaded form
[here](https://github.com/kaiidams/voice100-runtime/blob/main/voice100_runtime/__init__.py).
An STT model needs one ONNX file and a TTS model needs two ONNX files. Download ONNX files
and then run

```
python -m onnxruntime.tools.convert_onnx_models_to_ort --optimization_level all ./
```

Then copy ONNX files to `Voice100AndroidApp\Assets\`.

### Build the application

Open `Voice100AndroidApp.sln` with Visual Studio 2019 and connect your Android device
with a USB cable. Then start build with F5.

`Voice100AndroidApp` uses NuGet package `Microsoft.ML.OnnxRuntime.Managed` which contains
C# part of ONNX Runtime. The native part of ONNX Runtime is already included in this repository,
but if you want to get it,

- Go to [here](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime/) and download
package `microsoft.ml.onnxruntime.1.10.0.nupkg`.
- Rename `microsoft.ml.onnxruntime.1.10.0.nupkg` as `microsoft.ml.onnxruntime.1.10.0.nupkg.zip`
and extract `runtimes\android\native\onnxruntime.aar`.
- Rename `onnxruntime.aar` as `onnxruntime.aar.zip` and extract
`jni\[arch]\libonnxruntime.so` and copy to `Voice100AndroidApp\lib\[arch]`.
