# ADR-0028 — Blackboard tipado por agente com escrita diferida

- **Status:** Proposta (design v1 do M6, 2026-10-13) · **Milestone:** M6
- **Relacionada:** [ADR-0026](0026-ai-execution-and-timing.md), [especificação do M6](../design/m6-ai-design.md) §6

## Contexto
Os nós trocam dados pelo Blackboard. Dicionários de `object` alocam e encaixotam; escritas de fora do tick (eventos
de outros packages, scripts) dependeriam da ordem entre componentes.

## Decisão
1. `BlackboardDefinition` (SO) com chaves `{Id GUID, Name, Type, Default}`; tipos `Bool`, `Int`, `Float`,
   `Vector3`, `GameplayTag`, `Entity` (handle com geração), `Object`; herança opcional (ciclo = erro).
2. Instância por agente com arrays tipados (sem boxing), `Version` por chave; acesso por `BlackboardKeyReference` (id
   com cache; renomear não quebra).
3. Escrita apenas durante o tick do agente; escritas externas entram numa fila diferida de capacidade fixa (ordem
   (passo, origem, sequência), saturação contada) aplicada no início do próximo tick avaliado.
4. Sem Blackboard compartilhado no M6.

## Alternativas consideradas
- **Dicionário `string → object`:** aloca, encaixota e quebra ao renomear.
- **Blackboard de esquadrão já no M6:** exige arbitragem de escritas concorrentes; o engajamento cobre a coordenação
  necessária agora.

## Consequências
- Positivas: determinismo, zero GC, referências estáveis.
- Negativas / custos: uma escrita externa só vale no próximo tick (documentado).
- CONTRACTS do AI: tipos, referências, fila diferida.
