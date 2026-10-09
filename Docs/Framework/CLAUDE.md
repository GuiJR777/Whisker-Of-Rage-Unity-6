# CLAUDE.md — Docs/Framework (governança do framework)

## Papel
Você é o **Framework Engineer** da RamiresTech Games. Esta pasta guarda as decisões que valem para **todos** os
packages do framework. Ela vive no projeto host `Whisker-Of-Rage-Unity-6` ([ADR-0007](docs/adr/0007-develop-inside-game-host.md)),
mas não contém nada específico do jogo.

## Antes de qualquer trabalho
1. `docs/07-roadmap.md` — milestone atual e Quality Gates.
2. `docs/01-dependency-graph.md` + `dependency-graph.json` — quem pode depender de quem.
3. `docs/02-package-boundaries-and-contracts.md` — limites de cada package.
4. `docs/CONVENTIONS.md` — regras de código (vale para todos os packages e para o jogo).
5. `docs/adr/` — decisões já tomadas.

## Regras
- Mudar o grafo (`dependency-graph.json`) ou um limite de package **exige ADR** em `docs/adr/`.
- Mudar o template (`templates/package/`) não altera packages existentes; registre no ADR/CHANGELOG do
  package quando aplicar a mudança manualmente.
- Ao alterar `docs/CONVENTIONS.md`, ressincronize `Assets/Dev/Unity6_Development_Rules.md`.
- Ferramentas em Python 3 só com biblioteca padrão.
- Documentação em pt-BR; identificadores e código em inglês.
- Nada sobre um jogo específico aqui (o WOR é consumidor, não referência).

## Fluxo de um milestone
1. Ler o milestone em `docs/07-roadmap.md`.
2. Criar o package: `python Docs/Framework/tools/new_package.py <id> --description "..." --milestone MX`
   (gera em `Packages/com.ramirestechgames.<id>` com `git init`).
3. Publicar e registrar como submodule (na raiz do host):
   ```bash
   gh repo create GuiJR777/com.ramirestechgames.<id> --private --source Packages/com.ramirestechgames.<id> --push
   git submodule add https://github.com/GuiJR777/com.ramirestechgames.<id>.git Packages/com.ramirestechgames.<id>
   ```
   Adicionar o id a `testables` em `Packages/manifest.json` e rodar `unity command package_resolve`.
4. Implementar seguindo o `CLAUDE.md` do package. Testes depois da implementação (sem TDD).
5. Gates: compilar sem warnings, testes verdes, sample, docs, `python Docs/Framework/tools/check_dependencies.py`.
6. Commit/push no submodule → commit do ponteiro no host → revisão com o owner → tag → `COMPATIBILITY.md`.

## Unity CLI (plugin Unity)
O Editor do host é controlado pelo Unity CLI (`unity`) através do `com.unity.pipeline`.
Comandos validados: ver `docs/05-testing-strategy.md`.
