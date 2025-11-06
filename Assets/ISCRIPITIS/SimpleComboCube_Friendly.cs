using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SimpleComboCube_Friendly (versão final e segura)
/// ------------------------------------------------------------
/// - Implementa DFS (Depth First Search) para detecção de combos.
/// - Usa matriz de adjacência para representar conexões de tokens.
/// - Protege contra erros de índice e concorrência de coroutines.
/// - Mantém compatibilidade total com UI (Canvas/TextMesh).
/// ------------------------------------------------------------
/// </summary>
public class SimpleComboCube_Friendly : MonoBehaviour
{
    [Header("Referências de UI")]
    public GameObject bufferTextGO; // GameObject que exibe o buffer de inputs
    public GameObject matchedTextGO; // GameObject que exibe o combo detectado

    private UnityEngine.UI.Text bufferTextUI; // Componente UI Text do buffer
    private UnityEngine.UI.Text matchedTextUI; // Componente UI Text do combo detectado
    private TextMesh bufferTextMesh; // Alternativa TextMesh para buffer
    private TextMesh matchedTextMesh; // Alternativa TextMesh para combo detectado

    [Header("Cubo visual")]
    public Renderer playerRenderer; // Renderer do cubo para efeito de flash
    public Color flashColor = Color.red; // Cor de flash padrão
    public float flashDuration = 0.35f; // Duração do flash em segundos

    [Header("Floating Text (TextMesh)")]
    public float floatingTextRise = 0.9f; // Distância que o texto sobe
    public float floatingTextLife = 1.0f; // Tempo de vida do texto
    public Color floatingTextColor = Color.yellow; // Cor do texto flutuante
    public int floatingFontSize = 48; // Tamanho da fonte do texto

    [Header("Configuração de input")]
    [Tooltip("Tempo máximo que um input fica no buffer")]
    public float inputRetention = 2.0f; // Tempo máximo que um token fica registrado no buffer

    [Tooltip("Tempo máximo entre inputs consecutivos de um combo")]
    public float maxDeltaBetweenInputs = 0.65f; // Intervalo máximo entre dois inputs consecutivos para formar combo

    [Header("Detecção e comportamento")]
    public float matchCooldown = 0.45f; // Cooldown entre detecção de combos
    public bool clearBufferOnMatch = false; // Limpa todo o buffer após detectar combo

    [Tooltip("Tempo de espera para confirmar se o combo pode ser estendido")]
    public float extensionWindow = 0.25f; // Janela de extensão para combos

    private float lastMatchTime = -10f; // Guarda o tempo do último combo detectado

    // Estrutura auxiliar para armazenar inputs e tempos
    private struct Timed
    {
        public string token; // Nome do token
        public float time; // Timestamp do input
        public Timed(string t, float tm) { token = t; time = tm; }
    }
    private readonly List<Timed> buffer = new List<Timed>(); // Buffer de inputs do jogador

    // Lista de combos declarados
    private readonly List<(string id, string[] seq)> combosList = new List<(string, string[])>
    {
        ("Ruptura",         new [] { "Right", "LP" }),
        ("Carga Avançada",  new [] { "Down", "Right", "LP" }),
        ("Quebra de Guarda",new [] { "Down", "Right", "HP" }),
        ("Colosso",         new [] { "Down", "Right", "LP", "HP" }),
        ("Perfuração",      new [] { "Right", "LP", "Right", "HP" }),
        ("Dobrador",        new [] { "Down", "Down", "Right", "LP" })
    };

    // Informações detalhadas de combos (nome, descrição, cor para UI)
    private readonly Dictionary<string, (string title, string desc, Color color)> comboInfo = new Dictionary<string, (string, string, Color)>
    {
        { "Ruptura", ("Ruptura", "Pressão rápida — 60 dmg", Color.green) },
        { "Carga Avançada", ("Carga Avançada", "120 dmg", Color.cyan) },
        { "Quebra de Guarda", ("Quebra de Guarda", "Atordoamento", Color.red) },
        { "Colosso", ("Colosso", "Ataque pesado — alto dano", new Color(0.9f, 0.45f, 0.1f)) },
        { "Perfuração", ("Perfuração", "Sequência técnica — perfura guarda", new Color(0.6f, 0.2f, 0.8f)) },
        { "Dobrador", ("Dobrador", "Carga dupla — empurrão", new Color(0.2f, 0.7f, 0.2f)) }
    };

