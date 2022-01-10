#include <cstdint>
#include <cstdlib>
#include <cmath>
#include <world/synthesis.h>
#include <world/codec.h>

#ifdef __GNUC__
#define VOICE100_EXPORT __attribute__((visibility("default")))
#endif
#ifdef _WIN32
#define VOICE100_EXPORT __declspec(dllexport)
#endif

extern "C" VOICE100_EXPORT int Voice100Sharp_VocoderDecode(
    const float* f0, const float* logspc, const float* codedap, int f0_length,
    int fft_size, double frame_period, int fs, float log_offset, int16_t* y, int y_length)
{
    if (y == nullptr)
    {
        return static_cast<int>((f0_length - 1) *
            frame_period / 1000.0 * fs) + 1;
    }

    int spectrogram_dim = fft_size / 2 + 1;
    int coded_aperiodicity_dim = 1;

    double** coded_aperiodicity = new double*[f0_length];
    double* coded_aperiodicity_data = new double[coded_aperiodicity_dim * f0_length];
    for (int i = 0; i < f0_length; ++i) coded_aperiodicity[i] = coded_aperiodicity_data + coded_aperiodicity_dim * i;
    for (int i = 0; i < coded_aperiodicity_dim * f0_length; ++i) coded_aperiodicity_data[i] = codedap[i];

    double** aperiodicity = new double*[f0_length];
    double* aperiodicity_data = new double[spectrogram_dim * f0_length];
    for (int i = 0; i < f0_length; ++i) aperiodicity[i] = aperiodicity_data + spectrogram_dim * i;
 
    DecodeSpectralEnvelope(
        coded_aperiodicity, f0_length, fs, fft_size,
        coded_aperiodicity_dim, aperiodicity);

    delete[] coded_aperiodicity;
    delete[] coded_aperiodicity_data;

    double* f0_data = new double[f0_length];
    for (int i = 0; i < f0_length; ++i) f0_data[i] = f0[i];

    double** spectrogram = new double*[f0_length];
    double* spectrogram_data = new double[spectrogram_dim * f0_length];
    for (int i = 0; i < f0_length; ++i) spectrogram[i] = spectrogram_data + spectrogram_dim * i;
    for (int i = 0; i < spectrogram_dim * f0_length; ++i) spectrogram_data[i] = std::exp(logspc[i] + log_offset);

    double* y_data = new double[y_length];

    Synthesis(
        f0_data, f0_length,
        spectrogram,
        aperiodicity,
        fft_size, frame_period, fs,
        y_length, y_data);

    for (int i = 0; i < y_length; ++i) y[i] = static_cast<int16_t>(32767 * y_data[i]);

    delete[] f0_data;

    delete[] spectrogram;
    delete[] spectrogram_data;

    delete[] aperiodicity;
    delete[] aperiodicity_data;

    return y_length;
}