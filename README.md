# Unity DFS Combo Simulator

Um pequeno projeto em **Unity 6.2** que demonstra **DFS (Depth-First Search)** aplicado à detecção de combos em um cubo.  
Ideal para apresentação ou estudo de algoritmos em jogos de luta.

---

## 📦 O que o projeto faz

- Um **cubo** reage a entradas de teclas simulando **combos de ataque**.
- Mostra:
  - **Buffer de entradas** (o que você digitou até agora)
  - **Combo detectado** (quando uma sequência válida é completada)
  - **Texto flutuante** e mudança de cor no cubo ao acertar o combo
- Usa **DFS em uma trie** para encontrar o combo mais longo possível com base nas entradas recentes.
- Respeita limites de tempo entre entradas (`maxDeltaBetweenInputs`) e mantém uma **janela de extensão** para combos mais longos.

---

## 🎮 Como usar

### Teclas de entrada

| Tecla | Input |
|-------|-------|
| `U`   | Down  |
| `I`   | Right |
| `J`   | LP    |
| `K`   | HP    |

> As combinações devem ser feitas rapidamente, respeitando a janela de tempo definida (`maxDeltaBetweenInputs`).

### Combos disponíveis

| Combo            | Sequência de entradas | Efeito                         |
|-----------------|--------------------|--------------------------------|
| Ruptura          | Right + LP         | Pressão rápida — 60 dmg        |
| Carga Avançada   | Down + Right + LP  | 120 dmg                        |
| Quebra de Guarda | Down + Right + HP  | Atordoamento                   |
| Colosso          | Down + Right + LP + HP | Ataque pesado — alto dano |
| Perfuração       | Right + LP + Right + HP | Sequência técnica — perfura guarda |
| Dobrador         | Down + Down + Right + LP | Carga dupla — empurrão       |

---

## 🖌 Visual

- O cubo muda de cor conforme o combo detectado.
- Texto flutuante exibe o nome do combo.
- UI opcional: pode usar `Text` ou `TextMesh` para mostrar buffer e combo.

---

## ⚙ Configurações

- `inputRetention`: tempo máximo para manter entradas no buffer  
- `maxDeltaBetweenInputs`: intervalo máximo entre entradas consecutivas  
- `matchCooldown`: evita múltiplos disparos simultâneos  
- `extensionWindow`: aguarda possíveis extensões do combo  

- Pode alterar `floatingFontSize` para aumentar o texto flutuante do combo.

---

## 📝 Observações

- Feito apenas para demonstração de **DFS em algoritmos de detecção de combos**.
- Ideal para apresentações ou estudo de estruturas de dados e lógica de jogos.

---

## 🚀 Como rodar

1. Abra o projeto no Unity 6.2
2. Arraste o script `SimpleComboCube_Friendly.cs` para o cubo
3. Configure os `GameObjects` de UI (opcional)
4. Aperte **Play** e teste os combos!
