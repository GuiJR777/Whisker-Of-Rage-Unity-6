# ADR-0004 — Command buffers por domínio: Player e AI compartilham execução

- **Status:** Aceita (M0) · **Revisada** na revisão do M0: ciclo de vida de comandos explicitado
- **Relacionada:** [ADR-0003](0003-execution-order-bands.md) (bandas e loops)

## Contexto
O Master Prompt exige que Player e AI usem os mesmos sistemas de execução, e que
"Behaviour Tree decide; HFSM executa". Se estados lerem input diretamente, jogador e AI não podem compartilhar
a mesma execução. Além disso, comandos são produzidos em `Update` e muitas vezes consumidos em `FixedUpdate`,
que roda 0..N vezes por frame (ADR-0003): sem um contrato, inputs se perdem ou executam duas vezes.

## Decisão
1. Cada domínio executável expõe um **command buffer** (componente) com comandos tipados:
   `CharacterCommandBuffer`, `CombatCommandBuffer`, `AbilityCommandBuffer`.
2. **Fontes de comando** escrevem no buffer: adapter de Input System (jogador), nós de ação da BT (AI),
   scripts de cutscene/teste. **Executores** (estados da HFSM, motor, combo runner) só leem. Ninguém sabe quem
   está do outro lado.
3. Há **dois tipos de comando**, com regras diferentes:

| | Comando de **estado** (nível) | Comando de **borda** (evento) |
|---|---|---|
| Exemplos | Direção de movimento, defender segurado, mira | Pular, atacar (ação X), dash, usar ability Y |
| Escrita | A fonte sobrescreve o valor a cada amostra | A fonte **enfileira** uma ocorrência com número de sequência e instante |
| Leitura | Qualquer executor lê o último valor; ler não consome | Um único dono consome com `TryConsume`; outros podem apenas espiar (`HasPending`) |
| Vida útil | Até a próxima amostra; volta ao neutro se a fonte for desligada | Até ser consumido **ou** expirar (idade > janela definida pelo domínio consumidor) |

## Garantias de entrega
1. **Sem perda entre ticks:** uma borda fica *latched* no buffer até o dono consumi-la. Se um frame não tiver
   `FixedUpdate`, ela é consumida no próximo passo fixo.
2. **Sem duplicação:** consumir remove a ocorrência. Se o frame tiver vários `FixedUpdate`, só o primeiro consumo
   a recebe; os passos seguintes não a veem.
3. **Sem execução tardia:** bordas mais velhas que a janela do domínio (ex.: jump buffer, input buffer de combo)
   são descartadas sem executar. A janela pertence ao consumidor (M3/M4), não ao buffer.
4. **Um dono por tipo de borda.** O dono de cada comando é declarado no `CONTRACTS.md` do package.
   Guards da HFSM **espiam** (`HasPending`); o estado que entra **consome**. Isso evita transição dupla.
5. **Troca de controle limpa:** ao trocar a fonte (jogador → AI, cutscene), o buffer é limpo (`Clear`).
6. **Sem alocação:** bordas ficam em um ring buffer de capacidade fixa. Overflow descarta a ocorrência mais antiga
   e é contado para o debugger.

## Regras para fontes de comando
- Amostrar uma vez por frame, na banda `COMMAND_SOURCES` (`Update`).
- Input System: enfileirar bordas pelo callback `performed` da action ou com `WasPressedThisFrame()` **somente em
  `Update`**. Nunca consultar `WasPressedThisFrame()` em `FixedUpdate` (perde ou duplica input).
- AI escreve na banda `DECISION` (`Update`), no intervalo de avaliação da BT.

## Latência
Uma borda produzida no `Update` do frame N é consumida no primeiro passo fixo do frame N+1
(ou mais tarde no frame N, se o executor tickar em `Update` numa banda posterior). Isso é aceito até M3/M4
medirem impacto em game feel.

## Decisões adiadas
| Milestone | Decisão |
|---|---|
| M3 | Implementação do primeiro buffer (`CharacterCommandBuffer`); capacidade; janela do jump buffer. Se Combat (M4) precisar da mesma estrutura, o tipo genérico de borda/ring buffer vai para o Core (regra de ≥ 2 consumidores + ADR). |
| M4 | Janela do input buffer de combo; prioridade entre bordas simultâneas (ataque × defesa × grab). |
| M5 | Ativação de abilities por borda; interação de cancelamento com bordas pendentes. |

## Alternativas
- **Interface `IInputProvider` consultada pelos estados:** funciona, mas incentiva lógica de decisão dentro da
  execução e dificulta gravar/reproduzir comandos.
- **BT chamando estados da HFSM diretamente:** acopla AI à HFSM e impede o jogador de usar a mesma HFSM.
- **Limpar bordas a cada `Update`:** simples, mas perde input em frames sem `FixedUpdate`.

## Consequências
- Mesmo prefab pode ser controlado por jogador ou AI trocando só a fonte de comando.
- AI não depende da HFSM no grafo.
- Possível gravação/replay de comandos para testes.
- Cada buffer precisa de testes para as garantias 1–6 (perda, duplicação, expiração, dono único, clear, overflow).
