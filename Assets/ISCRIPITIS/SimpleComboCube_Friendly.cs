using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SimpleComboCube_Friendly
/// - Anexe ao cubo.
/// - Aceita GameObject para Buffer/Matched (UI Text ou TextMesh).
/// - Mantém buffer de inputs com timestamps.
/// - Constrói uma trie (ComboTrie) a partir de combos declarados.
/// - Usa DFS na trie para encontrar combos alinhados ao final do buffer,
///   respeitando tempo máximo entre entradas.
/// - Atualiza BufferText (legível) e MatchedText (nome/descrição), além de criar texto flutuante.
/// - Nesta versão: prioridade absoluta ao combo mais longo, remoção apenas das entradas usadas,
///   cooldown e uma pequena janela (extensionWindow) para aguardar possíveis extensões de combos
///   (evita que um combo curto dispare antes do jogador completar o combo longo).
/// - Cole em Assets/ISCRIPITIS/SimpleComboCube_Friendly.cs
/// </summary>
public class SimpleComboCube_Friendly : MonoBehaviour
{
    [Header("Arraste os GameObjects de texto (UI Text ou TextMesh)")]
    public GameObject bufferTextGO;
    public GameObject matchedTextGO;

    // componentes detectados
    private UnityEngine.UI.Text bufferTextUI;
    private UnityEngine.UI.Text matchedTextUI;
    private TextMesh bufferTextMesh;
    private TextMesh matchedTextMesh;

    [Header("Cube visual")]
    public Renderer playerRenderer;
    public Color flashColor = Color.red;
    public float flashDuration = 0.35f;

    [Header("Floating Text (TextMesh)")]
    public float floatingTextRise = 0.9f;
    public float floatingTextLife = 1.0f;
    public Color floatingTextColor = Color.yellow;
    public int floatingFontSize = 48;

    [Header("Config de input")]
    public float inputRetention = 1.2f;         // tempo máximo para manter inputs no buffer
    public float maxDeltaBetweenInputs = 0.45f; // janela máxima entre entradas consecutivas

    [Header("Combos / comportamento de detecção")]
    // Evita múltiplos disparos rapidamente
    private float lastMatchTime = -10f;
    public float matchCooldown = 0.45f;      // aumentado para reduzir sobreposição (segundos)
    // Se true, limpa todo o buffer ao detectar um combo (evita sobreposição).
    // Por padrão deixamos false para remover apenas as entradas usadas — evita remover possíveis prefixes úteis.
    public bool clearBufferOnMatch = false;

    [Header("Extensão")]
    [Tooltip("Tempo (s) para aguardar uma possível continuação do combo antes de confirmar um combo mais curto.")]
    public float extensionWindow = 0.15f;

    [Serializable]
    private struct Timed { public string token; public float time; public Timed(string t, float tm) { token = t; time = tm; } }

    private readonly List<Timed> buffer = new List<Timed>(); // antigo -> novo

    // ---------- Defina aqui os combos (ids únicos) ----------
    // Cada combo é (id, sequência de tokens).
    private readonly List<(string id, string[] seq)> combosList = new List<(string, string[])>
    {
        ("Ruptura",         new [] { "Right", "LP" }),                 // I J
        ("Carga Avançada",  new [] { "Down", "Right", "LP" }),         // U I J
        ("Quebra de Guarda",new [] { "Down", "Right", "HP" }),         // U I K
        ("Colosso",         new [] { "Down", "Right", "LP", "HP" }),   // U I J K
        ("Perfuração",      new [] { "Right", "LP", "Right", "HP" }),  // I J I K
        ("Dobrador",        new [] { "Down", "Down", "Right", "LP" })  // U U I J
    };
    // --------------------------------------------------------

    // Informações amigáveis para exibir
    private readonly Dictionary<string, (string title, string desc, Color color)> comboInfo = new Dictionary<string, (string,string,Color)>
    {
        { "Ruptura", ("Ruptura", "Pressão rápida — 60 dmg", Color.green) },
        { "Carga Avançada", ("Carga Avançada", "120 dmg", Color.cyan) },
        { "Quebra de Guarda", ("Quebra de Guarda", "Atordoamento", Color.red) },
        { "Colosso", ("Colosso", "Ataque pesado — alto dano", new Color(0.9f, 0.45f, 0.1f)) },
        { "Perfuração", ("Perfuração", "Sequência técnica — perfura guarda", new Color(0.6f, 0.2f, 0.8f)) },
        { "Dobrador", ("Dobrador", "Carga dupla — empurrão", new Color(0.2f, 0.7f, 0.2f)) }
    };

