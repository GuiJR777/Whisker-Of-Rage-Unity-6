# 03 — Estrutura padrão de repositório

## Workspace local

Todos os repositórios ficam lado a lado. A Sandbox referencia os packages irmãos por caminho relativo.

```
Games/Framework/
  ramirestech-framework/          <- este repo: governança, ADRs, template, ferramentas
  com.ramirestechgames.core/      <- um repo Git por package
  com.ramirestechgames.stats/
  ...
  RamiresTech-Sandbox/            <- projeto Unity de desenvolvimento e integração
```

## Estrutura de um package

Gerada por `tools/new_package.py` a partir de [`templates/package/`](../templates/package/).

```
com.ramirestechgames.<id>/
  package.json                       # name, version (SemVer), unity, dependencies (só hard), samples
  README.md                          # usuário: o que é, instalação, quick start, samples
  CLAUDE.md                          # agentes/engenheiros: regras, limites, comandos, DoD
  AGENTS.md                          # ponteiro para CLAUDE.md (Codex e outros agentes)
  ARCHITECTURE.md                    # camadas, componentes, fluxo de dados, ordem de execução
  CONTRACTS.md                       # API pública, estabilidade, eventos, extension points, integrações
  TESTING.md                         # como rodar, o que cobrir, cobertura atual
  ROADMAP.md                         # milestones do package e critérios
  CHANGELOG.md                       # Keep a Changelog + SemVer
  LICENSE.md
  .editorconfig  .gitignore  .gitattributes
  Documentation~/
    adr/README.md                    # índice de ADRs do package
    adr/0000-template.md
  Runtime/
    RamiresTechGames.<Sistema>.asmdef
    AssemblyInfo.cs                  # InternalsVisibleTo (Editor, Tests)
    <Sistema>PackageInfo.cs          # nome do package e raízes de menu
    <Area>/...                       # Domain + Unity Integration por área
    Presentation/...                 # opcional
    Integration/<Outro>/             # opcional; asmdef própria por package integrado
      RamiresTechGames.<Sistema>.Integration.<Outro>.asmdef
  Editor/
    RamiresTechGames.<Sistema>.Editor.asmdef
    <Sistema>EditorMenu.cs
    <Area>/...                       # inspectors, drawers, windows, validators, debuggers
  Tests/
    Editor/                          # EditMode: domínio + validações (obrigatório)
      RamiresTechGames.<Sistema>.Tests.Editor.asmdef
    Runtime/                         # PlayMode: física, MonoBehaviours (quando necessário)
      RamiresTechGames.<Sistema>.Tests.Runtime.asmdef
  Samples~/
    <NomeDoSample>/                  # importável pelo Package Manager
```

### Regras

- **Assemblies referenciadas por nome**, nunca por GUID (o verificador rejeita GUID).
- `Runtime` referencia apenas dependências **hard**.
- Cada `Integration/<Outro>` tem `defineConstraints` com o símbolo do outro package e `versionDefines` que o define.
- `Editor` e `Tests` seguem as mesmas regras de dependência que o `Runtime` correspondente.
- Pastas terminadas em `~` são ignoradas pelo importador da Unity (`Samples~`, `Documentation~`).
- **`.meta` são versionados**, gerados pela Unity no primeiro import (o template não traz `.meta` para não
  duplicar GUIDs entre packages).

## Estrutura da Sandbox

```
RamiresTech-Sandbox/
  CLAUDE.md  README.md
  Packages/manifest.json             # "com.ramirestechgames.<id>": "file:../../com.ramirestechgames.<id>"
                                     # + "testables": [...] para rodar testes dos packages
  Assets/
    Sandbox/
      Scenes/                        # Sandbox_Main (arena de integração)
      Scripts/                       # glue code da sandbox (RamiresTechGames.Sandbox.*)
      Data/                          # definições de exemplo (stats, ataques, BTs)
      Prefabs/
    Tests/
      Integration/                   # testes cross-package (RamiresTechGames.Sandbox.Tests)
```

## Estrutura do repo de governança (este)

```
ramirestech-framework/
  README.md  CLAUDE.md  AGENTS.md  COMPATIBILITY.md
  dependency-graph.json
  docs/  01..08 + CONVENTIONS.md + adr/
  templates/package/
  tools/new_package.py  tools/check_dependencies.py
```
