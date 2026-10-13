# ADR-0032 — Saída da IA por command buffers e rastreamento por observação

- **Status:** Proposta (design v1 do M6, 2026-10-13) · **Milestone:** M6
- **Relacionada:** [ADR-0004](0004-command-buffers.md), [ADR-0011](0011-character-command-buffer.md), [ADR-0015](0015-combat-commands-and-ownership.md), [ADR-0022](0022-ability-commands-and-ownership.md), [especificação do M6](../design/m6-ai-design.md) §10, §12

## Contexto
O ADR-0004 decidiu que jogador e IA compartilham a execução por command buffers e que a IA não depende da HFSM.
As ações da BT precisam saber se um ataque ou uma habilidade pedida aconteceu, mas os buffers não notificam o
consumo de uma borda quando o dono é o próprio package, e a IA não pode ser a dona `External` sem conflitar com a
HFSM.

## Decisão
1. A IA só escreve estado e bordas nos buffers (`CharacterCommandBuffer`, `CombatCommandBuffer`,
   `AbilityCommandBuffer`) por adaptadores das integration assemblies; `SetSource` ao habilitar e `Clear` ao
   desabilitar ou pausar.
2. Rastreamento por observação: a ação guarda o token; bridges registram em anéis de capacidade fixa os eventos
   públicos dos executores (`AttackStarted`/`AttackEnded`/`ActionResolved`; `AbilityActivated`/`AbilityEnded`/
   `ActivationResolved`); no tick seguinte a ação decide `Running`/`Success`/`Failure` pela presença da borda e pelos
   eventos.
3. Sem dependência da HFSM (reafirma o ADR-0004): o padrão BT → buffers → HFSM é testado na Sandbox do host.
4. Nenhum contrato de Character, Combat ou Abilities muda.

## Alternativas consideradas
- **IA como dona `External`:** recebe o resultado direto, mas conflita com a HFSM, que é a dona no padrão
  recomendado.
- **Novos eventos com token nos packages de execução:** mais diretos, mas alteram contratos aprovados; ficam como
  evolução opcional (R37).
- **Integração opcional com a HFSM:** mudaria o grafo e acoplaria a IA a nomes de estados.

## Consequências
- Positivas: jogador e IA intercambiáveis no mesmo prefab; nenhum package existente muda.
- Negativas / custos: resultado conhecido um tick depois; observação depende de a IA ser a fonte exclusiva do buffer.
- CONTRACTS do AI: adaptadores, `CompleteOn`, regras de `OnStop`.
