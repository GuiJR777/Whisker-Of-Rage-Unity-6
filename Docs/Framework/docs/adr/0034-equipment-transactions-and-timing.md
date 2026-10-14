# ADR-0034 — Transações atômicas do Equipment, sem tick

- **Status:** Proposta (design v1 do M7, 2026-10-14) · **Milestone:** M7
- **Relacionada:** [ADR-0003](0003-execution-order-bands.md), [ADR-0019](0019-abilities-execution-and-timing.md),
  [ADR-0033](0033-equipment-contribution-ledger.md), [especificação do M7](../design/m7-equipment-design.md) §7, §13

## Contexto
Equipar é um evento discreto (UI, loot, script de fase, troca de arma em combate), não uma simulação contínua. Uma
operação pode envolver vários itens (troca, loadout, ocupação de slots, conjuntos) e vários sistemas (Stats, Abilities,
efeitos do jogo); qualquer etapa pode falhar (requisito, capacidade de concessões, efeito recusado). Character, Combat,
Abilities e AI simulam no passo fixo com bandas; o Equipment precisa conviver com eles sem criar outra fase.

## Decisão
1. **Sem tick e sem banda**: o Equipment não roda em `Update`/`FixedUpdate`; toda mudança acontece dentro da chamada
   (`TryEquip`, `Unequip`, `TrySwap`, `TryApplyLoadout`, `SetContributionsEnabled`).
2. **Transação atômica** em etapas fixas: validação sem efeitos colaterais → lote do Stats aberto → aplicar o novo →
   reverter o antigo → conjuntos → commit → lote fechado → eventos. Falha depois da validação reverte, em ordem
   inversa, todos os recibos da transação; nada é comitado e só `EquipFailed` é emitido.
3. **Make-before-break**: em trocas e loadouts, o item novo é aplicado antes de o antigo sair; uma falha nunca
   reconcede o item antigo (preserva cooldowns, cargas e execuções dele). Custo aceito: capacidade momentânea maior.
4. **Eventos** em ordem fixa (`Unequipped` → `Equipped` → `SetBonusChanged` → `Changed`); reentrância no mesmo
   componente recusada (`Reentrant`), com fila FIFO explícita (`Enqueue`) executada depois da transação atual.
5. **Tempo**: efeitos temporais pertencem aos packages-alvo (modificadores imediatos; passivas na A2 seguinte; revogação
   encerra execução na chamada). Para resultados iguais em qualquer frame rate, o jogo chama a API no passo fixo; em
   `Update` (UI) é válido e visível no passo seguinte.
6. Inicialização: loadout inicial no `Start`; `OnDisable` desabilita contribuições, `OnEnable` reaplica, `OnDestroy`
   reverte.

## Alternativas consideradas
- **Command buffer de equipamento consumido numa banda:** traz latência e posse para uma operação que não vem de input
  por frame; nada na arquitetura precisa disso (HFSM e AI não equipam por bordas). Pode ser adicionado por cima se um
  jogo precisar.
- **Break-before-make (tirar o antigo, depois pôr o novo):** uma falha exigiria reconceder o antigo, reiniciando
  cooldowns e encerrando execuções — violaria a atomicidade observável.
- **Sem rollback (aplicação "melhor esforço"):** estados parciais e não reversíveis.

## Consequências
- Positivas: nenhum custo por frame; estado sempre consistente; testes de falha por etapa.
- Negativas / custos: troca pode falhar por capacidade momentânea (R46); quem chama em `Update` aceita latência de um
  passo para os efeitos do passo fixo.
- CONTRACTS do Equipment: etapas, ordem de eventos, motivos de falha e reentrância como garantias.
