# ADR-0021 — Custos de habilidade: verificação, reserva, commit e reembolso

- **Status:** Aceita (revisão do design do M5, 2026-10-12) · **Milestone:** M5
- **Relacionada:** especificação do M1 (custos atômicos do Stats), [especificação do M5](../design/m5-abilities-design.md) §6

## Contexto
Habilidades podem debitar custos na ativação, num instante posterior (quando o efeito "sai") ou na primeira entrega,
e devolver custos quando canceladas ou interrompidas. O Stats oferece verificação e consumo atômicos
(`CanAfford`, `TryConsumeResources`) e ganho (`Increase`), mas não reserva: outro sistema (Combat, script) pode gastar o
recurso entre a validação e o débito.

## Decisão
1. Validação usa `CanAfford(custos + reservas do ator)`.
2. Execuções com commit posterior registram o custo num **livro de reservas** do `AbilitySystemComponent`; outras
   habilidades do mesmo ator enxergam o recurso menos as reservas. A reserva não trava o recurso no Stats.
3. Commit (`OnActivate`, `AtTime`, `OnFirstDelivery`) debita atomicamente; se falhar, a execução termina como
   `Cancelled(CommitFailed)` sem efeitos posteriores e libera a reserva.
4. Reembolso por política (`None`, `OnCancelBeforeDelivery`, `OnInterrupt` com fração, `OnNoTarget`), via `Increase`,
   nunca acima do debitado; cooldown e cargas seguem a mesma política (configurável).
5. Reserva real no Stats (respeitada por todos os sistemas) fica como mudança opcional, com ADR própria, se o M5
   mostrar necessidade.

## Alternativas consideradas
- **Reserva real no Stats agora:** mudança em package L1 aprovado sem necessidade demonstrada.
- **Sem reserva (débito sempre na ativação):** impede custos "quando o efeito sai" e reembolsos naturais.

## Consequências
- Positivas: sem mudança no Stats; políticas explícitas e testáveis.
- Negativas / custos: o commit pode falhar quando outro sistema gasta o recurso (caminho documentado e testado — R23).

## Ajustes da revisão
Aceita com o ajuste 2 da revisão (§25.2 da especificação), que substitui os itens 1, 3 e 4 da decisão:

1. Magnitude dos custos **congelada** na validação e gravada na reserva; custos **consolidados por
   `ResourceDefinition`** (duplicados somados); um único `TryConsumeResources` atômico no commit.
2. Verificação: `disponível = atual − reservas das outras execuções` — a própria reserva nunca bloqueia o próprio
   commit.
3. Reserva com estado explícito `Reserved → Committed | Released`, cada transição uma vez; cancelamento, interrupção e
   `Restart` liberam exatamente uma vez.
4. `CommitFailed`: sem débito, sem carga, sem cooldown, sem entregas; ações anteriores ao commit só podem ser
   não-entregadoras (validator).
5. Reembolso = valor efetivamente aplicado por `ResourcePool.Increase` (limitado pelo máximo atual), nunca acima do
   debitado.
6. Com entrega pelo Combat (`PerformAttack`, `CombatDelivery`), o commit é **obrigatório antes do envio de
   candidatos**; `OnFirstDelivery` nessas habilidades é erro de validação. `OnFirstDelivery` segue permitido em
   entregas diretas do Abilities (commit em A1, antes de o efeito entrar na caixa do alvo).
