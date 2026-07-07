using System;
using System.Threading;
using NAudio.Wave;
using System.IO;
using System.Numerics;

class SpectrumVisualization
{
    private const double MinDb = -45.0;
    private const double CutoffFrequency = 35.0;

    private static double _adaptiveMaxDb = -3.0;
    private static readonly int BarsHeight = 11;
    private static readonly int BarsCount = 80;
    
    private readonly int _channels;
    private readonly double _sampleRate;


    public SpectrumVisualization(int channels, double sampleRate)
    {
        _channels = channels;
        _sampleRate = sampleRate;
    }

    private float[] ToMonoSamples(float[] samples)
    {
        float[] monoSamples;
        if (_channels == 2)
        {
            monoSamples = new float[samples.Length / 2];
            for (int i = 0; i < monoSamples.Length; i++)
            {
                monoSamples[i] = (samples[2 * i] + samples[2 * i + 1]) / 2.0f;
            }
            return monoSamples;
        }
        return samples;
    }

    public void Show(float[] samples)
    {
        float[] monoSamples = ToMonoSamples(samples);
        float[] windowedSamples = ApplyHannWindow(monoSamples);

        Complex[] fftResult = FFT(windowedSamples);

        int halfLength = fftResult.Length / 2;
        Complex[] spectrumHalf = new Complex[halfLength - 1];
        Array.Copy(fftResult, 1, spectrumHalf, 0, halfLength - 1);

        double[] magnitudes = GetMagnitude(spectrumHalf);
        double fftSize = windowedSamples.Length;

        for (int j = 0; j < magnitudes.Length; j++)
        {
            double frequency = (j + 1) * (_sampleRate / fftSize);

            double h_f = frequency / Math.Sqrt(frequency * frequency + CutoffFrequency * CutoffFrequency);
            magnitudes[j] *= h_f;
        }

        double[] bars = ToDecibels(CompressToBars(magnitudes, BarsCount));

        double frameMax = bars.Max();

        _adaptiveMaxDb = frameMax > _adaptiveMaxDb ?
         frameMax : Math.Max(-3.0, _adaptiveMaxDb - 0.15);

        double currentMaxDb = Math.Max(0.0, _adaptiveMaxDb + 1.0);
        int[] normalized = new int[bars.Length];

        for (int i = 0; i < bars.Length; i++)
        {
            double db = bars[i];

            if (db < MinDb) db = MinDb;
            if (db > currentMaxDb) db = currentMaxDb;

            normalized[i] = (int)((db - MinDb) / (currentMaxDb - MinDb) * (BarsHeight + 1));
        }

        PrintBars(normalized);
    }

    private static void PrintBars(int[] bars)
    {
        ConsoleColor mainColor = Console.ForegroundColor;
        for (int i = BarsHeight - 1; i >= 0; i--)
        {
            var line = new string(Enumerable.Range(0, bars.Length)
                .Select(j => j % 2 == 0 ? (bars[j] > i ? '█' : ' ') : ' ')
                .ToArray());

            int maxWidth = Math.Min(line.Length, Console.WindowWidth - 1);
            if (maxWidth > 0)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine(line[..maxWidth].PadRight(Console.WindowWidth - 1));
            }
        }
        Console.ForegroundColor = mainColor;
    }

    private static float[] ApplyHannWindow(float[] samples)
    {
        int size = samples.Length;
        float[] windowed = new float[size];
        for (int i = 0; i < size; i++)
        {
            float windowValue = 0.5f * (1.0f - (float)Math.Cos(2.0 * Math.PI * i / (size - 1)));
            windowed[i] = samples[i] * windowValue;
        }
        return windowed;
    }

    public static double[] CompressToBars(double[] magnitudes, int barCount)
    {
        int fftSize = magnitudes.Length;
        double[] bars = new double[barCount];

        for (int i = 0; i < barCount; i++)
        {
            double percentStart = (double)i / barCount;
            double percentEnd = (double)(i + 1) / barCount;

            int indexStart = (int)(Math.Pow(percentStart, 1.7) * fftSize);
            int indexEnd = (int)(Math.Pow(percentEnd, 1.7) * fftSize);

            if (indexEnd <= indexStart) indexEnd = indexStart + 1;
            if (indexEnd > fftSize) indexEnd = fftSize;

            double sum = 0;
            int count = 0;
            for (int j = indexStart; j < indexEnd; j++)
            {
                sum += magnitudes[j];
                count++;
            }

            bars[i] = sum / count;
        }

        return bars;
    }

    static double[] ToDecibels(double[] magnitude)
    {
        double[] res = new double[magnitude.Length];
        for (int i = 0; i < magnitude.Length; i++)
        {
            res[i] = 20 * Math.Log10(Math.Max(magnitude[i], 1e-5));
        }
        return res;
    }

    static double[] GetMagnitude(Complex[] Spectrum)
    {
        double[] res = new double[Spectrum.Length];
        for (int i = 0; i < Spectrum.Length; i++)
        {
            res[i] = Spectrum[i].Magnitude;
        }
        return res;
    }

    static Complex[] FFT(float[] samples)
    {
        int N = samples.Length;
        if (N == 1) return [new Complex(samples[0], 0)];

        if ((N & (N - 1)) != 0)
        {
            int ex = (int)Math.Log2(N);
            N = (int)Math.Pow(2, ex + 1);
        }

        float[] even = new float[N / 2];
        float[] odd = new float[N / 2];

        for (int i = 0; i < N / 2; i++)
        {
            if (2 * i < samples.Length) even[i] = samples[2 * i];
            if (2 * i + 1 < samples.Length) odd[i] = samples[2 * i + 1];
        }

        Complex[] E = FFT(even);
        Complex[] O = FFT(odd);

        Complex[] X = new Complex[N];
        for (int k = 0; k < N / 2; k++)
        {
            double angle = -2 * Math.PI * k / N;
            Complex rotate = new(Math.Cos(angle), Math.Sin(angle));

            Complex component = rotate * O[k];

            X[k] = E[k] + component;
            X[k + N / 2] = E[k] - component;
        }

        return X;
    }
}