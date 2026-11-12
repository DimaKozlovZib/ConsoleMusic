using System.Runtime.CompilerServices;
using System.Threading.Channels;

class TerminalInterface
{
    private List<IComponent> _components;
    private Channel<AppEvent> _channel = Channel.CreateUnbounded<AppEvent>();
    private MusicManager _music_manager;
    private TerminalBuffer _console_buffer;

    public TerminalInterface()
    {
        _components = [];
    }

    public void AddComponent(IComponent component)
    {
        _components.Add(component);
    }

    private async Task RunEventLoop(EventDispatcher dispatcher, CancellationToken token)
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync(token))
        {
            dispatcher.Dispatch(evt);
        }
    }

    private async Task RunRenderLoop(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        Console.WriteLine("dsddsds");
        int startTop = Console.CursorTop;
        Console.CursorVisible = false;

        while (await timer.WaitForNextTickAsync(token))
        {

            Console.SetCursorPosition(0, startTop);

            for (int i = 0; i < _components.Count; i++)
            {
                var component = _components[i];
                if (component.NeedRender(_music_manager))
                {
                    component.Render(_music_manager, _console_buffer.GetBuffer(i));
                }
            }
            _console_buffer.Flush();
        }
    }

    public void Work(string dirPath)
    {
        if (_components.Count == 0) return;

        _console_buffer = new(_components.Count);
        _music_manager = new(dirPath);

        var dispatcher = new EventDispatcher(_music_manager);

        var keyEvent = new KeyboardSource(_channel.Writer);
        keyEvent.Start();

        using var cts = new CancellationTokenSource();

        dispatcher.Subscribe<KeyPressedEvent>((ev, state) =>
                {
                    if (ev.Key.Key == ConsoleKey.Q) cts.Cancel();
                });

        _music_manager.Play();

        var eventLoopTask = RunEventLoop(dispatcher, cts.Token);
        var renderLoopTask = RunRenderLoop(cts.Token);

        try
        {
            Task.WaitAll(eventLoopTask, renderLoopTask);
        }
        catch (AggregateException ex)
        {
            foreach (var inner in ex.InnerExceptions)
            {
                if (inner is TaskCanceledException)
                {
                    continue;
                }
                Console.WriteLine($"Ошибка: {inner.GetType().Name}: {inner.Message}");
                Console.WriteLine(inner.StackTrace);
            }
        }
    }
}

interface IComponent
{
    bool NeedRender(ICoreState state);

    void Render(ICoreState state, IComponentBuffer buffer);

    void Init();
}

interface IComponentBuffer
{
    public void Write(char[] text, ConsoleColor color);
    public void Write(char[] text);
}

class TerminalBuffer : IComponentBuffer
{
    private List<char[]>[] _data;
    private List<ConsoleColor>[] _colors;

    private readonly ConsoleColor _system_color = Console.ForegroundColor;

    private int _current_comp = 0;

    public TerminalBuffer(int componentCount)
    {
        _data = new List<char[]>[componentCount];
        _colors = new List<ConsoleColor>[componentCount];

        for (int i = 0; i < componentCount; i++)
        {
            _data[i] = [];
            _colors[i] = [];
        }
    }

    public IComponentBuffer GetBuffer(int comp_idx)
    {
        if (comp_idx >= _data.Length || comp_idx < 0) throw new ArgumentException("Неверный индекс компонента");

        _current_comp = comp_idx;
        _data[_current_comp].Clear();
        _colors[_current_comp].Clear();

        return this;
    }

    public void Write(char[] text, ConsoleColor color)
    {
        if (text.Length != 0)
        {
            _data[_current_comp].Add(text);
            _colors[_current_comp].Add(color);
        }
    }

    public void Write(char[] text)
    {
        Write(text, _system_color);
    }

    public void Flush()
    {
        for (int i = 0; i < _data.Length; i++)
        {
            for (int j = 0; j < _data[i].Count; j++)
            {
                Console.ForegroundColor = _colors[i][j];
                if (_data[i][j] is not null) Console.Write(_data[i][j]);
            }
        }
        Console.ResetColor();
    }
}

interface ICoreState
{
    public bool IsPlaying { get; }
    public TimeSpan Position { get; }
    public TimeSpan Duration { get; }
    public float[] CurrentSamples { get; }
    public TrackInfo[] Files { get; }
}

interface IPlaybackState : ICoreState
{
    public void SetPlaying(bool playing);
    public void SetPosition(TimeSpan position);
    public void NextTrack();
}