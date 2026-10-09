# 03 — Estrutura padrão de repositório

## Projeto host

Todo o desenvolvimento acontece no `Whisker-Of-Rage-Unity-6` ([ADR-0007](adr/0007-develop-inside-game-host.md)).

```
Whisker-Of-Rage-Unity-6/                 <- repo do jogo e host do framework
  CLAUDE.md  AGENTS.md
  Docs/Framework/                        <- governança: grafo, convenções, ADRs, template, ferramentas
  Packages/
    manifest.json                        <- "testables": ["com.ramirestechgames.<id>", ...]
    com.ramirestechgames.core/           <- submodule (repo próprio no GitHub)
    com.ramirestechgames.<id>/           <- um submodule por package
  Assets/
    _FrameworkSandbox/                   <- Integration Sandbox do framework (não é conteúdo do jogo)
      Scenes/Sandbox_Main.unity          <- arena: chão 30×12, Spawns/PlayerSpawn, Spawns/AISpawn, Systems, Actors
      Scripts/                           <- glue de integração (RamiresTechGames.FrameworkSandbox.*)
      Data/  Prefabs/  Materials/
      Tests/Integration/                 <- testes cross-package (M8)
    Dev/Unity6_Development_Rules.md      <- cópia sincronizada de Docs/Framework/docs/CONVENTIONS.md
    ...                                  <- conteúdo do jogo (ver CONVENTIONS §11.2)
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
    Runtime/                         # PlayMode + Fixtures/ (MonoBehaviours de teste)
      RamiresTechGames.<Sistema>.Tests.Runtime.asmdef
  Samples~/
    <NomeDoSample>/                  # importável pelo Package Manager
```

### Regras

- **Assemblies referenciadas por nome**, nunca por GUID (o verificador rejeita GUID).
- `Runtime` referencia apenas dependências **hard**; nunca assemblies do jogo.
- Cada `Integration/<Outro>` tem `defineConstraints` com o símbolo do outro package e `versionDefines` que o define.
- `Editor` e `Tests` seguem as mesmas regras de dependência que o `Runtime` correspondente.
- Pastas terminadas em `~` são ignoradas pelo importador da Unity (`Samples~`, `Documentation~`).
- **`.meta` são versionados**, gerados pela Unity no primeiro import (o template não traz `.meta` para não
  duplicar GUIDs entre packages). Os `.meta` de `Samples~` são escritos à mão (a Unity não importa essa pasta).
- Pastas vazias versionadas levam `.gitkeep` (senão o `.meta` da pasta fica órfão em outro clone).

## Estrutura de `Docs/Framework`

```
Docs/Framework/
  README.md  CLAUDE.md  AGENTS.md  COMPATIBILITY.md
  dependency-graph.json
  docs/  01..08 + CONVENTIONS.md + adr/
  templates/package/
  tools/new_package.py  tools/check_dependencies.py
```
