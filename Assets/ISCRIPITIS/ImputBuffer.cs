using System.Collections.Generic;

public class InputBuffer
{
    private readonly List<TimedInput> buffer = new List<TimedInput>();
    private readonly float retentionSeconds;

    public InputBuffer(float retentionSeconds = 1.5f)
    {
        this.retentionSeconds = retentionSeconds;
    }

    public void Add(string input, float time)
    {
        buffer.Add(new TimedInput(input, time));
        Trim(time);
    }

    private void Trim(float now)
    {
        int removeCount = 0;
        while (removeCount < buffer.Count && now - buffer[removeCount].time > retentionSeconds)
            removeCount++;
        if (removeCount > 0)
            buffer.RemoveRange(0, removeCount);
    }

    public TimedInput[] GetSnapshot()
    {
        return buffer.ToArray();
    }

    public int Count => buffer.Count;

    public static InputBuffer FromSnapshot(TimedInput[] snapshot, float retentionSeconds)
    {
        var ib = new InputBuffer(retentionSeconds);
        foreach (var ti in snapshot) ib.buffer.Add(ti);
        return ib;
    }
}