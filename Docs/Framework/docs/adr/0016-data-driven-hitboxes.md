# ADR-0016 — Hitboxes como dados ancorados, consultas sem alocação e anti-tunneling

- **Status:** Proposta (design do M4) · **Milestone:** M4
- **Relacionada:** [ADR-0013](0013-combat-timing.md), especificação do M3 §19.2/§19.8 (política de saturação),
  [especificação do M4](../design/m4-combat-design.md) §6

## Contexto
O combate precisa funcionar com sprites e modelos 3D, ser testável sem animação e não depender de callbacks de
física. Hitboxes rápidas (golpes largos, arremessos) podem atravessar hurtboxes finas entre passos.

## Decisão
1. Formas de hitbox (`Box`, `Sphere`, `Capsule`) fazem parte da `AttackDefinition`, no espaço do facing lógico do ator,
   opcionalmente ancoradas em `HitboxAnchor`s nomeados (mão, arma, osso) que a animação pode mover.
2. Detecção por `PhysicsScene.Overlap*` com buffers pré-alocados (primário + estendido, saturação residual contada —
   mesma política aprovada no GroundSensor).
3. Anti-tunneling: deslocamento maior que metade da menor dimensão → até 4 poses interpoladas; acima disso, contado.
4. Hurtboxes são colliders numa camada própria, ligados a um `CombatTarget`; várias hurtboxes contam um alvo.

## Alternativas consideradas
- **Colliders filhos animados pelo Animator:** simulação dependente da animação (R8) e do Update.
- **Triggers com `OnTriggerEnter`:** fora das bandas (ADR-0003) e com perda em passos rápidos.

## Consequências
- Positivas: preview com scrub no Editor; mesma definição para sprite e 3D; sem GC.
- Negativas / custos: âncoras precisam existir no prefab (Validator avisa id ausente).
