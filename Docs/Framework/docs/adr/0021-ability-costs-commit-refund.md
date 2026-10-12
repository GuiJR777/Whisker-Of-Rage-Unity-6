# ADR-0021 — Custos de habilidade: verificação, reserva, commit e reembolso

- **Status:** Proposta (design do M5, 2026-10-12) · **Milestone:** M5
- **Relacionada:** especificação do M1 (custos atômicos do Stats), [especificação do M5](../design/m5-abilities-design.md) §6

## Contexto
Habilidades podem debitar custos na ativação, num instante posterior (quando o efeito "sai") ou na primeira entrega,
e devolver custos quando canceladas ou interrompidas. O Stats oferece verificação e consumo atômicos
(`CanAfford`, `TryConsumeResources`) e ganho (`Increase`), mas não reserva: outro sistema (Combat, script) pode gastar o
recurso entre a validação e o débito.

## Decisão (proposta)
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
