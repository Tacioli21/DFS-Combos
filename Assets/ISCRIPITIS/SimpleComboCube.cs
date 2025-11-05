using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SimpleComboCube : MonoBehaviour
{
    [Header("UI (arraste os Texts daqui)")]
    public Text bufferText;
    public Text matchedText;

    [Header("Cube visual")]
    public Renderer playerRenderer;
    public Color flashColor = Color.red;
    public float flashDuration = 0.35f;

    [Header("Floating Text")]
    public float floatingTextRise = 0.9f;
    public float floatingTextLife = 1.0f;
    public Color floatingTextColor = Color.yellow;
    public int floatingFontSize = 48;

    [Header("Config")]
    public float inputRetention = 1.2f;
    public float maxDeltaBetweenInputs = 0.45f;

    [Serializable]
    private struct Timed { public string token; public float time; public Timed(string t, float tm) { token = t; time = tm; } }

    private readonly List<Timed> buffer = new List<Timed>();
    private readonly List<(string id, string[] seq)> combos = new List<(string, string[])>
    {
        ("DRLP", new [] { "Down", "Right", "LP" }),
        ("DRHP", new [] { "Down", "Right", "HP" }),
        ("RLP",  new [] { "Right", "LP" })
    };

    private Color originalColor;
    private Material instanceMaterial;

    void Start()
    {
        if (playerRenderer != null)
        {
            instanceMaterial = new Material(playerRenderer.sharedMaterial);
            playerRenderer.material = instanceMaterial;
            originalColor = instanceMaterial.color;
        }
        if (matchedText != null) matchedText.text = "";
    }

    void Update()
    {
        // teclas: U = Down, I = Right, J = LP, K = HP
        if (Input.GetKeyDown(KeyCode.U)) AddInput("Down");
        if (Input.GetKeyDown(KeyCode.I)) AddInput("Right");
        if (Input.GetKeyDown(KeyCode.J)) AddInput("LP");
        if (Input.GetKeyDown(KeyCode.K)) AddInput("HP");

        TrimBuffer();
        UpdateUI();

        var match = TryMatchCombo();
        if (match != null) OnComboMatched(match.Value);
    }

    private void AddInput(string token)
    {
        buffer.Add(new Timed(token, Time.time));
        TrimBuffer();
        Debug.Log($"Input: {token} @ {Time.time:F2}");
    }

    private void TrimBuffer()
    {
        float now = Time.time;
        for (int i = 0; i < buffer.Count;)
        {
            if (now - buffer[i].time > inputRetention) buffer.RemoveAt(i);
            else i++;
        }
    }

    private (string id, int length)? TryMatchCombo()
    {
        if (buffer.Count == 0) return null;
        foreach (var combo in combos)
        {
            var seq = combo.seq;
            if (seq.Length > buffer.Count) continue;
            int startIndex = buffer.Count - seq.Length;
            bool ok = true;
            for (int i = 0; i < seq.Length; i++)
            {
                if (buffer[startIndex + i].token != seq[i]) { ok = false; break; }
                if (i > 0)
                {
                    float dt = buffer[startIndex + i].time - buffer[startIndex + i - 1].time;
                    if (dt > maxDeltaBetweenInputs) { ok = false; break; }
                }
            }
            if (ok) return (combo.id, seq.Length);
        }
        return null;
    }

    private void OnComboMatched((string id, int length) match)
    {
        Debug.Log($"Combo matched: {match.id}");
        if (matchedText != null)
        {
            matchedText.text = $"Combo: {match.id}";
            CancelInvoke(nameof(ClearMatchedText));
            Invoke(nameof(ClearMatchedText), 0.8f);
        }

        if (playerRenderer != null && instanceMaterial != null)
        {
            CancelInvoke(nameof(ResetCubeColor));
            instanceMaterial.color = flashColor;
            Invoke(nameof(ResetCubeColor), flashDuration);
        }

        // spawn texto flutuante
        StartCoroutine(SpawnFloatingText(match.id));

        // remover inputs usados (simples)
        int removeCount = match.length;
        for (int i = 0; i < removeCount && buffer.Count >= match.length; i++)
            buffer.RemoveAt(buffer.Count - match.length);
    }

    private void ResetCubeColor()
    {
        if (instanceMaterial != null) instanceMaterial.color = originalColor;
    }

    private void ClearMatchedText()
    {
        if (matchedText != null) matchedText.text = "";
    }

    private void UpdateUI()
    {
        if (bufferText == null) return;
        string s = "Buffer (antigo -> novo):\n";
        for (int i = 0; i < buffer.Count; i++)
            s += $"{i}: {buffer[i].token} @{buffer[i].time:F2}\n";
        bufferText.text = s;
    }

    // Texto flutuante usando TextMesh (builtin)
    private System.Collections.IEnumerator SpawnFloatingText(string text)
    {
        GameObject go = new GameObject("FloatingText");
        go.transform.position = transform.position + Vector3.up * 1.2f;
        // deixar olhando para a câmera
        if (Camera.main) go.transform.rotation = Camera.main.transform.rotation;

        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = floatingFontSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = floatingTextColor;
        go.transform.localScale = Vector3.one * 0.02f;

        float t = 0f;
        Vector3 startPos = go.transform.position;
        while (t < floatingTextLife)
        {
            t += Time.deltaTime;
            float norm = t / floatingTextLife;
            go.transform.position = startPos + Vector3.up * (floatingTextRise * norm);
            Color c = tm.color; c.a = Mathf.Lerp(1f, 0f, norm); tm.color = c;
            if (Camera.main) go.transform.rotation = Camera.main.transform.rotation;
            yield return null;
        }
        Destroy(go);
    }
}