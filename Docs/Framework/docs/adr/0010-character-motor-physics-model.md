# ADR-0010 — Motor de personagem: Rigidbody dinâmico, controle por variação de velocidade e canais

- **Status:** Aceita (revisão do design do M3, 2026-10-10) · **Milestone:** M3
- **Relacionada:** [ADR-0003](0003-execution-order-bands.md), [ADR-0004](0004-command-buffers.md),
  [especificação do M3](../design/m3-character-design.md) §3, §6, §8

## Contexto
O Character precisa mover Player, Enemy e NPC com sensação precisa (altura de pulo exata, aceleração definida em
segundos) e, ao mesmo tempo, aceitar knockback e launch do futuro Combat sem que o motor os anule no passo seguinte
nem ceda o controle da física ao emissor. A Unity 6000.6 oferece `CharacterController` (sem Rigidbody), Rigidbody
cinemático (movimento e colisão por conta própria) e Rigidbody dinâmico (solver do PhysX). A documentação de
`Rigidbody.linearVelocity` desaconselha atribuir velocidade a cada passo e indica mudança direta para casos como pulo.

## Decisão
1. `Rigidbody` **dinâmico**, `useGravity = false`, rotação congelada, collider com `PhysicsMaterial` sem atrito
   (`frictionCombine = Minimum`), `interpolation = Interpolate` nos atores seguidos pela câmera.
2. Velocidade controlada por **variações**: a cada passo fixo o modelo calcula a velocidade alvo e o motor aplica
   `AddForce(alvo − medida, ForceMode.VelocityChange)`, com a parte de locomoção limitada pela aceleração do profile.
   Gravidade do profile por `AddForce(g, ForceMode.Acceleration)`.
3. Três canais: **locomoção** (planar, comandada), **externo** (impulsos e forças, decaimento próprio, não passa pela
   aceleração de locomoção) e **vertical** (pulo, gravidade, launch).
4. **Reconciliação** com a velocidade medida no início de cada passo: perdas causadas por colisões são removidas
   primeiro do canal externo e depois da locomoção; nenhuma lógica depende de callbacks de colisão.
5. Chão por `PhysicsScene.SphereCast` com buffer + raio de confirmação; rampa caminhável projeta a locomoção no plano
   do chão; snapping evita decolar no topo de rampas; rampa íngreme não é chão.
6. Forças externas entram pelo contrato estável `IExternalForceReceiver` e são aplicadas no próximo passo fixo.

## Alternativas consideradas
- **Rigidbody cinemático + collide-and-slide próprio:** controle total e determinístico por quadro, mas sem
  interação física (empurrar caixas, ser empurrado), mais código de colisão (degraus, depenetração, quinas) e forças
  externas simuladas à mão.
- **`CharacterController` da Unity:** simples, mas não é Rigidbody (forças, interpolação e interação com a física
  ficam por conta do usuário) e o contrato do roadmap pede Rigidbody 3D.
- **Atribuir `linearVelocity` diretamente:** contraria a documentação e apaga impulsos aplicados por terceiros no
  mesmo passo.
- **Canal único de velocidade:** knockback seria desacelerado pela locomoção na taxa da aceleração (ex.: 10 m/s
  anulados em ~0,17 s segurando o direcional contra).

## Consequências
- Positivas: knockback/launch previsíveis e não anulados; o Combat só chama a interface; mesmo profile para massas
  diferentes (`VelocityChange` ignora massa); física real com o resto da cena.
- Negativas / custos: dependência do solver (contatos, depenetração) — mitigada por validators (atrito, detecção
  contínua) e testes PlayMode; reconciliação é heurística e precisa de testes de parede/teto.
- CONTRACTS do Character: `IExternalForceReceiver` Experimental na 0.1.0; Stable após validação pelo Combat (M4).

## Ajustes da revisão (2026-10-10)
- Gravidade aplicada **uma vez**, pelo `MovementModel`, dentro da única variação de velocidade do passo
  (`VelocityChange`); o motor não usa `ForceMode.Acceleration` (especificação §19.1).
- Sensor com sobreposição inicial via `OverlapSphere` + `ComputePenetration` e tratamento de buffer saturado (§19.2).
- `IExternalForceReceiver` começa **Experimental**; `TryAddForceOverTime` com handle (vaga, geração); unidades m/s e
  N·s (§19.3).
