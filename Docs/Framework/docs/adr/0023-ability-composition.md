# ADR-0023 — Composição de habilidades por linha do tempo e ações SR (sem grafo)

- **Status:** Aceita (revisão do design do M5, 2026-10-12) · **Milestone:** M5
- **Relacionada:** [ADR-0008](0008-serialize-reference-picker-in-core.md), [ADR-0013](0013-combat-timing.md),
  [especificação do M5](../design/m5-abilities-design.md) §10, §15

## Contexto
O roadmap exige "composição de 2 abilities sem código novo". Sistemas comerciais costumam usar uma classe por
habilidade (com tarefas assíncronas) ou um grafo visual. O framework já tem um seletor de `[SerializeReference]` no
Core e uma regra temporal consolidada (janelas por sobreposição).

## Decisão
1. `AbilityDefinition` contém uma **linha do tempo**: ações pontuais em τ (`IAbilityAction`) e janelas `[a, b)`
   (`IAbilityWindowBehaviour` com `OnEnter`/`OnStep`/`OnExit`), escolhidas no seletor do Core.
2. Efeitos (`GameplayEffectDefinition`) e magnitudes (`IMagnitudeCalculation`) são reutilizados entre habilidades.
3. Comportamentos novos genéricos = novas implementações SR (no package, nas integrações ou no jogo), nunca uma classe
   por habilidade.
4. **Sem editor de grafo no M5**: a habilidade é linear no tempo; ramificações simples ficam em condições de ações
   (`IActivationRequirement` reutilizado como condição) e em efeitos condicionais.

## Alternativas consideradas
- **Classe C# por habilidade:** viola o critério de aceite e exige programador para cada conteúdo.
- **Grafo visual (GraphView):** ~1.500 linhas de Editor; segunda forma de expressar a mesma linha do tempo; sem ganho
  de determinismo. Reavaliar com caso real de ramificação.
- **Tarefas assíncronas (coroutines/async):** tempo fora do passo fixo e GC.

## Consequências
- Positivas: conteúdo por designers; mesmo modelo temporal do Combat; inspector com linha do tempo e validação.
- Negativas / custos: lógica muito ramificada fica verbosa (mitigado por SR do jogo); fragilidade de SR (R4/R25).

## Ajustes da revisão
Aceita sem alterações de decisão (sem editor de grafo no M5). Complementos: ações que mudam o mundo são diferidas
para A2 do próprio ator (§25.1); validators de commit e de entrega da §25.8.
