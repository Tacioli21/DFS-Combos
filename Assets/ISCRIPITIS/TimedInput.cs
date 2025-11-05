using System;

[Serializable]
public struct TimedInput
{
    public string input;
    public float time;

    public TimedInput(string input, float time)
    {
        this.input = input;
        this.time = time;
    }
}
