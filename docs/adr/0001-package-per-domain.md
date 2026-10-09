# ADR-0001 — Um Unity Package por domínio, um repositório por package

- **Status:** Aceita (M0)

## Contexto
O framework precisa servir o novo WOR (construído do zero) e jogos futuros. O padrão anterior (`RTG_Unity_Modules` dentro de cada projeto)
copiava código entre jogos e não tinha versionamento.

## Decisão
- Cada domínio do Master Prompt é um Unity Package (`com.ramirestechgames.<id>`) em repositório Git próprio.
- Um repositório extra, `ramirestech-framework`, guarda governança (grafo, convenções, ADRs transversais,
  template, ferramentas). Ele **não** é um package e não é consumido pelos jogos.
- Um projeto Unity `RamiresTech-Sandbox` é o host de desenvolvimento e integração.
- Novos packages só com necessidade concreta + ADR + entrada no `dependency-graph.json`.

## Alternativas
- **Monorepo com vários packages:** mais simples de operar, mas o Master Prompt exige repositórios independentes
  e versionamento por package.
- **Governança dentro do Core:** colocaria documentação e ferramentas de framework no runtime dos jogos.

## Consequências
- Overhead operacional (R15), mitigado por scripts e convenções idênticas.
- Cada package tem ciclo de release próprio.
