# CLAUDE.md — {{PACKAGE_ID}}

## 1. Propósito
{{DESCRIPTION}}

Este package faz parte do **RamiresTech Games Gameplay Framework**. Ele é genérico: não conhece nenhum jogo
consumidor (WOR ou outros) e não pode conter nomes, assets ou regras específicas de um jogo.

## 2. Leia antes de alterar
1. `CONTRACTS.md` — API pública e estabilidade (não quebre sem seguir a política de versionamento).
2. `ARCHITECTURE.md` — camadas, componentes, fluxo e ordem de execução.
3. `ROADMAP.md` — milestone atual e o que está fora de escopo.
4. `TESTING.md` — como testar e o que precisa de teste.
5. `Documentation~/adr/` — decisões já tomadas (não reabra sem novo ADR).
6. Framework (repo irmão `../ramirestech-framework/`): `docs/CONVENTIONS.md`, `docs/01-dependency-graph.md`.

## 3. Limites — este package NÃO faz
- _Preencha no início do milestone (copie de `ramirestech-framework/docs/02-package-boundaries-and-contracts.md`)._
- Não lê Input System, Animator ou SpriteRenderer fora de integration/presentation assemblies.
- Não adiciona nada ao Core.

## 4. Dependências permitidas (fonte: `dependency-graph.json`)
- **Hard:** {{HARD_DEPS_MD}}
- **Opcionais (somente em `Runtime/Integration/<Outro>/` com asmdef própria):** {{OPTIONAL_DEPS_MD}}
- Qualquer outra referência é proibida. Verifique com:
  `python ../ramirestech-framework/tools/check_dependencies.py`

## 5. Regras essenciais (resumo de CONVENTIONS.md)
- Namespace = `{{ASSEMBLY_ROOT}}` + caminho de pastas a partir de `Runtime/` ou `Editor/`.
- Arquivo começa com `// Purpose: ...`; usings System → Unity → externos → projeto; termina com 1 newline.
- Allman, 4 espaços, 120 colunas, tipos explícitos, early return, sem números mágicos.
- `_camelCase` para campos privados, `UPPER_SNAKE_CASE` para constantes, sem abreviações.
- Regions na ordem: Constants, Properties, On Editor Editable, Events, Private/Protected Variables,
  MonoBehaviour Methods, Private/Protected Methods, Public Methods.
- `[SerializeField] private` sempre com `[Header]` e `[Tooltip]`. Nada de campos públicos.
- ScriptableObject = definição imutável em runtime; estado mutável em instâncias puras.
- Lógica em classes puras (recebem `deltaTime`); MonoBehaviour só integra.
- Sem alocações por frame (LINQ, closures, `new`, boxing). Física `NonAlloc`.
- Menus via `{{SYSTEM_NAME}}PackageInfo` (`CREATE_ASSET_MENU_ROOT`, `COMPONENT_MENU_ROOT`, `TOOLS_MENU_ROOT`).
- `[DefaultExecutionOrder(ExecutionOrder.<BANDA>)]` do Core em componentes que ticam simulação.
- Definições implementam `IValidatable`.
- Toda API pública tem XML doc e aparece em `CONTRACTS.md`.
- Testes depois da implementação (sem TDD), obrigatórios antes de fechar o milestone.

## 6. Comandos
O package é compilado e testado dentro da Sandbox (`../RamiresTech-Sandbox`), que o referencia via `file:`.
Com o Editor da Sandbox aberto (Unity CLI / plugin Unity):

```bash
SANDBOX="../RamiresTech-Sandbox"
unity status                                                                  # Editor "ready"?
unity command recompile        --project-path "$SANDBOX"
unity command recompile_status --project-path "$SANDBOX"
unity command console_status   --project-path "$SANDBOX" --format json      # erros/warnings
unity command run_tests        --project-path "$SANDBOX" --mode all \
    --filter {{ASSEMBLY_ROOT}} --filter_type assembly --format json
python ../ramirestech-framework/tools/check_dependencies.py
```
Arquivos editados fora da Unity só compilam após `recompile`.

## 7. Definition of Done (Quality Gates)
- [ ] Compila em Unity 6000.6.5f1 sem erros/warnings novos (QG1)
- [ ] Funciona sem qualquer jogo consumidor (QG2)
- [ ] Testes de domínio/integração verdes (QG3)
- [ ] Sample funcional (QG4)
- [ ] Docs atualizadas: README, CONTRACTS, ARCHITECTURE, TESTING, ROADMAP, CHANGELOG (QG5)
- [ ] API pública compreensível e listada (QG6)
- [ ] Editor UX: Header/Tooltip, menus, ferramentas visuais necessárias (QG7)
- [ ] Validações de configuração (QG8)
- [ ] Debug de runtime (QG9)
- [ ] `check_dependencies.py` verde (QG10)
- [ ] Caso comum montado só pelo Inspector (QG11)

## 8. Armadilhas conhecidas
- Dentro de `namespace {{ASSEMBLY_ROOT}}.Editor`, o nome `Editor` resolve para o namespace:
  use `UnityEditor.Editor` ao herdar custom inspectors.
- Renomear campo serializado: `[FormerlySerializedAs]`. Mover/renomear tipo `[SerializeReference]`: `[MovedFrom]`.
- `.meta` são gerados pela Unity no primeiro import; sempre commitar.
- Não use `Time.time`/`UnityEngine.Random` no domínio (quebra determinismo dos testes).
- MonoBehaviours usados em testes ficam em `Tests/Runtime/Fixtures/` (assembly não-Editor); em assembly
  Editor-only a Unity recusa `AddComponent` e só loga um aviso.
- `FindObjectsSortMode` é obsoleto no Unity 6000.6: use `FindObjectsByType<T>(FindObjectsInactive)`.
