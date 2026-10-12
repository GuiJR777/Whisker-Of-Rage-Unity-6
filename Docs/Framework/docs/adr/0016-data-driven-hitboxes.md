# ADR-0016 — Hitboxes como dados ancorados, consultas sem alocação e anti-tunneling

- **Status:** Aceita (revisão do design do M4, 2026-10-10) · **Milestone:** M4
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

## Ajustes da revisão
Aprovada após definir a pose autoritativa (§24.4 da especificação): pose do Rigidbody × facing lógico × pose de
autoria da âncora capturada no `Awake` × pose da hitbox (com keyframes no tempo normalizado da janela), calculada na
fase 1 no relógio fixo; animação nunca determina colisão. Âncoras identificadas por `GameplayTag`, com validação e
fallback seguro para a raiz (contado). Limite de 4 subamostras documentado e testado com hurtboxes finas.

## Refinamento (M4, entrega 6 — risco R20, 2026-10-11)
A captura usava o inverso do facing lógico no `Awake`. Com o facing apresentado por um visual girado só no
`LateUpdate` (`TransformFacingPresenter`), uma âncora dentro desse visual era congelada com a orientação errada
quando o facing inicial não era +z, e o preview usava o `forward` do transform em vez do facing lógico.

**Decisão:** a pose de autoria é **canônica**, relativa ao *frame de facing* da âncora (+z local = facing por
contrato): `HitboxAnchor.FacingFrame` explícito → frame do corpo (`IFacingFrameSource`, novo contrato em
`Combat.Bodies`; o `CharacterCombatBody` devolve o visual do presenter) quando a âncora está dentro dele → raiz do
ator. Posição medida a partir da posição do ator nos eixos do frame; rotação relativa ao frame. A captura continua no
`Awake`, uma vez, sem Animator; não é adiada para `LateUpdate`. O runtime do Combat continua sem depender do
Character (o contrato é do Combat; a integração o implementa). O preview usa o mesmo facing lógico e a mesma função.

**Compatibilidade:** âncoras na raiz de um ator sem fonte de facing (raiz girada com o facing) e formas sem âncora
mantêm exatamente a pose anterior (testado). Âncoras na raiz de um ator com fonte de facing passam a ser lidas nos
eixos da raiz (+z = facing), não no facing inicial — o caso que o R20 tornava ambíguo.

**Validação:** frame explícito que não contém a âncora e frame fora do ator (erro); pivô do frame fora do eixo vertical
do ator, vários presenters sem frame explícito (aviso); frame igual ao Rigidbody quando há presenter (erro).