    // Trie node
    private class ComboNode
    {
        public Dictionary<string, ComboNode> children = new Dictionary<string, ComboNode>();
        public bool isEnd = false;
        public string comboId = null;
    }

    private ComboNode root = new ComboNode();
    private Color originalColor;
    private Material instanceMaterial;

    void Start()
    {
        // detecta componentes de texto no GameObject arrastado
        if (bufferTextGO != null)
        {
            bufferTextUI = bufferTextGO.GetComponent<UnityEngine.UI.Text>();
            if (bufferTextUI == null) bufferTextMesh = bufferTextGO.GetComponent<TextMesh>();
        }
        if (matchedTextGO != null)
        {
            matchedTextUI = matchedTextGO.GetComponent<UnityEngine.UI.Text>();
            if (matchedTextUI == null) matchedTextMesh = matchedTextGO.GetComponent<TextMesh>();
        }

        // instanciar material para alterar cor sem afetar sharedMaterial
        if (playerRenderer != null)
        {
            instanceMaterial = new Material(playerRenderer.sharedMaterial);
            playerRenderer.material = instanceMaterial;
            originalColor = instanceMaterial.color;
        }

        BuildTrieFromList();

        SetBufferText("Entradas: —");
        SetMatchedText("");
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

        // procura combos alinhados ao final do buffer usando DFS na trie
        var match = TryMatchComboWithTrie();
        if (match != null) OnComboMatched(match.Value);
    }

    // -------- Buffer manipulation --------
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

    // -------- Trie building --------
    private void BuildTrieFromList()
    {
        root = new ComboNode();
        foreach (var combo in combosList)
        {
            var node = root;
            foreach (var token in combo.seq)
            {
                if (!node.children.TryGetValue(token, out var child))
                {
                    child = new ComboNode();
                    node.children[token] = child;
                }
                node = child;
            }
            node.isEnd = true;
            node.comboId = combo.id;
        }
    }

    // -------- DFS search aligned to buffer end --------
    // Retorna (id, length) do melhor match ou null.
    // Critérios: match termina no final do buffer; entre entradas consecutive <= maxDeltaBetweenInputs.
    private (string id, int length)? TryMatchComboWithTrie()
    {
        if (buffer.Count == 0) return null;

        int maxComboLen = GetMaxComboLength();
        int startMin = Mathf.Max(0, buffer.Count - maxComboLen);

        (string id, int length)? best = null;

        for (int start = startMin; start < buffer.Count; start++)
        {
            DFS_Search(root, start, -1f, 0, ref best, start);
        }

        if (best == null) return null;

        // Se o candidato encontrado pode ser estendido (há filhos na trie a partir do nó final desse candidato),
        // aguardamos a extensionWindow após o último input antes de confirmar o combo curto.
        // Isso evita disparar um combo curto quando o jogador está prestes a apertar a próxima tecla para formar um combo longo.
        float lastInputTime = buffer[buffer.Count - 1].time;
        // encontra o nó correspondente ao best.id seguindo a sequência (do último comprimento)
        var endNode = FindNodeForMatch(best.Value.id, best.Value.length);
        if (endNode != null && endNode.children.Count > 0)
        {
            // existe possibilidade de extensão; se a última entrada foi recente, aguarde
            if (Time.time - lastInputTime < extensionWindow)
            {
                return null; // adia a confirmação para próxima frame/entrada
            }
        }

        return best;
    }

    // encontra o nó que corresponde ao match final (navega desde a raiz seguindo os últimos 'length' tokens do buffer)
    private ComboNode FindNodeForMatch(string comboId, int length)
    {
        // percorre os últimos 'length' tokens do buffer e anda na trie
        int startIndex = buffer.Count - length;
        if (startIndex < 0) return null;
        var node = root;
        for (int i = startIndex; i < buffer.Count; i++)
        {
            var token = buffer[i].token;
            if (!node.children.TryGetValue(token, out var child)) return null;
            node = child;
        }
        // valida se o nó realmente representa comboId
        if (node.isEnd && node.comboId == comboId) return node;
        return null;
    }