    // ------------------------------------------------------------
    // MATRIZ DE ADJACÊNCIA
    // ------------------------------------------------------------
    private List<string> tokenList = new List<string> { "Down", "Right", "LP", "HP" }; // Lista de tokens possíveis
    private bool[,] adjacencyMatrix; // Matriz que indica conexões válidas entre tokens
    private Dictionary<string, int> tokenIndex; // Mapeia tokens para índices da matriz

    private Color originalColor; // Cor original do cubo
    private Material instanceMaterial; // Material instanciado para manipulação segura

    void Start()
    {
        // Inicializa componentes UI
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

        // Inicializa material do cubo para efeitos visuais
        if (playerRenderer != null)
        {
            instanceMaterial = new Material(playerRenderer.sharedMaterial); // Cria instância para não modificar material compartilhado
            playerRenderer.material = instanceMaterial;
            originalColor = instanceMaterial.color; // Armazena cor original
        }

        BuildAdjacencyMatrix(); // Constroi a matriz de adjacência baseada nos combos

        SetBufferText("Entradas: —"); // Inicializa UI
        SetMatchedText("");
    }

    void Update()
    {
        // Captura inputs do jogador
        if (Input.GetKeyDown(KeyCode.U)) AddInput("Down");
        if (Input.GetKeyDown(KeyCode.I)) AddInput("Right");
        if (Input.GetKeyDown(KeyCode.J)) AddInput("LP");
        if (Input.GetKeyDown(KeyCode.K)) AddInput("HP");

        TrimBuffer(); // Remove inputs antigos do buffer
        UpdateUI(); // Atualiza interface de buffer

        var match = TryMatchCombo_DFS(); // Tenta detectar combo
        if (match != null) OnComboMatched(match.Value); // Se encontrou, processa combo
    }

    // ------------------------------------------------------------
    // MATRIZ DE ADJACÊNCIA
    // ------------------------------------------------------------
    private void BuildAdjacencyMatrix()
    {
        int n = tokenList.Count;
        adjacencyMatrix = new bool[n, n]; // Inicializa matriz n x n
        tokenIndex = new Dictionary<string, int>();

        // Associa cada token a um índice
        for (int i = 0; i < tokenList.Count; i++)
            tokenIndex[tokenList[i]] = i;

        // Inicializa matriz com false
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                adjacencyMatrix[i, j] = false;

        // Preenche matriz com conexões válidas baseadas na lista de combos
        foreach (var combo in combosList)
        {
            var seq = combo.seq;
            for (int i = 0; i < seq.Length - 1; i++)
            {
                int from = tokenIndex[seq[i]];
                int to = tokenIndex[seq[i + 1]];
                adjacencyMatrix[from, to] = true; // Conexão válida
            }
        }
    }

    // ------------------------------------------------------------
    // BUFFER DE INPUTS
    // ------------------------------------------------------------
    private void AddInput(string token)
    {
        buffer.Add(new Timed(token, Time.time)); // Adiciona input com timestamp
        Debug.Log($"[INPUT] Token recebido: {token} (t={Time.time:F2})");
        TrimBuffer(); // Remove inputs antigos para manter buffer limpo
    }

    private void TrimBuffer()
    {
        float now = Time.time;
        for (int i = 0; i < buffer.Count;)
        {
            if (now - buffer[i].time > inputRetention)
                buffer.RemoveAt(i); // Remove input antigo
            else i++;
        }
    }

    // ------------------------------------------------------------
    // DETECÇÃO DE COMBOS COM DFS (versão segura)
    // ------------------------------------------------------------
    private (string id, int length)? TryMatchCombo_DFS()
    {
        if (buffer.Count == 0) return null;
        (string id, int length)? best = null;

        // Verifica cada combo declarado na lista
        foreach (var combo in combosList)
        {
            if (CheckComboDFS(combo.seq))
            {
                if (best == null || combo.seq.Length > best.Value.length)
                    best = (combo.id, combo.seq.Length); // Escolhe maior combo possível
            }
        }

        // Se um combo foi encontrado, aguarda janela de extensão
        if (best != null && best.Value.length < MaxComboLength())
        {
            StopCoroutine(nameof(ConfirmComboWithDelay)); // Cancela coroutine antiga
            StartCoroutine(ConfirmComboWithDelay(best.Value)); // Inicia confirmação segura
            return null;
        }

        return best;
    }

    // ------------------------------------------------------------
    // Coroutine segura com verificação de buffer
    // ------------------------------------------------------------
    private IEnumerator ConfirmComboWithDelay((string id, int length) combo)
    {
        float wait = extensionWindow;
        yield return new WaitForSeconds(wait); // Espera janela de extensão

        // Verifica se buffer ainda existe e contém elementos
        if (buffer == null || buffer.Count == 0)
            yield break;

        float lastInputTime = buffer[buffer.Count - 1].time;

        // Só confirma se não houve novos inputs durante a espera
        if (Time.time - lastInputTime >= wait)
        {
            Debug.Log($"[CONFIRMAÇÃO] Combo '{combo.id}' confirmado após {wait:F2}s de extensão.");
            OnComboMatched(combo);
        }
    }

