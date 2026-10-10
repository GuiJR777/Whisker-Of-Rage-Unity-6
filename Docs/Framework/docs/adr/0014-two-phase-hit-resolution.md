# ADR-0014 — Resolução de acertos em duas fases (detecção → resolução)

- **Status:** Aceita (revisão do design do M4, 2026-10-10) · **Milestone:** M4
- **Relacionada:** [ADR-0003](0003-execution-order-bands.md) (ordem entre instâncias não é garantida),
  [especificação do M4](../design/m4-combat-design.md) §3.3, §6, §7

## Contexto
Dois atores podem se acertar no mesmo passo. Se cada acerto fosse resolvido no momento da detecção, quem roda
primeiro interromperia o ataque do outro antes de ele detectar: o resultado dependeria da ordem de execução entre
instâncias, que o ADR-0003 não garante.

## Decisão
1. **Detecção** no `FixedUpdate` do `CombatController` (banda `COMBAT`): cada janela ativa consulta a física e
   enfileira `HitCandidate`s **no alvo**, sem alterar nenhum estado de combate.
2. **Resolução** no `FixedUpdate` do `CombatTarget` (banda `COMBAT + 50`): cada alvo ordena seus candidatos de forma
   determinística (prioridade do ataque, distância, id estável do atacante) e aplica defesa, poise, dano e reação.
3. Interrupções e hit-confirm decididos na resolução valem a partir do passo seguinte para a timeline do atacante.
4. Trocas simultâneas: os dois acertos valem (clash e prioridade entre golpes ficam para depois).
5. O `CharacterMotor` (banda `CHARACTER`) roda depois e aplica o knockback no mesmo passo.

## Alternativas consideradas
- **Resolver na detecção:** dependente da ordem.
- **Coordenador global (singleton por cena):** resolve a ordem, mas cria estado global e acopla cenas/PhysicsScenes.
- **Callbacks de trigger (`OnTriggerEnter`):** rodam depois do passo de física, fora das bandas (ADR-0003).

## Consequências
- Positivas: resultado independente da ordem de criação dos atores (teste com ordem embaralhada); sem singleton.
- Negativas / custos: um passo de atraso entre o hit e o efeito na timeline do atacante (imperceptível; hitstop cobre).
- Filas de candidatos por alvo com capacidade fixa e saturação contada.

## Ajustes da revisão
Aprovada após especificar snapshot e efeitos (§24.1 da especificação): o passo tem **quatro fases** — ação e
detecção (`COMBAT`), snapshot imutável de defesa (`COMBAT + 10`), decisão pura sobre `AttackSnapshot` +
`DefenseSnapshot` (`COMBAT + 50`) e aplicação de efeitos com caixas de entrada por combatente (`COMBAT + 80`, antes do
`CharacterMotor`). Nada decidido num alvo lê estado mutável de outro; efeitos pedidos por listeners e `IHitEffect`
valem no próximo passo. Identidade `CombatantId` por instância e ordenação determinística dos candidatos
independentes da ordem de criação. Testes de trocas com ordem invertida/embaralhada, morte simultânea, parry-stun e
múltiplos candidatos.
