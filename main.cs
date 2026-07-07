using System;
using System.Threading;
using NAudio.Wave;
using System.IO;
using System.Numerics;

class Program
{
    static void Main()
    {
        new MusicManager("./").Play();


        Console.WriteLine("\nВоспроизведение остановлено.");
    }
}

class MusicManager
{
    static readonly string[] file_types = [".mp3", ".wav"];

    readonly List<TrackInfo> _files = [];
    int _current_pos = 0;

    public MusicManager(string dirStr)
    {
        Console.WriteLine(Directory.Exists(dirStr));
        if (Directory.Exists(dirStr))
        {
            IEnumerable<string> files = Directory.EnumerateFiles(dirStr);

            foreach (var file in files)
            {
                string extension = Path.GetExtension(file);
                if (!IsSuportedFile(extension)) continue;

                using var audioFile = new AudioFileReader(file);

                TrackInfo info = new TrackInfo { file_path = file, name = Path.GetFileNameWithoutExtension(file), time = audioFile.TotalTime };
                _files.Add(info);
            }
        }
    }

    public void Play()
    {
        int startTop = Console.CursorTop;
        Console.CursorVisible = false;

        using var audioFile = new AudioFileReader(_files[0].file_path);
        var specProvider = new SpectrumSampleProvider(audioFile);

        using var outputDevice = new WaveOutEvent();

        outputDevice.Init(specProvider);
        outputDevice.Play();

        double totalLength = audioFile.TotalTime.TotalSeconds;
        SpectrumVisualization sv = new(specProvider.WaveFormat.Channels, specProvider.WaveFormat.SampleRate);
        int barWidth = 20;

        while (outputDevice.PlaybackState == PlaybackState.Playing)
        {
            Console.SetCursorPosition(0, startTop);

            sv.Show(specProvider.GetLastSamples());

            double currentTime = audioFile.CurrentTime.TotalSeconds;
            double progress = totalLength > 0 ? currentTime / totalLength : 0;

            int countDone = (int)(progress * barWidth);

            string doneBar = new('█', countDone);
            string remainingBar = new('░', barWidth - countDone);

            Console.Write($"\nПрогресс: [{doneBar}{remainingBar}]\n", ConsoleColor.Red);
            Console.Write($"{audioFile.CurrentTime.ToString("mm\\:ss")} / {audioFile.TotalTime.ToString("mm\\:ss")}");

            if (Console.KeyAvailable)
            {
                ConsoleKeyInfo key = Console.ReadKey(intercept: true);

                if (key.Key == ConsoleKey.Spacebar)
                {
                    outputDevice.Stop();
                    break;
                }
            }

            Thread.Sleep(35);
        }
    }

    static bool IsSuportedFile(string file_type)
    {
        for (int i = 0; i < file_types.Length; i++)
        {
            if (file_type.Equals(file_types[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}

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



struct TrackInfo
{
    public string file_path;
    public string name;
    public TimeSpan time;
}