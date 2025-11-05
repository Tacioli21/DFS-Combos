using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Versão do SimpleComboCube que aceita GameObject para facilitar o arraste no Inspector.
// Ele detecta automaticamente UnityEngine.UI.Text (UI), TextMesh (3D) ou TextMeshProUGUI (se presente).
public class SimpleComboCube_DragFix : MonoBehaviour
{
    [Header("UI (arraste os GameObjects dos textos aqui)")]
    public GameObject bufferTextGO;   // arraste o GameObject que contém o texto (UI Text / TextMesh / TMP)
    public GameObject matchedTextGO;

    // componentes detectados (preenchidos em Start)
    private UnityEngine.UI.Text bufferTextUI;
    private UnityEngine.UI.Text matchedTextUI;
    private TextMesh bufferTextMesh;
    private TextMesh matchedTextMesh;
#if TMP_PRESENT
    private TMPro.TextMeshProUGUI bufferTMP;
    private TMPro.TextMeshProUGUI matchedTMP;
#endif

    [Header("Cube visual")]
    public Renderer playerRenderer;
    public Color flashColor = Color.red;
    public float flashDuration = 0.35f;

    [Header("Floating Text (TextMesh)")]
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
        // detecta componentes de texto do bufferTextGO e matchedTextGO
        if (bufferTextGO != null)
        {
            bufferTextUI = bufferTextGO.GetComponent<UnityEngine.UI.Text>();
            if (bufferTextUI == null) bufferTextMesh = bufferTextGO.GetComponent<TextMesh>();
#if TMP_PRESENT
            if (bufferTextUI == null && bufferTextMesh == null) bufferTMP = bufferTextGO.GetComponent<TMPro.TextMeshProUGUI>();
#endif
        }
        if (matchedTextGO != null)
        {
            matchedTextUI = matchedTextGO.GetComponent<UnityEngine.UI.Text>();
            if (matchedTextUI == null) matchedTextMesh = matchedTextGO.GetComponent<TextMesh>();
#if TMP_PRESENT
            if (matchedTextUI == null && matchedTextMesh == null) matchedTMP = matchedTextGO.GetComponent<TMPro.TextMeshProUGUI>();
#endif
        }

        if (playerRenderer != null)
        {
            instanceMaterial = new Material(playerRenderer.sharedMaterial);
            playerRenderer.material = instanceMaterial;
            originalColor = instanceMaterial.color;
        }

        // inicializa textos (se houver)
        SetMatchedText("");
        UpdateUI();
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
        SetMatchedText($"Combo: {match.id}");
        CancelInvoke(nameof(ClearMatchedText));
        Invoke(nameof(ClearMatchedText), 0.8f);

        if (playerRenderer != null && instanceMaterial != null)
        {
            CancelInvoke(nameof(ResetCubeColor));
            instanceMaterial.color = flashColor;
            Invoke(nameof(ResetCubeColor), flashDuration);
        }

        // spawn texto flutuante (usa TextMesh runtime)
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
        SetMatchedText("");
    }

    private void UpdateUI()
    {
        string s = "Buffer (antigo -> novo):\n";
        for (int i = 0; i < buffer.Count; i++)
            s += $"{i}: {buffer[i].token} @{buffer[i].time:F2}\n";
        SetBufferText(s);
    }

    // helpers para escrever no componente que estiver presente
    private void SetBufferText(string s)
    {
        if (bufferTextUI != null) bufferTextUI.text = s;
        else if (bufferTextMesh != null) bufferTextMesh.text = s;
#if TMP_PRESENT
        else if (bufferTMP != null) bufferTMP.text = s;
#endif
    }

    private void SetMatchedText(string s)
    {
        if (matchedTextUI != null) matchedTextUI.text = s;
        else if (matchedTextMesh != null) matchedTextMesh.text = s;
#if TMP_PRESENT
        else if (matchedTMP != null) matchedTMP.text = s;
#endif
    }

    // Texto flutuante usando TextMesh (builtin)
    private IEnumerator SpawnFloatingText(string text)
    {
        GameObject go = new GameObject("FloatingText");
        go.transform.position = transform.position + Vector3.up * 1.2f;
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