using System.Threading.Channels;
//public record PlayPauseRequestedEvent : AppEvent;

class StopPlaying
{
    private readonly ChannelWriter<AppEvent> _writer;

    public StopPlaying(ChannelWriter<AppEvent> writer)
    {
        _writer = writer;
    }

    public void Start()
    {
        
    }
}