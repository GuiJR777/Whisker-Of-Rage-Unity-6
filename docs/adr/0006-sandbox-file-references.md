# ADR-0006 — Sandbox com referências `file:`; jogos com submodules

- **Status:** Aceita (M0)

## Contexto
O Master Prompt define submodules em `Packages/` para o WOR. Durante o desenvolvimento do framework,
vários packages mudam ao mesmo tempo e precisam ser editados e testados juntos.

## Decisão
- **Sandbox:** `Packages/manifest.json` referencia cada package irmão por caminho relativo
  (`"file:../../com.ramirestechgames.<id>"`) e lista todos em `testables`.
  Pacotes locais via `file:` são editáveis na Unity e cada um continua em seu próprio repo Git.
- **Jogos (WOR):** Git submodules em `Packages/com.ramirestechgames.<id>`, fixados em tags (`vX.Y.Z`).

## Alternativas
- Submodules também na Sandbox: exigiria commit/bump do submodule a cada alteração de desenvolvimento.
- Registry privado (OpenUPM/Verdaccio): melhor resolução de dependências, mas infraestrutura extra.
  Reavaliar após o M8.

## Consequências
- A Sandbox exige o layout de workspace documentado em [03](../03-repository-structure.md).
- A Sandbox valida a working copy; o `COMPATIBILITY.md` registra as tags efetivamente validadas.
