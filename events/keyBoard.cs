using System.Threading.Channels;

public record PlayPauseRequestedEvent : AppEvent;

class KeyboardSource
{
    private readonly ChannelWriter<AppEvent> _writer;

    public KeyboardSource(ChannelWriter<AppEvent> writer)
    {
        _writer = writer;
    }

    public void Start()
    {
        var thread = new Thread(ReadLoop)
        {
            IsBackground = true
        };
        thread.Start();
    }

    private void ReadLoop()
    {
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            _writer.TryWrite(new KeyPressedEvent(key));
        }
    }
}