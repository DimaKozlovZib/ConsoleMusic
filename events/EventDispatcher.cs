
interface IEventDispatcher
{
    public void Subscribe<TEvent>(Action<TEvent, IPlaybackState> handler) where TEvent : AppEvent;
}

class EventDispatcher: IEventDispatcher
{
    private readonly Dictionary<Type, List<object>> _handlers = new();
    private readonly IPlaybackState _state;

    public EventDispatcher(IPlaybackState state)
    {
        _state = state;
    }

    public void Subscribe<TEvent>(Action<TEvent, IPlaybackState> handler) where TEvent : AppEvent
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var list))
        {
            list = new List<object>();
            _handlers[typeof(TEvent)] = list;
        }
        list.Add(handler);
    }

    public void Dispatch(AppEvent evt)
    {
        if (_handlers.TryGetValue(evt.GetType(), out var list))
        {
            foreach (var handler in list)
            {
                ((Delegate)handler).DynamicInvoke(evt, _state);
            }
        }
    }
}

public abstract record AppEvent;

public record KeyPressedEvent(ConsoleKeyInfo Key) : AppEvent;
public record PlaybackTickEvent(TimeSpan Position) : AppEvent;
public record TrackFinishedEvent : AppEvent;