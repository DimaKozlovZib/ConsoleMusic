using System;
using System.Numerics;

class PreProcessingSamples
{
    private double[] _magnitudes = Array.Empty<double>();
    private Complex[] _fftResult = Array.Empty<Complex>();

    public double[] Magnitudes => _magnitudes;

    public void NextPart(float[] samples)
    {
        int n = samples.Length;

        if ((n & (n - 1)) != 0)
        {
            int ex = (int)Math.Log2(n);
            n = (int)Math.Pow(2, ex + 1);
        }

        ApplyHannWindow(samples);
        EnsureBufferSizes(n);

        FFTCore(samples, _fftResult.AsSpan(0, n), offset: 0, stride: 1);

        SetMagnitude(n);
    }

    private static void ApplyHannWindow(float[] samples)
    {
        int size = samples.Length;
        float denominator = size - 1;
        for (int i = 0; i < size; i++)
        {
            float windowValue = 0.5f * (1.0f - (float)Math.Cos(2.0 * Math.PI * i / denominator));
            samples[i] *= windowValue;
        }
    }

    private void EnsureBufferSizes(int n)
    {
        if (_fftResult.Length < n)
        {
            _fftResult = new Complex[n];
        }

        int spectrumLength = n / 2;
        if (_magnitudes.Length < spectrumLength)
        {
            _magnitudes = new double[spectrumLength];
        }
    }

    private void SetMagnitude(int n)
    {
        int half = n / 2;
        for (int i = 0; i < half; i++)
        {
            _magnitudes[i] = _fftResult[i].Magnitude;
        }
    }

    private static void FFTCore(ReadOnlySpan<float> samples, Span<Complex> X, int offset, int stride)
    {
        int N = X.Length;

        if (N == 1)
        {
            X[0] = (offset < samples.Length) ? new Complex(samples[offset], 0) : Complex.Zero;
            return;
        }

        int half = N / 2;

        Span<Complex> E = X.Slice(0, half);
        Span<Complex> O = X.Slice(half, half);

        FFTCore(samples, E, offset, stride * 2);
        FFTCore(samples, O, offset + stride, stride * 2);

        for (int k = 0; k < half; k++)
        {
            double angle = -2 * Math.PI * k / N;
            Complex rotate = new(Math.Cos(angle), Math.Sin(angle));

            Complex eValue = E[k];
            Complex oValue = O[k];

            Complex component = rotate * oValue;

            X[k] = eValue + component;
            X[k + half] = eValue - component;
        }
    }
}

class SpectrumAnalyzer
{
    private const double MinDecibels = -45.0;
    private const double SilenceFloor = 1e-5;
    private const double HighPassCutoffHz = 35.0;
    private const double BarCompressionExponent = 1.7;
    private const double AdaptiveGainDecayPerFrame = 0.15;
    private const double AdaptiveGainHeadroomDb = 1.0;

    private double _adaptiveMaxDb = -3.0;

    private readonly double[] _bars;
    private readonly int[] _normalized;

    private readonly double _sampleRate;
    private readonly int _barsHeight;
    private readonly int _barsCount;

    public int[] NormalizedBars => _normalized;

    public SpectrumAnalyzer(double sampleRate, int barsCount, int barsHeight)
    {
        _sampleRate = sampleRate;
        _barsCount = barsCount;
        _barsHeight = barsHeight;

        _bars = new double[barsCount];
        _normalized = new int[barsCount];
    }

    public double[] CompressToBars(double[] magnitudes, int barCount)
    {
        int fftSize = magnitudes.Length;

        for (int i = 0; i < barCount; i++)
        {
            double percentStart = (double)i / barCount;
            double percentEnd = (double)(i + 1) / barCount;

            int indexStart = (int)(Math.Pow(percentStart, BarCompressionExponent) * fftSize);
            int indexEnd = (int)(Math.Pow(percentEnd, BarCompressionExponent) * fftSize);

            if (indexEnd <= indexStart) indexEnd = indexStart + 1;
            if (indexEnd > fftSize) indexEnd = fftSize;

            double sum = 0;
            int count = 0;
            for (int j = indexStart; j < indexEnd; j++)
            {
                sum += magnitudes[j];
                count++;
            }

            _bars[i] = sum / count;
        }
        return _bars;
    }

