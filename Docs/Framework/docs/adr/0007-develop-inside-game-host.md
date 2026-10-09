# ADR-0007 — Desenvolvimento do framework dentro do Whisker-Of-Rage-Unity-6

- **Status:** Aceita (revisão do M0) · **Substitui:** ADR-0006 e o projeto/repo extra do ADR-0001

## Contexto
No M0 o framework foi montado com um projeto Unity separado (`RamiresTech-Sandbox`) e um repositório extra de
governança. O owner decidiu que **todo o desenvolvimento acontece no `Whisker-Of-Rage-Unity-6`**, o projeto base
do novo WOR.

## Decisão
1. **Projeto host único:** `Whisker-Of-Rage-Unity-6` (Unity 6000.6.5f1) é onde os packages são editados,
   compilados e testados.
2. **Um repositório por package continua** (Master Prompt): cada package é um repo privado no GitHub
   (`GuiJR777/com.ramirestechgames.<id>`) adicionado como **Git submodule** em `Packages/com.ramirestechgames.<id>`.
   A Unity trata submodules em `Packages/` como *embedded packages* (editáveis no lugar, sem entrada em
   `dependencies` do manifest). Cada package fica em `testables`.
3. **Governança** (grafo, convenções, ADRs, template, ferramentas) vive em `Docs/Framework/` do host, fora de
   `Assets/` (não é importada pela Unity). O histórico do antigo repo `ramirestech-framework` foi preservado via
   `git subtree`.
4. **Integration Sandbox** é a pasta `Assets/_FrameworkSandbox/` do host (cena de integração, glue e testes
   cross-package), separada do conteúdo do jogo.
5. **Independência dos packages** (QG2) é garantida por:
   - `tools/check_dependencies.py`: assemblies de package só podem referenciar assemblies do framework permitidos
     pelo grafo e assemblies da Unity. Qualquer outra referência (ex.: código do jogo) é erro.
   - Código do jogo nunca dentro de `Packages/com.ramirestechgames.*`; código de package nunca em `Assets/`.
   - Testes de cada package não usam cenas nem assets do jogo.
   - **QG12:** `tools/verify_isolated_install.py` instala o package e apenas suas dependências obrigatórias em um
     projeto Unity vazio e descartável (temporário, apagado ao final), compila e roda os testes. Não há segunda
     Sandbox permanente.

## Fluxo de trabalho com submodules
```bash
# alterar um package
cd Packages/com.ramirestechgames.<id>
git switch main            # submodules ficam em detached HEAD após clone/update
# ...editar, compilar e testar no Editor do host...
git commit -m "feat: ..." && git push
cd ../..
git add Packages/com.ramirestechgames.<id>     # atualiza o ponteiro do submodule no host
git commit -m "chore(deps): bump <id>"
```
Clonar o host: `git clone --recurse-submodules <url>` (ou `git submodule update --init` depois).

## Alternativas
- **Projeto Sandbox separado (ADR-0006):** isolamento máximo, mas duplica Editor/projeto e afasta o
  desenvolvimento do jogo real.
- **Packages direto em `Packages/` sem submodule:** contraria o Master Prompt e elimina versionamento independente.

## Consequências
- Um só Editor aberto; o framework é exercitado no contexto real do jogo.
- Risco de acoplamento acidental ao jogo (R17), mitigado pelo verificador e pela separação de pastas.
- Disciplina de submodule: commit/push no package antes de commitar o ponteiro no host.
- Futuros jogos consomem os packages pelos mesmos submodules, fixados em tags.
