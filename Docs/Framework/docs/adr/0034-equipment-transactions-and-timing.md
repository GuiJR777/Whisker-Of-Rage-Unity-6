# ADR-0034 — Transações atômicas do Equipment em sete etapas, sem tick

- **Status:** Aceita (auditoria da v2 do design do M7, 2026-10-14; proposta no v1, revisada na v2) · **Milestone:** M7
- **Relacionada:** [ADR-0003](0003-execution-order-bands.md), [ADR-0019](0019-abilities-execution-and-timing.md),
  [ADR-0033](0033-equipment-contribution-ledger.md), [ADR-0036](0036-equipment-slots-occupancy-and-storage.md),
  [especificação do M7](../design/m7-equipment-design.md) §7, §9, §13

## Contexto
Equipar é um evento discreto (UI, loot, script, troca de arma em combate), não uma simulação contínua. Uma operação
pode envolver vários itens (troca, loadout, ocupação de slots), limiares de conjunto, armazenamentos e vários sistemas
(Stats, Abilities, efeitos do jogo); várias etapas podem recusar (requisito, capacidade, armazenamento, efeito, limiar).
Revogar uma concessão do Abilities encerra a execução e descarta cooldown e cargas: depois de remover o item antigo,
"desfazer" exigiria reconcedê-lo, perdendo esse estado. A auditoria do v1 apontou que a ordem anterior (aplicar novo →
remover antigo → conjuntos) permitia exatamente isso quando um limiar de conjunto recusava depois da remoção.

## Decisão
1. **Sem tick e sem banda**: toda mudança acontece dentro da chamada da API; nenhum custo por frame.
2. **Sete etapas, em ordem normativa:**
   1. **Planejar** o estado final completo — itens resolvidos, slots e ocupações, itens que entram/saem/permanecem,
      movimentos de armazenamento, peças de conjunto e limiares que serão ativados e desativados — sem efeitos;
   2. **validar** sobre o plano, sem efeitos: categorias, ocupação, unicidade, níveis, requisitos, stats presentes,
      capacidade dos livros, `CanRelease`/`CanAccept` e capacidade líquida dos armazenamentos;
   3. **aplicar provisoriamente**, com o lote do Stats aberto, todas as contribuições novas potencialmente falíveis —
      efeitos dos itens que entram **e dos limiares que serão ativados**, depois seus modificadores;
   4. **desfazer só o provisório**, em ordem inversa, se qualquer aplicação recusar (nada do estado antigo foi tocado);
   5. **ponto sem rejeição → remover o antigo**: livros dos itens que saem e dos limiares que serão desativados;
   6. **consolidar** slots, localizações, armazenamentos (`CommitRemove`/`CommitAdd`), livros, limiares e handles;
   7. **fechar o lote do Stats e publicar os eventos** do Equipment.
3. **Operações das etapas 5 e 6 são infalíveis por contrato**: `RemoveModifier`, `RevokeAbility`, `RemoveLooseTag`
   por handle; `IEquipmentEffect.Revert`; `CommitRemove`/`CommitAdd` do armazenamento já validados; livros e slots
   internos. Violação inesperada (handle desconhecido, exceção, pós-condição de armazenamento falsa) **não** desfaz a
   transação: é registrada, contada e devolvida em `ContractViolations`; o item afetado fica `Detached` quando o
   armazenamento viola o commit.
4. **Itens que permanecem** no plano (mesmo slot, ou outro slot do mesmo componente via `TryMove`) nunca são revertidos
   nem reaplicados.
5. **Eventos**: no sucesso, depois do commit e do lote, em ordem fixa (`Unequipped` → `Moved` → `Equipped` →
   `SetBonusChanged` → `ContributionsStateChanged` → `Changed`); na falha, **somente** `EquipFailed`. Apresentação (som,
   VFX, UI) só em listeners desses eventos.
6. **Reentrância**: da etapa 1 ao fim dos eventos, transações em qualquer `EquipmentComponent` retornam `Reentrant`;
   `Enqueue` executa operações em FIFO depois da transação externa.
7. **Não se promete rollback perfeito de callbacks externos irreversíveis** (ex.: o Abilities registra o descarte da
   passiva provisória revogada); o estado de slots, livros, stats, concessões e tags volta ao anterior.
8. **Tempo**: modificadores valem na chamada; passivas na A2 seguinte; para o mesmo resultado em qualquer frame rate, o
   jogo chama a API no passo fixo. Inicialização: loadout inicial no `Start` (uma transação); `OnDisable` desabilita;
   `OnEnable` reaplica item a item (falha deixa o item desabilitado com motivo); `OnDestroy` reverte.

9. **Precisões da auditoria da v2:** (a) o resultado distingue sucesso íntegro (`IsClean`) de sucesso com
   `ContractViolations > 0`, e os itens `Detached` por violação ficam expostos no componente e no Debugger;
   (b) operações enfileiradas (`Enqueue`) durante as etapas 1–4 de uma transação recusada são descartadas
   (`Discarded`); listeners de `EquipFailed` podem enfileirar recuperação; FIFO, reentrância recusada e limite de
   operações por drenagem (`QueueOverflow`) contra ciclos.

## Alternativas consideradas
- **Ordem do v1 (novo → antigo → conjuntos):** limiar recusado depois de remover o antigo obrigaria a reconcedê-lo.
- **Break-before-make:** idem, para qualquer recusa.
- **Command buffer de equipamento numa banda:** latência e posse sem necessidade; pode ser feito por cima pelo jogo.
- **Desfazer depois do ponto sem rejeição:** só seria possível reconcedendo, o que viola a atomicidade observável.

## Consequências
- Positivas: I4 sem reconcessão; falhas testáveis por etapa e posição; nenhum custo por frame; eventos só de estados
  consolidados.
- Negativas / custos: novo e antigo coexistem na etapa 3 (capacidades, R46); violações de contrato após o ponto sem
  rejeição não têm rollback (R51, R53); resíduos externos documentados (R52).
- CONTRACTS do Equipment: etapas, operações infalíveis, tratamento de violações, ordem de eventos e reentrância.

## Revisão v2 (auditoria do v1)
Ordem refeita (plano completo e provisório incluindo limiares antes de qualquer remoção), contrato das operações
infalíveis e tratamento de violações formalizados, escopo global da reentrância, ressalva de callbacks externos.
