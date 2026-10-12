# ADR-0024 — Projéteis determinísticos e entrega pelo Combat

- **Status:** Aceita (revisão do design do M5, 2026-10-12) · **Milestone:** M5
- **Relacionada:** [ADR-0014](0014-two-phase-hit-resolution.md), [ADR-0016](0016-data-driven-hitboxes.md), decisão
  D15 do M4 (projéteis adiados para Abilities), especificação do M4 §24.8,
  [especificação do M5](../design/m5-abilities-design.md) §11

## Contexto
O M4 adiou projéteis para o Abilities e preparou o caminho: `ICombatHitSource` e `CombatTarget.EnqueueCandidate` na
fase 1 do Combat permitem que produtores externos entrem no mesmo pipeline (snapshot → decisão → aplicação). Muitos
projéteis não são ataques (cura, marca, armadilha), e jogos sem Combat também precisam deles.

## Decisão
1. Projéteis vivem no Abilities: **cinemáticos** no passo fixo (sem Rigidbody dinâmico), colisão por **sweep**
   `NonAlloc` do ponto anterior ao atual, buffers pré-alocados, saturação contada, **pool** por definição.
2. Identidade = instigador combinado com o índice de criação; registro de acertos por alvo (perfuração, intervalo).
3. Entrega sem Combat: ações/efeitos de acerto pela caixa de entrada (ADR-0019).
4. Entrega com Combat (integração): o projétil é um `ICombatHitSource` e enfileira `AttackSnapshot`s na fase 1
   (banda `COMBAT`) com as próprias `HitProperties`; bloqueio, parry, dano, reações e hitstop são do Combat; o
   feedback decide se o projétil para, some ou perfura. Áreas usam o mesmo caminho.
5. Fora: reflexão por parry (regra nova no Combat), ricochete físico, projéteis com Rigidbody.

## Alternativas consideradas
- **Projéteis no Combat:** nem todo projétil é ataque; Abilities sem Combat ficaria sem projéteis.
- **Rigidbody + `OnCollisionEnter`:** fora das bandas, dependente de ordem e de tunneling.
- **Dano próprio no Abilities para projéteis:** duplicaria o pipeline de dano/defesa do Combat.

## Consequências
- Positivas: um pipeline de dano no framework; projéteis funcionam com ou sem Combat; determinismo preservado.
- Negativas / custos: dois caminhos de entrega para testar; um componente na banda `COMBAT` dentro do assembly de
  integração.

## Ajustes da revisão
Aceita com o ajuste 4 da revisão (§25.4 da especificação), que substitui o item 2 da decisão:

1. **Identidade por disparo:** `ProjectileShotId` = (instigador, índice de disparo, geração da vaga do pool); o
   `ProjectileCombatSource` deriva dela o `CombatantId`.
2. `ReceiveHitFeedback` (fase 3 do Combat) só grava o resultado num buffer fixo com a geração; nada muda durante a
   resolução. O buffer é consumido em A1 do passo seguinte, em ordem total, pelas políticas: `Hit` conta acerto
   (perfuração ou término); `Blocked` termina (`StopOnBlock`) ou continua (`PierceGuard`); `Parried` termina;
   `Invulnerable`/`Dodged`/`Miss` continuam sem registrar o alvo.
3. **Pool:** a vaga só é reutilizada quando não há feedback nem candidatos pendentes; feedback de geração antiga é
   descartado e contado.
4. **Instigador removido ou habilidade revogada:** o disparo guarda só dados capturados no spawn; `OnInstigatorLost`
   = `Destroy` (padrão) ou `ContinueDetached` (`Stats = null` → poder de ataque 1, regra do Combat); objetos Unity
   verificados antes de cada uso.
5. Contatos e `MaxTargets` com seleção top-K determinística (como o Combat); disparos criados em *n* movem a partir de
   *n + 1*.
