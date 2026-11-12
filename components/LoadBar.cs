using System;
using System.Threading;
using NAudio.Wave;
using System.IO;
using System.Numerics;
using NAudio.Utils;

class LoadBar : IComponent
{
    private int _bar_width = 20;
    private DateTime _render_time = DateTime.Now;
    public bool NeedRender (ICoreState state) => _render_time <= DateTime.Now;

    public void Init()
    {
        
    }

    public void Render(ICoreState state, IComponentBuffer buffer)
    {
        double currentTime = state.Position.TotalMilliseconds;
        double totalLength = state.Duration.TotalMilliseconds;

        double progress = totalLength > 0 ? currentTime / totalLength : 0;

        int countDone = (int)(progress * _bar_width);

        string doneBar = new('█', countDone);
        string remainingBar = new('░', _bar_width - countDone);

        buffer.Write($"\nПрогресс: [{doneBar}{remainingBar}]\n".ToArray());
        buffer.Write($"{state.Position.ToString("mm\\:ss")} / {state.Duration.ToString("mm\\:ss")}".ToArray());

        _render_time = DateTime.Now.AddMilliseconds(1000);
    }
}