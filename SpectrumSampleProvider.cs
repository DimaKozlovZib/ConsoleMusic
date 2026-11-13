using System;
using NAudio.Wave;

class SpectrumSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly float[] _circularBuffer;
    private int _bufferIndex = 0;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public SpectrumSampleProvider(ISampleProvider source, int bufferSize = 4096)
    {
        _source = source;
        _circularBuffer = new float[bufferSize];
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);

        for (int i = 0; i < samplesRead; i++)
        {
            _circularBuffer[_bufferIndex] = buffer[offset + i];
            _bufferIndex = (_bufferIndex + 1) % _circularBuffer.Length;
        }

        return samplesRead;
    }

    public float[] GetLastSamples()
    {
        int size = _circularBuffer.Length;
        float[] result = new float[size];

        for (int i = 0; i < size; i++)
        {
            result[i] = _circularBuffer[(_bufferIndex + i) % size];
        }

        return result;
    }
}