    private static double[] ToDecibels(double[] magnitudes)
    {
        for (int i = 0; i < magnitudes.Length; i++)
        {
            magnitudes[i] = 20 * Math.Log10(Math.Max(magnitudes[i], SilenceFloor));
        }
        return magnitudes;
    }

    public void NextPart(double[] magnitudes)
    {
        for (int j = 0; j < magnitudes.Length; j++)
        {
            double frequency = (j + 1) * (_sampleRate / (magnitudes.Length * 2));
            double h_f = frequency / Math.Sqrt(frequency * frequency + HighPassCutoffHz * HighPassCutoffHz);
            magnitudes[j] *= h_f;
        }

        double[] bars = ToDecibels(CompressToBars(magnitudes, _barsCount));

        double frameMax = double.MinValue;
        for (int i = 0; i < bars.Length; i++)
        {
            if (bars[i] > frameMax) frameMax = bars[i];
        }

        _adaptiveMaxDb = frameMax > _adaptiveMaxDb
            ? frameMax
            : Math.Max(-3.0, _adaptiveMaxDb - AdaptiveGainDecayPerFrame);

        double currentMaxDb = Math.Max(0.0, _adaptiveMaxDb + AdaptiveGainHeadroomDb);

        for (int i = 0; i < bars.Length; i++)
        {
            double db = bars[i];

            if (db < MinDecibels) db = MinDecibels;
            if (db > currentMaxDb) db = currentMaxDb;

            _normalized[i] = (int)((db - MinDecibels) / (currentMaxDb - MinDecibels) * _barsHeight);
        }
    }
}

class SpectrumVisualization : IComponent
{
    private const int BarsHeight = 11;
    private const int BarsCount = 80;

    private readonly int _channels;
    private float[] _monoBuffer = Array.Empty<float>();
    private DateTime _render_time = DateTime.Now;

    private readonly PreProcessingSamples _fftPreProc = new();
    private readonly SpectrumAnalyzer _analyzer;

    public bool NeedRender(ICoreState state)
    {
        return _render_time <= DateTime.Now && state.IsPlaying;
    }

    public void Init()
    {
        
    }

    public void Render(ICoreState state, IComponentBuffer buffer)
    {
        float[] monoSamples = PrepareMonoSamples(state.CurrentSamples);

        _fftPreProc.NextPart(monoSamples);

        _analyzer.NextPart(_fftPreProc.Magnitudes);

        PrintBars(_analyzer.NormalizedBars,  buffer);

        _render_time = DateTime.Now.AddMilliseconds(40);
    }

    public SpectrumVisualization(int channels, double sampleRate)
    {
        _channels = channels;
        _analyzer = new SpectrumAnalyzer(sampleRate, BarsCount, BarsHeight);
    }

    private float[] PrepareMonoSamples(float[] samples)
    {
        if (_channels == 2)
        {
            int monoLength = samples.Length / 2;
            if (_monoBuffer.Length != monoLength)
            {
                _monoBuffer = new float[monoLength];
            }

            for (int i = 0; i < monoLength; i++)
            {
                _monoBuffer[i] = (samples[2 * i] + samples[2 * i + 1]) / 2.0f;
            }
            return _monoBuffer;
        }

        if (_monoBuffer.Length != samples.Length)
        {
            _monoBuffer = new float[samples.Length];
        }
        Array.Copy(samples, _monoBuffer, samples.Length);
        return _monoBuffer;
    }

    private static void PrintBars(int[] bars,  IComponentBuffer buffer)
    {
        ConsoleColor mainColor = Console.ForegroundColor;

        int windowWidth = 80;
        try { windowWidth = Console.WindowWidth; } catch { }
        int maxWidth = Math.Max(1, windowWidth - 1);

        char[] lineBuffer = new char[bars.Length];

        for (int i = BarsHeight - 1; i >= 0; i--)
        {
            for (int j = 0; j < bars.Length; j++)
            {
                lineBuffer[j] = (j % 2 == 0 && bars[j] > i) ? '█' : ' ';
            }

            string line = new(lineBuffer);
            if (line.Length > maxWidth)
            {
                line = line[..maxWidth];
            }
            else
            {
                line = line.PadRight(maxWidth);
            }

            buffer.Write(line.ToArray(), ConsoleColor.Magenta);
            buffer.Write(['\n']);
        }
    }
}