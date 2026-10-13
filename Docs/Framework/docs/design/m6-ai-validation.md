# M6 — Evidências de implementação do AI

- **Especificação:** [m6-ai-design](m6-ai-design.md) v2 (WOR `f606142`, ajustes autorizados em `f0595be`); ADRs
  0026–0032.
- **Unity:** 6000.6.5f1 · **Package:** `com.ramirestechgames.ai` 0.1.0 (sem tag), repositório privado
  `GuiJR777/com.ramirestechgames.ai`, submodule do host.
- **Packages existentes:** nenhum alterado (Core 0.4.0, Stats 0.1.1, HFSM 0.1.0, Character 0.2.0, Combat 0.1.0,
  Abilities 0.1.0 nos commits fixados no WOR).

## Entrega 1 — fundação da BT e do Blackboard (AI `94bf5f4`)

| Gate | Resultado |
|---|---|
| Compilação | 0 erros, 0 warnings |
| EditMode (AI) | 59/59 — ciclo de vida, `Parallel` com vários ramos e conflitos, aborts (ordem, um por tick, sem cascata, dentro de `Parallel`), orçamento (parada consistente, nó iniciado sem tick abortado), canais, services, decorators em 4 Δt, utility, random, blackboard, validators |
| PlayMode (AI) | 7/7 — escalonamento por identidade, relógio, escrita externa na avaliação seguinte, evento, pause/resume, disable, identidade inválida, ordem de criação embaralhada |
| Zero GC | `TreeAllocationTests` (árvore com `Parallel`, aborts `Both`, services, `Repeat`, `Cooldown`, `TimeLimit`, utility, random) |
| QG12 (só Core) | 66 testes (59 + 7), 0 ignorados, 0 mensagens do compilador |
| `check_dependencies` | OK (AI incluído) |

Desvios: nenhum de contrato. Precisões de implementação registradas no CONTRACTS do package: subárvore sem filhos
falha; `Parallel` com política `Unset` vale `RequireAll` em runtime (erro de validação); `Restart` recria memórias
(aloca), `Resume` mantém.

## Entrega 2 — percepção e alvo (AI `eabad50`)

| Gate | Resultado |
|---|---|
| Compilação | 0 erros, 0 warnings |
| EditMode (AI) | 67/67 — memória (top-K em 6 ordens, esquecimento, saída do mundo, atraso, ameaça, zero GC), caixa de estímulos (top-K × entrega em 5 ordens), validators de perfil/seleção/nós |
| PlayMode (AI) | 24/24 — registro (duplicado, zero, cheio, clones `Combine`, handles velhos e de outro mundo, ordem do snapshot), visão (cone, alcance, time, parede, detectabilidade 0), audição em *n + 1*, estímulo dirigido, caixa com intervalo 5, saturação do mundo, esquecimento, seleção (histerese, empate), invariância com 3 ordens de criação (15 atores, 12 passos) |
| Zero GC | passo completo com 20 agentes e 20 percebíveis (P0, estímulos, 4 sensores, memória, seleção, árvore) |
| QG12 (só Core) | 91 testes (67 + 24), 0 ignorados, 0 mensagens do compilador |
| `check_dependencies` | OK |

Desvios e precisões: (1) num agente o `AIAgent` registra o percebível depois de resolver o mundo (o `OnEnable` do
percebível não registra), porque durante a ativação a ordem de `OnEnable` entre componentes não garante o mundo;
(2) `IsSensedNow` significa "percebido na última atualização": um sensor com intervalo maior que a avaliação não
percebe em todas; (3) linha de visão depende de colliders sincronizados pela física (testes chamam
`Physics.SyncTransforms` ao criar paredes); (4) dano de fonte não registrada é ignorado pela memória (a seleção só usa
entidades registradas).

## Entrega 3 — movimento e engajamento (AI `d4eb189`)

| Gate | Resultado |
|---|---|
| Compilação | 0 erros, 0 warnings |
| EditMode (AI) | 68/68 — inclui validators de perfil de engajamento e de nós de movimento/escopo |
| PlayMode (AI) | 38/38 — chegada, `AlignDepthFirst`, desvio de parede, travamento, separação, patrulha, distância/fuga/órbita, facing em `Parallel`, pulo/dash rastreados; engajamento com 6 agentes e 2 tokens (máximo 2 detentores em 400 passos, token retido em todos os ticks do filho, vagas distintas, liberação única por concessão), interrupção, troca de alvo, handle velho, agente desabilitado, revogação por tempo, mesma sequência de concessões em 3 ordens de criação |
| Zero GC | 12 agentes com escopo, movimento até a vaga, desvio, separação, órbita e revogação |
| QG12 (só Core) | 106 testes (68 + 38), 0 ignorados, 0 mensagens do compilador |
| `check_dependencies` | OK |

Desvios e precisões: (1) a liberação do token é imediata (síncrona) — concessões continuam só na fase +60, então o
resultado não depende da ordem dos agentes; isso permite reativar o escopo no mesmo tick sem conflito; (2) o lado de
desvio de obstáculo é mantido pelo nó enquanto a sonda acerta (sem isso o agente oscilava no eixo da parede — achado
pelo teste); (3) com score = −distância, os agentes mais próximos recuperam o token: a rotação entre atacantes é
configuração da árvore (ex.: `Cooldown` no escopo), não regra do package.
