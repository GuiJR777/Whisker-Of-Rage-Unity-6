# ADR-0013 — Tempo de combate: segundos de jogo, Δt fixo e janelas por sobreposição

- **Status:** Proposta (design do M4) · **Milestone:** M4
- **Relacionada:** [ADR-0003](0003-execution-order-bands.md), [ADR-0012](0012-character-timing.md), risco R8,
  [especificação do M4](../design/m4-combat-design.md) §5

## Contexto
Ataques têm fases e janelas curtas (poucos frames). O risco R8 aponta o conflito entre hitboxes no `FixedUpdate`,
animação no `Update`, hitstop e a escolha entre frames e segundos. O projeto define o Δt fixo (host 0,02 s) e o
framework não o altera (ADR-0012). Com Δt de 0,02 s, uma janela de 3 frames a 60 fps (0,05 s) ocupa 2,5 passos.

## Decisão
1. Linhas do tempo de combate em **segundos de tempo de jogo**, acumulando o Δt fixo; nunca dirigidas pelo Animator.
2. **Regra de janela:** `[a, b)` está ativa no passo `[t, t + Δt)` se os intervalos se sobrepõem. Nenhuma janela de
   duração > 0 é pulada em nenhum Δt; a duração em passos é `⌈(b−a)/Δt⌉` ou `+1`.
3. Autoria em segundos; o inspector mostra frames de referência (60 fps) e passos no Δt atual.
4. Hitstop pausa as linhas do tempo de combate do ator (não o relógio global).
5. No passo: timeline → novas ações/cancelamentos → detecção. Uma ação aceita só fica ativa a partir do passo seguinte.

## Alternativas consideradas
- **Ticks fixos inteiros:** o mesmo asset mudaria de duração com o Δt do projeto.
- **Frames @60 em runtime:** arredondamento diferente por Δt; janelas curtas poderiam sumir com Δt grande.
- **Animation events:** acoplam simulação à animação (R8) e não rodam em ordem de banda.

## Consequências
- Positivas: o mesmo ataque funciona em qualquer Δt e `timeScale`; testável em EditMode sem Animator.
- Negativas / custos: janelas podem durar um passo a mais que o nominal (documentado e testado).
- CONTRACTS do Combat descreve a regra; testes em Δt 0,02 / 1/60 / 0,01 / 0,0333.
