using System;
using System.Collections.Generic;

[Serializable]
public class ComboNode
{
    public string name;
    public List<ComboEdge> edges = new List<ComboEdge>();

    public bool isComboEnd;
    public string comboId;

    public ComboNode(string name = "node")
    {
        this.name = name;
    }

    public void AddEdge(string input, ComboNode child, float maxDeltaTime = 0.45f)
    {
        edges.Add(new ComboEdge { input = input, child = child, maxDeltaTime = maxDeltaTime });
    }
}

[Serializable]
public class ComboEdge
{
    public string input;
    public float maxDeltaTime;
    public ComboNode child;
}