# ADR-0029 — Percepção: identidades, registro, snapshot e estímulos com ordem total

- **Status:** Aceita (auditoria do design do M6, 2026-10-13; proposta no v1) · **Milestone:** M6
- **Relacionada:** [ADR-0009](0009-ai-optional-stats.md), [ADR-0026](0026-ai-execution-and-timing.md), [especificação do M6](../design/m6-ai-design.md) §7, §14

## Contexto
Sensores por consulta de física dependem de camadas e da ordem dos colliders; eventos de outros packages chegam
depois da banda `DECISION`; agentes com intervalo > 1 não avaliam todo passo; clones de prefab copiam identidades.
A auditoria do M5 exigiu saturação explícita, determinística e com diagnóstico.

## Decisão
1. **Identidade:** `AIEntityId` de 64 bits (0 inválido), serializado (único por cena, validator) ou atribuído antes
   de habilitar (`Combine(spawnIndex)` para clones); duplicata ou zero → **registro recusado**, erro e contador; nunca
   renomeação automática.
2. **Handle:** `AIEntityHandle` = (`WorldId` de sessão, vaga, geração); de outro mundo, vaga reciclada ou percebível
   que saiu → inválido.
3. **Registro com capacidade declarada** (padrão 256): cheio → recusa contada e logada; independência de ordem
   garantida **só até a capacidade**; validator de cena acusa excesso.
4. **Snapshot P0** em ordem de `AIEntityId`; descoberta sem física; física só para linha de visão (booleano).
5. **Estímulos:** emissões de *n* recebem (passo, `Source`, sequência); em P0 de *n + 1* passam por **top-K global**
   pela chave de seleção (força ↓, `Source` ↑, sequência ↑) e são entregues às **caixas por agente**, que acumulam
   entre avaliações (passos pulados não perdem estímulos) com top-K pela mesma chave; o agente consome em **ordem de
   entrega cronológica** (passo ↑, `Source` ↑, sequência ↑).
6. **Memória** por agente com top-K pela chave total da especificação, independente da ordem de chegada;
   esquecimento por tempo; detectabilidade por sentido e time por extension points.

## Alternativas consideradas
- **Overlap de física por sensor:** dependente de camadas e da ordem de colliders.
- **Renomear ids duplicados automaticamente:** resultado dependeria da ordem de habilitação.
- **Barramento lido só no passo da emissão:** agentes com intervalo > 1 perderiam estímulos.

## Consequências
- Positivas: percepção e memória invariantes à criação embaralhada (dentro das capacidades); toda saturação contada.
- Negativas / custos: O(agentes × percebíveis), mitigado por intervalos (R35); acima da capacidade do registro não há
  garantia de ordem (R43).
- CONTRACTS do AI: identidades, handles, capacidades, chaves de seleção e entrega.

## Ajustes da revisão
Fechados: registro cheio (limite declarado), duplicatas e clones, handle com mundo + vaga + geração, estímulos em
passos pulados (caixa por agente), ordem de seleção × ordem de entrega, invariância com criação embaralhada.
