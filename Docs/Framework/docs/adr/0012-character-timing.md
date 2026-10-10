# ADR-0012 — Decisões temporais do Character

- **Status:** Proposta (design do M3) · **Milestone:** M3
- **Relacionada:** [ADR-0003](0003-execution-order-bands.md) (fecha as decisões adiadas para o M3),
  [especificação do M3](../design/m3-character-design.md) §7.2, §11

## Contexto
O ADR-0003 adiou para o M3: valor do passo fixo, interpolação de Rigidbody, motor consumindo comandos no
`FixedUpdate`, coyote/jump buffer em segundos ou passos e efeito de `timeScale`.

## Decisão
1. **Passo fixo:** o package não altera `Time.fixedDeltaTime`; é configuração do projeto (host: 0,02 s). As fórmulas
   de pulo compensam o Δt do passo (correção discreta), e os testes cobrem 0,02 / 0,0166 / 0,01. Validator avisa
   acima de 0,0334 s.
2. **Loop:** motor em `FixedUpdate`, banda `CHARACTER`; fontes em `Update`; bordas consumidas no primeiro passo fixo
   após a escrita (latência ≤ 1 frame + 1 passo, como já aceito no ADR-0003).
3. **Interpolação:** `RigidbodyInterpolation.Interpolate` em atores seguidos pela câmera; presenters em `LateUpdate`;
   teleporte por API (`Teleport`) com `Physics.SyncTransforms`.
4. **Coyote e jump buffer em segundos** de tempo de jogo (soma dos Δt fixos no motor); erro ≤ 1 passo.
5. **`timeScale`:** o motor usa o Δt fixo constante; menos passos por segundo real com `timeScale` < 1, logo
   trajetórias e alturas iguais em tempo de jogo; `timeScale = 0` congela o motor e a expiração de bordas.
6. **Hitstop local por ator:** adiado para o M4 (Combat), que decidirá entre time scale local e congelamento de
   canais; o motor não expõe escala local no M3.

## Alternativas consideradas
- **Fixar 50 Hz no package:** interferiria em projetos com outros valores; a correção discreta torna isso desnecessário.
- **Coyote/buffer em passos fixos:** mudaria a sensação ao alterar o passo fixo do projeto.
- **Usar tempo não escalado para o motor:** quebraria pausas e câmera lenta globais.

## Consequências
- Altura de pulo estável entre projetos com passos fixos diferentes (testada).
- Câmera lenta global preserva a forma das trajetórias.
- O M4 herda um ponto em aberto explícito (hitstop local).
