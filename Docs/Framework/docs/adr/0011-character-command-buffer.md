# ADR-0011 — `CharacterCommandBuffer`: comandos, dono das bordas, relógio e capacidade

- **Status:** Aceita (revisão do design do M3, 2026-10-10) · **Milestone:** M3
- **Relacionada:** [ADR-0004](0004-command-buffers.md) (fecha as decisões adiadas para o M3),
  [especificação do M3](../design/m3-character-design.md) §5

## Contexto
O ADR-0004 definiu buffers por domínio com comandos de estado e de borda e deixou para o M3: o primeiro buffer
concreto, a capacidade, a janela do jump buffer e onde fica o tipo genérico.

## Decisão
1. Comandos de estado: `Move` (Vector2, magnitude ≤ 1), `JumpHeld` (bool), `FacingOverride` (Vector2 opcional).
   Bordas: `Jump`, `Dash` (payload: direção opcional).
2. Bordas em ring buffer por tipo, capacidade padrão 4 (configurável 1..16); overflow descarta a mais antiga e
   incrementa `DroppedCount`. Expiradas contam em `ExpiredCount`.
3. **Janela pertence ao consumidor:** `JumpBufferTime` e `DashBufferTime` do `MovementProfileDefinition`.
4. **Dono por tipo de borda**, declarado no motor: `Motor` (padrão: o motor consome) ou `External` (um executor, por
   exemplo um estado da HFSM, consome pelo token da claim e chama `Jump()`/`Dash()`). Executores que não são donos só
   espiam.
5. Relógio: tempo de jogo do componente (`Time.timeAsDouble`), passado ao domínio; idade negativa vale 0; em
   `timeScale = 0` nada expira.
6. `Clear()` em troca de controle; `SetSource(owner)` limpa quando o dono muda.
7. O tipo genérico `CommandEdgeQueue<T>` fica **interno ao Character** até o M4; com o `CombatCommandBuffer` (segundo
   consumidor) avalia-se a extração para o Core com novo ADR.

## Alternativas consideradas
- **Sempre o motor como dono:** HFSM não poderia decidir quando o pulo acontece (ex.: bloquear pulo durante ataque
  no M4) sem duplicar regras.
- **Sempre externo:** o Character deixaria de funcionar sozinho (exigiria HFSM para pular).
- **Tipo genérico já no Core:** um único consumidor hoje; viola a regra de ≥ 2 consumidores.
- **Capacidade 1 (último vence):** perde pressionamentos rápidos em frames longos.

## Consequências
- O Character funciona isolado (dono `Motor`) e com HFSM (dono `External`) sem mudar o buffer.
- Seis testes dedicados às garantias do ADR-0004 (perda, duplicação, expiração, dono, clear, overflow).

## Ajustes da revisão (2026-10-10)
- Dono `External` usa requisição em duas fases: `RequestJump/RequestDash(token)` → consumo e execução juntos no
  `FixedUpdate`, ou rejeição quando a janela expira sem consumir (especificação §19.4).
- Instantes num único referencial (`Time.timeAsDouble`); durações acumuladas pelo modelo nunca comparadas com
  instantes (§19.7).
