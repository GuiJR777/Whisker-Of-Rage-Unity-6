# ADR-0019 — Execução de habilidades: passo fixo, duas sub-bandas, caixa de entrada e hitstop

- **Status:** Aceita (revisão do design do M5, 2026-10-12) · **Milestone:** M5
- **Relacionada:** [ADR-0003](0003-execution-order-bands.md), [ADR-0013](0013-combat-timing.md),
  [ADR-0014](0014-two-phase-hit-resolution.md), [especificação do M5](../design/m5-abilities-design.md) §3.3, §13

## Contexto
Habilidades aplicam efeitos a outros atores, criam projéteis e interagem com o Combat, que já é determinístico por
passo fixo e independente da ordem entre atores. Se um ator aplicasse efeitos diretamente em outro durante o próprio
tick, o resultado (stacks, tags, ativações bloqueadas) dependeria da ordem de `FixedUpdate` entre instâncias, que a
Unity não garante (ADR-0003).

## Decisão
1. O `AbilitySystemComponent` roda no `FixedUpdate`, banda `ABILITIES` (−700), em duas sub-bandas:
   **A1 (+0)** — pedidos, validação, commit, avanço das execuções e ações do passo (efeitos para outros atores vão para
   a caixa de entrada deles; projéteis e áreas são criados); **A2 (+50)** — cada ator aplica a própria caixa em ordem
   total (instigador, sequência), avança efeitos periódicos/expiração e entrega eventos FIFO.
2. Pedidos gerados em A2 (efeitos condicionais, listeners) entram na caixa do passo seguinte.
3. Tempo em segundos de jogo acumulando Δt fixo; ações em τ e janelas `[a, b)` pela regra de sobreposição do
   ADR-0013 (nenhuma pulada em Δt diferentes).
4. Hitstop: a linha do tempo das execuções pausa quando o ator tem `IAbilityTimeSource` em pausa (padrão por
   habilidade); cooldowns, recargas e durações/períodos de efeitos não pausam (padrão; configurável).
5. Como `ABILITIES` precede `COMBAT`, modificadores aplicados em A2 valem para o Combat do mesmo passo; efeitos pedidos
   pelo Combat (`IHitEffect`) são aplicados em A2 do passo seguinte.

## Alternativas consideradas
- **Aplicação imediata entre atores:** resultados dependentes de ordem (como o problema resolvido no ADR-0014).
- **Coordenador global (singleton) de habilidades:** acoplamento e estado estático; contrário ao padrão de componentes.
- **Relógio no `Update`:** quebra a equivalência entre Δt e a integração com Combat/Character.

## Consequências
- Positivas: determinismo e integração natural com Combat e Character; mesma regra temporal em todo o framework.
- Negativas / custos: um passo de atraso para efeitos entre atores; componente de fase oculto (como no Combat).

## Ajustes da revisão
Aceita com os ajustes 1 e 3 da revisão (§25.1 e §25.3 da especificação), que substituem os itens 1 e 5 da decisão:

1. **Três sub-bandas** com componentes de fase ocultos (sem singleton): **A0** (`ABILITIES + 0`) — cada ASC publica o
   snapshot do início do passo (tags possuídas, vivo/habilitado, identidade, time, pose e facing) e avança cooldowns;
   **A1** (`+30`) — pedidos, validação, commit, execuções, ações, targeting e projéteis; **A2** (`+60`) — fila de
   mutações do mundo do próprio ator, caixa de entrada, efeitos periódicos/expiração, tags e eventos.
2. Em A1, dados de outros atores vêm **só do snapshot A0**; o mundo é somente-leitura: teleporte, spawn com colliders e
   suspensão são diferidos para A2 do próprio ator. Projéteis criados em A1 de *n* começam a mover em *n + 1*.
3. **Ordem total** da caixa de entrada: (canal, emissor, sequência do emissor, índice); emissor = `AbilityActorId`
   (64 bits) ou `ProjectileShotId`; canal `Combat` usa a chave do Combat (solicitante, ordem). **Saturação
   determinística:** mantém as K menores chaves (top-K) e conta os descartes — nunca "os primeiros N" por ordem de
   `FixedUpdate`.
4. **Latência do Combat:** efeitos de `IHitEffect` (fase 4 de *n*) passam pelo sink, são aplicados pelo Combat na fase 4
   de *n + 1* (colocando o `EffectSpec` na caixa) e valem em **A2 de *n + 2***. A escrita direta na caixa (*n + 1*)
   não é adotada: exige ADR e emenda do contrato do `IHitEffect`. Interrupções e Gameplay Events vindos de eventos do
   Combat (fim da fase 4 de *n*) valem em **A1 de *n + 1***.
5. A garantia é de **equivalência lógica** (mesmas decisões dados os mesmos comandos e resultados de consulta),
   independente da ordem entre atores e do Δt; não se promete determinismo bit a bit do PhysX.
6. Testes com ordem de criação/execução embaralhada: efeitos cruzados, tags bloqueadoras simultâneas, stacking e
   saturação.
