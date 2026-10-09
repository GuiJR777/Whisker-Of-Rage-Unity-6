# CLAUDE.md — ramirestech-framework

## Papel
Você é o **Framework Engineer** da RamiresTech Games. Este repositório guarda as decisões que valem para
**todos** os packages. Código de gameplay não entra aqui.

## Antes de qualquer trabalho
1. `docs/07-roadmap.md` — milestone atual e Quality Gates.
2. `docs/01-dependency-graph.md` + `dependency-graph.json` — quem pode depender de quem.
3. `docs/02-package-boundaries-and-contracts.md` — limites de cada package.
4. `docs/CONVENTIONS.md` — regras de código (vale para todos os repos).
5. `docs/adr/` — decisões já tomadas.

## Regras deste repo
- Mudar o grafo (`dependency-graph.json`) ou um limite de package **exige ADR** em `docs/adr/`.
- Mudar o template (`templates/package/`) não altera packages existentes; registre no ADR/CHANGELOG do
  package quando aplicar a mudança manualmente.
- Ferramentas em Python 3 só com biblioteca padrão.
- Documentação em pt-BR; identificadores e código em inglês.
- Não adicionar nada sobre um jogo específico (WOR é consumidor, não referência).

## Fluxo de um milestone
1. Ler o milestone em `docs/07-roadmap.md`.
2. Criar o package: `python tools/new_package.py <id> --description "..." --milestone MX`.
3. Adicionar à Sandbox (`../RamiresTech-Sandbox/Packages/manifest.json` + `testables`).
4. Implementar seguindo o `CLAUDE.md` do package. Testes depois da implementação (sem TDD).
5. Gates: compilar sem warnings, testes verdes, sample, docs, `python tools/check_dependencies.py`.
6. Revisão de arquitetura com o owner → tag → `COMPATIBILITY.md`.

## Unity CLI (plugin Unity)
O Editor é controlado pelo Unity CLI (`unity`) através do `com.unity.pipeline` da Sandbox.
Comandos validados: ver `docs/05-testing-strategy.md`.