    private int MaxComboLength()
    {
        int max = 0;
        foreach (var c in combosList)
            if (c.seq.Length > max) max = c.seq.Length;
        return max;
    }

    private bool CheckComboDFS(string[] comboSeq)
    {
        if (buffer.Count < comboSeq.Length) return false;

        int startIndex = buffer.Count - comboSeq.Length;

        for (int i = 0; i < comboSeq.Length; i++)
        {
            string current = buffer[startIndex + i].token;
            if (current != comboSeq[i]) return false;

            if (i > 0)
            {
                float delta = buffer[startIndex + i].time - buffer[startIndex + i - 1].time;
                if (delta > maxDeltaBetweenInputs) return false; // Input muito lento

                int prevIndex = tokenIndex[comboSeq[i - 1]];
                int currIndex = tokenIndex[comboSeq[i]];
                if (!adjacencyMatrix[prevIndex, currIndex]) return false; // Conexão inválida
            }
        }

        return true; // Combo válido
    }

    // ------------------------------------------------------------
    // COMBO DETECTADO / UI / VISUAL
    // ------------------------------------------------------------
    private void OnComboMatched((string id, int length) match)
    {
        if (Time.time - lastMatchTime < matchCooldown) return; // Evita spam de combos
        lastMatchTime = Time.time;

        Debug.Log($"[COMBO DETECTADO] {match.id} (t={Time.time:F2})");

        ShowComboFriendly(match.id); // Atualiza UI com combo

        if (playerRenderer != null && instanceMaterial != null)
        {
            CancelInvoke(nameof(ResetCubeColor)); // Cancela qualquer reset pendente
            instanceMaterial.color = comboInfo.ContainsKey(match.id) ? comboInfo[match.id].color : flashColor;
            Invoke(nameof(ResetCubeColor), flashDuration); // Reseta cor após flash
        }

        StartCoroutine(SpawnFloatingText(match.id + "!")); // Mostra texto flutuante

        // Remove tokens usados do buffer
        for (int i = 0; i < match.length && buffer.Count > 0; i++)
            buffer.RemoveAt(buffer.Count - 1);

        if (clearBufferOnMatch) buffer.Clear(); // Limpa buffer inteiro se configurado
    }

    private void ResetCubeColor()
    {
        if (instanceMaterial != null) instanceMaterial.color = originalColor; // Restaura cor original
    }

    private void UpdateUI()
    {
        SetBufferText(BufferToString()); // Atualiza UI do buffer
    }

    private string BufferToString()
    {
        if (buffer.Count == 0) return "Entradas: —";
        List<string> tokens = new List<string>();
        foreach (var b in buffer) tokens.Add(b.token);
        return "Entradas: " + string.Join(" → ", tokens);
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

        CancelInvoke(nameof(ClearMatchedText)); // Cancela limpeza anterior
        Invoke(nameof(ClearMatchedText), 0.9f); // Limpa texto após tempo
    }

    private void ClearMatchedText()
    {
        SetMatchedText("");
    }

    private IEnumerator SpawnFloatingText(string text)
    {
        GameObject go = new GameObject("FloatingText"); // Cria objeto do texto flutuante
        go.transform.position = transform.position + Vector3.up * 1.2f; // Posiciona acima do cubo
        if (Camera.main) go.transform.rotation = Camera.main.transform.rotation; // Alinha com câmera

        var tm = go.AddComponent<TextMesh>(); // Adiciona TextMesh
        tm.text = text;
        tm.fontSize = floatingFontSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = floatingTextColor;
        go.transform.localScale = Vector3.one * 0.02f; // Escala adequada

        float t = 0f;
        Vector3 startPos = go.transform.position;
        while (t < floatingTextLife)
        {
            t += Time.deltaTime;
            float norm = t / floatingTextLife;
            go.transform.position = startPos + Vector3.up * (floatingTextRise * norm); // Move texto para cima
            Color c = tm.color; c.a = Mathf.Lerp(1f, 0f, norm); tm.color = c; // Fade out
            if (Camera.main) go.transform.rotation = Camera.main.transform.rotation; // Mantém alinhamento com câmera
            yield return null;
        }
        Destroy(go); // Destroi objeto ao final
    }
}
