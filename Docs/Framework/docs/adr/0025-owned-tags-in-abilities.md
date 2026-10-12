# ADR-0025 — Tags possuídas com contagem no Abilities

- **Status:** Aceita (revisão do design do M5, 2026-10-12) · **Milestone:** M5
- **Relacionada:** [ADR-0018](0018-gameplay-tags-in-core.md), [especificação do M5](../design/m5-abilities-design.md) §8

## Contexto
O Core 0.4.0 tem `GameplayTag` (hierárquico) e `GameplayTagSet` (conjunto serializável, sem contagem). Atores com
habilidades precisam de tags **possuídas** concedidas por várias fontes ao mesmo tempo (dois efeitos concedem
"lento"; remover um não pode remover a tag), com consultas hierárquicas rápidas para requisitos, bloqueios e
imunidades.

## Decisão
1. O Abilities define `OwnedTagCounter` (puro, capacidade fixa, contagem por tag, sem alocação) e `TagRequirements`
   (exigir todas de um conjunto, nenhuma de outro), sobre os tipos do Core.
2. Semântica hierárquica igual ao Core (`Has`: a tag ou uma descendente satisfaz o requisito pela ancestral).
3. Nenhuma mudança no Core no M5; extração para o Core avaliada quando a AI (M6) precisar das mesmas consultas
   (regra de ≥ 2 consumidores, com ADR).

## Alternativas consideradas
- **Contador no Core agora:** Core cresce (R6) com um único consumidor.
- **Usar `GameplayTagSet` sem contagem:** remoção de uma fonte apagaria a tag de todas.

## Consequências
- Positivas: semântica correta com múltiplas fontes; Core estável.
- Negativas / custos: possível migração para o Core no M6 (tipo puro, sem assets serializados — migração barata).

## Ajustes da revisão
Aceita com o refinamento da revisão (§25.7 da especificação): contribuições por (tag, fonte), em que a fonte é um
handle (dono + vaga + geração) — efeito ativo, execução, concessão, integração ou `LooseTagHandle`. Remover por fonte
remove só as contribuições dela; várias fontes da mesma tag somam; handle obsoleto ou de outro ator não remove nada.
Outros atores leem as tags pelo snapshot A0 (ADR-0019).
