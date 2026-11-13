using System;
using System.Threading;
using NAudio.Wave;
using System.IO;
using System.Numerics;
using NAudio.Utils;

class Program
{
    static void Main()
    {
        var system = new TerminalInterface();

        system.AddComponent(new SpectrumVisualization(2, 44.1));
        system.AddComponent(new LoadBar());

        system.Work("./");

        Console.WriteLine("\nВоспроизведение остановлено.");
    }
}

class MusicManager : ICoreState, IPlaybackState
{
    static readonly string[] file_types = [".mp3", ".wav"];

    readonly List<TrackInfo> _files = [];

    int _current_pos = 0;

    private AudioFileReader _file_reader;
    private SpectrumSampleProvider _sample_reader;
    private WaveOutEvent _output_device;

    public bool IsPlaying
    {
        get => _output_device.PlaybackState == NAudio.Wave.PlaybackState.Playing;
    }
    public TimeSpan Position { get => _output_device.GetPositionTimeSpan(); }
    public TimeSpan Duration { get => _file_reader.TotalTime; }
    public float[] CurrentSamples { get => _sample_reader.GetLastSamples(); }
    public TrackInfo[] Files { get => [.. _files]; }

    public MusicManager(string dirStr)
    {
        if (string.IsNullOrWhiteSpace(dirStr))
            throw new ArgumentException("Путь к директории не может быть пустым.", nameof(dirStr));

        if (!Directory.Exists(dirStr))
            throw new DirectoryNotFoundException($"Директория не найдена: {dirStr}");

        IEnumerable<string> files = Directory.EnumerateFiles(dirStr, "*.*", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            string extension = Path.GetExtension(file);
            if (!IsSuportedFile(extension)) continue;

            try
            {
                using var audioFile = new AudioFileReader(file);
                TrackInfo info = new() { file_path = file, name = Path.GetFileNameWithoutExtension(file), time = audioFile.TotalTime };
                _files.Add(info);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка чтения файла {file}: {ex.Message}");
            }
        }

        if (_files.Count == 0)
        {
            throw new InvalidOperationException("В указанной директории не найдено поддерживаемых аудиофайлов.");
        }

        _output_device = new WaveOutEvent();
        SetNAudio(_files[0].file_path);
    }

    private void SetNAudio(string file_path)
    {
        _file_reader = new AudioFileReader(file_path);
        _sample_reader = new SpectrumSampleProvider(_file_reader);

        _output_device.Init(_sample_reader);
    }

    public void NextTrack()
    {
        if (_current_pos == _files.Count - 1) return;

        bool wasPlaying = IsPlaying;

        _current_pos++;
        SetNAudio(_files[_current_pos].file_path);

        if (wasPlaying)
        {
            _output_device.Play();
        }
    }

    public void Play()
    {
        if (!IsPlaying)
        {
            _output_device.Play();
        }
    }

    public void Pause()
    {
        if (IsPlaying)
        {
            _output_device.Pause();
        }
    }

    public void SetPlaying(bool playing)
    {
        if (playing) Play();
        else Pause();
    }

    public void SetPosition(TimeSpan position)
    {
        if (position < TimeSpan.Zero)
            position = TimeSpan.Zero;

        if (position > _file_reader.TotalTime)
            position = _file_reader.TotalTime;

        _file_reader.CurrentTime = position;
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

    // public void AddEvent()
    // {
    //     _output_device.PlaybackStopped
    // }
}

struct TrackInfo
{
    public string file_path;
    public string name;
    public TimeSpan time;
}