# ADR-0028 — Blackboard tipado por agente com escrita externa coalescida

- **Status:** Aceita (auditoria do design do M6, 2026-10-13; proposta no v1) · **Milestone:** M6
- **Relacionada:** [ADR-0026](0026-ai-execution-and-timing.md), [especificação do M6](../design/m6-ai-design.md) §6

## Contexto
Os nós trocam dados pelo Blackboard. Dicionários de `object` alocam e encaixotam; escritas de fora do tick
dependeriam da ordem entre componentes; uma fila FIFO de escritas pode saturar.

## Decisão
1. `BlackboardDefinition` com chaves `{Id GUID, Name, Type, Default}`; tipos `Bool`, `Int`, `Float`, `Vector3`,
   `GameplayTag`, `Entity` (`AIEntityHandle`), `Object`; herança opcional (ciclo = erro).
2. Instância por agente em arrays tipados (sem boxing), `Version` por chave, `BlackboardKeyReference` por id.
3. Escritas dentro do tick são imediatas e sequenciais em pré-ordem.
4. **Escritas externas coalescidas por chave:** no máximo uma pendente por chave (a fila não satura); vence a maior
   `priority`, empate → chamada mais recente; aplicadas em ordem de índice da chave no início do próximo tick avaliado.
   Fontes com ordem de chamada não determinística precisam de prioridades distintas (documentado). Bridges usam
   estímulos.
5. Sem Blackboard compartilhado no M6.

## Alternativas consideradas
- **Fila FIFO com capacidade:** satura sob escritas repetidas e depende da ordem de chamada.
- **Dicionário `string → object`:** aloca, encaixota e quebra ao renomear.

## Consequências
- Positivas: zero GC, sem saturação, referências estáveis, regra de conflito explícita.
- Negativas / custos: valores intermediários de uma chave entre ticks são descartados (só o vencedor vale).
- CONTRACTS do AI: tipos, referências, coalescência.

## Ajustes da revisão
Escrita externa sob saturação resolvida por coalescência por chave (sem perda silenciosa nem dependência de
capacidade).
