# CLAUDE.md — Whisker-Of-Rage-Unity-6

## O que é este projeto
Projeto Unity **6000.6.5f1** (URP) com dois papéis:

1. **Host de desenvolvimento do RamiresTech Games Gameplay Framework.** Os packages são editados, compilados e
   testados aqui ([ADR-0007](Docs/Framework/docs/adr/0007-develop-inside-game-host.md)).
2. **Base do novo Whiskers of Rage**, um beat'em up roguelike 2.5D construído do zero sobre o framework (M9).
   O repositório antigo `GuiJR777/WOR` não é referência nem requisito de compatibilidade.

## Onde as coisas ficam
| Caminho | Conteúdo | Regra |
|---|---|---|
| `Docs/Framework/` | Governança do framework: grafo, contratos, convenções, ADRs, roadmap, template, ferramentas | Comece por `Docs/Framework/CLAUDE.md` |
| `Packages/com.ramirestechgames.*` | Um **Git submodule** por package (repo privado no GitHub) | Código de framework só aqui; cada package tem seu `CLAUDE.md` |
| `Assets/_FrameworkSandbox/` | Integration Sandbox do framework (cena `Sandbox_Main`, glue, testes cross-package) | Não é conteúdo do jogo |
| `Assets/Dev/Unity6_Development_Rules.md` | Cópia das convenções | Fonte: `Docs/Framework/docs/CONVENTIONS.md` |
| Restante de `Assets/` | Conteúdo do jogo (a partir do M9) | Estrutura em CONVENTIONS §11.2 |

## Estado atual
**M0–M3 concluídos** (Core `v0.3.0`, Stats `v0.1.0`, HFSM `v0.1.0`, Character `v0.1.0`). **M4 — Combat + Combos
em design** (`Docs/Framework/docs/design/m4-combat-design.md`); não implementar antes da aprovação do design.
Roadmap e Quality Gates: `Docs/Framework/docs/07-roadmap.md`.

## Regras
- Siga `Docs/Framework/docs/CONVENTIONS.md` (código e jogo).
- Packages nunca referenciam código, cenas ou assets do jogo. Verifique com
  `python Docs/Framework/tools/check_dependencies.py`.
- Sem TDD: testes escritos após a implementação, obrigatórios para fechar cada milestone.
- Controle o Editor pelo Unity CLI (plugin Unity) via `com.unity.pipeline`.

## Submodules
```bash
git clone --recurse-submodules https://github.com/GuiJR777/Whisker-Of-Rage-Unity-6.git
git submodule update --init            # em um clone existente
```
No Windows, clone em um caminho curto ou habilite `git config --global core.longpaths true`: caminhos longos quebram
os objetos dos submodules ("Filename too long").
Alterar um package: `cd Packages/<id>`, `git switch main`, commit + push no package, depois
`git add Packages/<id>` e commit no host.

## Comandos (Editor aberto)
```bash
unity status
unity command package_resolve  --project-path .    # após adicionar um submodule novo
unity command recompile        --project-path .
unity command console_status   --project-path . --format json
unity command run_tests        --project-path . --mode all --filter RamiresTechGames --filter_type assembly --format json
python Docs/Framework/tools/check_dependencies.py
```