    // DFS_Search explores trie following buffer tokens from position 'pos' forward.
    // We keep track of lastTime (time of previous consumed token) to enforce maxDeltaBetweenInputs.
    private void DFS_Search(ComboNode node, int pos, float lastTime, int consumed, ref (string id, int length)? best, int startPos)
    {
        if (pos >= buffer.Count) return;

        string token = buffer[pos].token;

        if (node.children.TryGetValue(token, out var child))
        {
            float currentTime = buffer[pos].time;
            if (consumed == 0 || lastTime < 0f || (currentTime - lastTime) <= maxDeltaBetweenInputs)
            {
                int newConsumed = consumed + 1;
                if (child.isEnd && pos == buffer.Count - 1)
                {
                    var candidate = (id: child.comboId, length: newConsumed);
                    // preferimos sempre o mais longo; se empate, mantém o primeiro encontrado
                    if (best == null || candidate.length > best.Value.length) best = candidate;
                }
                DFS_Search(child, pos + 1, currentTime, newConsumed, ref best, startPos);
            }
        }
    }

    private int GetMaxComboLength()
    {
        int max = 0;
        foreach (var c in combosList) if (c.seq.Length > max) max = c.seq.Length;
        return max;
    }

    // -------- On match --------
    private void OnComboMatched((string id, int length) match)
    {
        // cooldown global para evitar múltiplos disparos
        if (Time.time - lastMatchTime < matchCooldown) return;
        lastMatchTime = Time.time;

        Debug.Log($"Combo matched: {match.id}");

        // mostrar informação amigável no MatchedText
        ShowComboFriendly(match.id);

        // flash do cubo
        if (playerRenderer != null && instanceMaterial != null)
        {
            CancelInvoke(nameof(ResetCubeColor));
            instanceMaterial.color = comboInfo.ContainsKey(match.id) ? comboInfo[match.id].color : flashColor;
            Invoke(nameof(ResetCubeColor), flashDuration);
        }

        // spawn floating text (apenas ID curto)
        StartCoroutine(SpawnFloatingText(match.id + "!"));

        // remover apenas as entradas usadas (do final) — não limpar o buffer inteiro por padrão
        int removeCount = match.length;
        for (int i = 0; i < removeCount && buffer.Count >= removeCount; i++)
            buffer.RemoveAt(buffer.Count - removeCount);

        // opcional: limpar todo o buffer para evitar sobreposição (se o usuário explicitamente ativar)
        if (clearBufferOnMatch) buffer.Clear();
    }

    private void ResetCubeColor()
    {
        if (instanceMaterial != null) instanceMaterial.color = originalColor;
    }

    // -------- UI helpers --------
    private void UpdateUI()
    {
        SetBufferText(BufferToFriendlyString());
    }

    private void SetBufferText(string s)
    {
        if (bufferTextUI != null) bufferTextUI.text = s;
        else if (bufferTextMesh != null) bufferTextMesh.text = s;
    }

    private void SetMatchedText(string s)
    {
        if (matchedTextUI != null) matchedTextUI.text = s;
        else if (matchedTextMesh != null) matchedTextMesh.text = s;
    }

    private string BufferToFriendlyString()
    {
        var tokens = GetTokenListFromBuffer();
        if (tokens.Count == 0) return "Entradas: —";
        return "Entradas: " + string.Join(" → ", tokens);
    }

    private List<string> GetTokenListFromBuffer()
    {
        var list = new List<string>();
        for (int i = 0; i < buffer.Count; i++) list.Add(buffer[i].token);
        return list;
    }

    private void ShowComboFriendly(string comboId)
    {
        if (comboInfo.TryGetValue(comboId, out var info))
        {
            SetMatchedText($"{info.title}\n{info.desc}");
            if (matchedTextUI != null) matchedTextUI.color = info.color;
            if (matchedTextMesh != null) matchedTextMesh.color = info.color;
        }
        else
        {
            SetMatchedText($"Combo: {comboId}");
        }
        CancelInvoke(nameof(ClearMatchedText));
        Invoke(nameof(ClearMatchedText), 0.9f);
    }

    private void ClearMatchedText()
    {
        SetMatchedText("");
    }

    // -------- Floating Text (TextMesh runtime) --------
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