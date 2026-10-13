# ADR-0032 — Saída da IA por command buffers e rastreamento por token

- **Status:** Aceita (auditoria do design do M6, 2026-10-13; proposta no v1) · **Milestone:** M6
- **Relacionada:** [ADR-0004](0004-command-buffers.md), [ADR-0011](0011-character-command-buffer.md), [ADR-0015](0015-combat-commands-and-ownership.md), [ADR-0022](0022-ability-commands-and-ownership.md), [especificação do M6](../design/m6-ai-design.md) §10, §20.2

## Contexto
Jogador e IA compartilham a execução por command buffers e a IA não depende da HFSM (ADR-0004). As ações da BT
precisam saber o desfecho de cada borda enviada. O v1 presumia que `AttackStarted`/`AbilityActivated` correspondiam à
última borda e descartava bordas com `TryConsume`, o que viola a garantia 4 do ADR-0004.

## Decisão
1. A IA escreve só estado e bordas, pelos bridges das integration assemblies; nunca chama `TryConsume`;
   `SetSource(agent)` ao assumir o controle.
2. **Três caminhos de execução**, declarados no bridge e verificados em runtime: `HfsmOwner` (estado da HFSM pede com
   o token), `AgentOwner` (o bridge pede `RequestAction`/`RequestActivation`/`RequestJump/Dash(token)`; posse
   `External`, sem HFSM no mesmo buffer), `PackageOwner` (combo runner, ASC ou motor).
3. **Correlação por token** em `HfsmOwner`/`AgentOwner`: `ActionResolved`/`ActivationResolved` com `Request.Token`;
   término pela identidade da execução (`Instance` do ataque, handle + geração da habilidade, `DashEnded`).
4. **`PackageOwner`:** os eventos não trazem token; o bridge mantém **uma borda rastreada pendente por buffer**;
   token ausente + exatamente um evento de início compatível → iniciado; nenhum evento e idade ≥ janela do consumidor →
   `Expired`; qualquer outro caso → **`Unconfirmed`** (contado).
5. Desfechos `Pending`, `Executing`, `Completed`, `Expired`, `Rejected`, `Cancelled`, `Interrupted`, `Unconfirmed`,
   `TimedOut`. Abort com rastreio aberto: `AgentOwner` cancela por token; `HfsmOwner`/`PackageOwner` abandonam a borda
   (expira na janela do consumidor) ou limpam as próprias bordas do canal (`ClearChannel`).
6. Troca de fonte detectada por `Source`: rastreios `Cancelled (SourceChanged)`, agente sem controle até
   `TakeControl()`.
7. Nenhuma API pública de Character, Combat ou Abilities muda.

## Alternativas consideradas
- **Presumir evento = última borda:** errado com bordas iguais, prioridades, expiração e scripts.
- **IA como dona `External` sempre:** conflita com a HFSM; fica como caminho `AgentOwner` opcional.
- **Token nos eventos de início dos executores / `Retract(token)` nos buffers:** resolveriam o `PackageOwner` e o
  abandono, mas mudam packages aprovados (minor aditivo + ADR); registrados como evolução (especificação §20.2).

## Consequências
- Positivas: correlação exata com HFSM e sem HFSM (`AgentOwner`); limites explícitos e contados.
- Negativas / custos: `PackageOwner` serializa bordas rastreadas e pode resultar em `Unconfirmed`; borda abandonada
  pode executar dentro da janela (R41); a ordem `AttackStarted` → `ActionResolved` do Combat é fixada por teste de
  contrato, não pelo CONTRACTS dele (R42).
- CONTRACTS do AI: caminhos, tabela de correlação, desfechos, abandono, troca de fonte.

## Ajustes da revisão
Substituída a correlação por "último evento" por correlação por token e pela identidade da execução; estado
`Unconfirmed`/`TimedOut`; caminhos com e sem HFSM verificados contra o código do Combat 0.1.0, Abilities 0.1.0 e
Character 0.2.0; limitações e alternativas registradas antes da implementação.
