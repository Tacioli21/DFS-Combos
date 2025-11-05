using UnityEngine;

/// <summary>
/// MonoBehaviour exemplo que integra InputBuffer + ComboMatcher.
/// Coloque este arquivo em Assets/ISCRIPITIS/ComboDemoController.cs
/// Certifique-se de ter também TimedInput.cs, InputBuffer.cs, ComboNode.cs e ComboMatcher.cs no mesmo projeto.
/// </summary>
public class ComboDemoController : MonoBehaviour
{
    [Tooltip("Segundos para reter inputs no buffer")]
    public float inputRetention = 1.5f;

    private InputBuffer inputBuffer;
    private ComboMatcher matcher;
    private ComboNode comboRoot;

    void Start()
    {
        inputBuffer = new InputBuffer(inputRetention);
        BuildDemoCombos();
        matcher = new ComboMatcher(comboRoot);
    }

    void Update()
    {
        // Mapeamento de teclas de exemplo (troque pelos seus tokens/enums)
        if (Input.GetKeyDown(KeyCode.J)) RegisterInput("LP");           // Light Punch
        if (Input.GetKeyDown(KeyCode.K)) RegisterInput("HP");           // Heavy Punch
        if (Input.GetKeyDown(KeyCode.U)) RegisterInput("Down");
        if (Input.GetKeyDown(KeyCode.I)) RegisterInput("Right");
        if (Input.GetKeyDown(KeyCode.O)) RegisterInput("RightForward");

        // Se não houver inputs, não rodamos o matcher
        var snap = inputBuffer.GetSnapshot();
        if (snap.Length == 0) return;

        // Tenta achar o melhor combo usando DFS
        var result = matcher.FindBestMatch(snap);
        if (result.matched)
        {
            Debug.Log($"Matched combo {result.comboId}, length {result.length}");
            foreach (var ti in result.consumed)
                Debug.Log($"  {ti.input} @ {ti.time:F2}");

            // Consumir os inputs usados pelo combo (simples: remove primeiros N)
            ConsumeInputs(result.consumed.Length);
            // Aqui você pode também disparar animação / dano / mudar estado do personagem
        }
    }

    private void RegisterInput(string token)
    {
        float now = Time.time;
        inputBuffer.Add(token, now);
        Debug.Log($"Input {token} at {now:F2}");
    }

    private void ConsumeInputs(int count)
    {
        var snap = inputBuffer.GetSnapshot();
        if (count <= 0 || snap.Length == 0) return;
        if (count >= snap.Length)
        {
            // limpa tudo
            inputBuffer = new InputBuffer(inputRetention);
            return;
        }

        // recria buffer com os que sobraram
        var newBuf = new InputBuffer(inputRetention);
        for (int i = count; i < snap.Length; i++)
            newBuf.Add(snap[i].input, snap[i].time);
        inputBuffer = newBuf;
    }

    // Constrói alguns combos de exemplo na trie/graph
    private void BuildDemoCombos()
    {
        comboRoot = new ComboNode("root");

        // Exemplo: Down -> Right -> LP
        var n1 = new ComboNode("down");
        var n2 = new ComboNode("df");
        var end1 = new ComboNode("end_anker") { isComboEnd = true, comboId = "DRLP" };

        comboRoot.AddEdge("Down", n1, 0.4f);
        n1.AddEdge("Right", n2, 0.35f);
        n2.AddEdge("LP", end1, 0.25f);

        // Down -> Right -> HP
        var end2 = new ComboNode("end_strong") { isComboEnd = true, comboId = "DRHP" };
        n2.AddEdge("HP", end2, 0.25f);

        // Right -> LP
        var r1 = new ComboNode("right");
        var end3 = new ComboNode("end_simple") { isComboEnd = true, comboId = "RLP" };
        comboRoot.AddEdge("Right", r1, 0.35f);
        r1.AddEdge("LP", end3, 0.25f);

        // RightForward -> HP -> LP -> HP
        var rf = new ComboNode("rf");
        var rf2 = new ComboNode("rf_hp");
        var rf3 = new ComboNode("rf_hp_lp_end") { isComboEnd = true, comboId = "RF_HP_LP" };
        var rfEnd = new ComboNode("rf_full_end") { isComboEnd = true, comboId = "RF_HP_LP_HP" };

        comboRoot.AddEdge("RightForward", rf, 0.35f);
        rf.AddEdge("HP", rf2, 0.3f);
        rf2.AddEdge("LP", rf3, 0.25f);
        rf3.AddEdge("HP", rfEnd, 0.2f);
    }
}