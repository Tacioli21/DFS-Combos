using System.Collections.Generic;

public class ComboMatcher
{
    private readonly ComboNode root;
    private readonly float defaultMaxDelta = 0.45f;

    public ComboMatcher(ComboNode root)
    {
        this.root = root;
    }

    public struct MatchResult
    {
        public bool matched;
        public string comboId;
        public int length;
        public TimedInput[] consumed;
    }

    public MatchResult FindBestMatch(TimedInput[] inputBuffer)
    {
        MatchResult best = new MatchResult { matched = false, length = 0, consumed = new TimedInput[0] };

        for (int start = 0; start < inputBuffer.Length; start++)
        {
            var path = new List<TimedInput>();
            DFSExplore(root, start, inputBuffer, path, ref best);
        }

        return best;
    }

    private void DFSExplore(ComboNode node, int index, TimedInput[] inputBuffer, List<TimedInput> pathSoFar, ref MatchResult best)
    {
        if (node.isComboEnd)
        {
            if (pathSoFar.Count > best.length)
            {
                best.matched = true;
                best.comboId = node.comboId;
                best.length = pathSoFar.Count;
                best.consumed = pathSoFar.ToArray();
            }
        }

        if (index >= inputBuffer.Length) return;

        foreach (var edge in node.edges)
        {
            var candidate = inputBuffer[index];

            if (candidate.input != edge.input) continue;

            if (pathSoFar.Count > 0)
            {
                float prevTime = pathSoFar[pathSoFar.Count - 1].time;
                float delta = candidate.time - prevTime;
                float allowed = (edge.maxDeltaTime > 0f) ? edge.maxDeltaTime : defaultMaxDelta;
                if (delta > allowed) continue;
            }

            pathSoFar.Add(candidate);
            DFSExplore(edge.child, index + 1, inputBuffer, pathSoFar, ref best);
            pathSoFar.RemoveAt(pathSoFar.Count - 1);
        }

        DFSExplore(node, index + 1, inputBuffer, pathSoFar, ref best);
    }
}