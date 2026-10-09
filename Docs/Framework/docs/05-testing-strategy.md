# 05 — Estratégia de testes

## Princípio

**Sem TDD.** A implementação vem primeiro; os testes são escritos logo depois, no mesmo milestone,
e são **gate obrigatório** para fechar o milestone. Testes existem para proteger contratos e regras de
domínio contra regressão, não para guiar o design linha a linha.

## Pirâmide

| Nível | Onde | Framework | O que cobre | Obrigatório |
|---|---|---|---|---|
| **1. Domínio** | `Tests/Editor` do package | NUnit (EditMode) | Classes puras: fórmulas de stats, stacking, transições da HFSM, travessia de combo, nós de BT, regras de slot. | Sim, para todo contrato público com lógica. |
| **2. Integração Unity** | `Tests/Runtime` do package | Unity Test Framework (PlayMode, `[UnityTest]`) | MonoBehaviours, física (hitbox/hurtbox, chão, altura exata de pulo com tolerância), ciclo de vida. | Sim, quando há física ou ciclo de vida relevante. |
| **3. Integration assemblies** | `Tests/Editor/Integration/<Outro>` com as mesmas `defineConstraints` | NUnit/UTF | Adapters entre packages (ex.: Combat→Character knockback). | Sim, por adapter. |
| **4. Cross-package** | `Assets/_FrameworkSandbox/Tests/Integration` do host | UTF PlayMode | Fluxos completos (BT → HFSM → Combat → Stats). | A partir do M8; smoke tests antes. |
| **5. Validação de dados** | Validator (Core) + testes de validators | NUnit | Definições inválidas são detectadas no Editor. | Sim. |
| **6. Alocação/performance** | Testes de hot path | `Is.Not.AllocatingGCMemory()` (UTF) | Tick de StatCollection, HFSM, BT, overlap de hitbox sem GC. | Sim para hot paths. |

## Regras

1. **Determinismo:** domínio recebe `deltaTime` por parâmetro; aleatoriedade por fonte injetável e com seed
   (ex.: chance de crítico/defesa). Nada de `Time.time` ou `UnityEngine.Random` dentro do domínio.
2. **Sem cenas em testes de package:** GameObjects criados no código e destruídos no `TearDown`.
3. **Definições em teste:** `ScriptableObject.CreateInstance` + setters `internal` expostos via
   `InternalsVisibleTo` (nunca reflexão sobre campos privados).
4. **Nome:** `Metodo_Condicao_ResultadoEsperado`. Uma razão de falha por teste.
5. **Sem % mínimo de cobertura.** A régua é: toda regra documentada em `CONTRACTS.md` tem teste.
6. Testes de package rodam no projeto host (Whisker-Of-Rage-Unity-6) através de `testables`.
   Testes de package não usam cenas, assets ou código do jogo.
7. **MonoBehaviours de teste (fixtures) ficam em `Tests/Runtime/Fixtures/`** (assembly não-Editor).
   A Unity recusa `AddComponent` de MonoBehaviour definido em assembly Editor-only
   ("Can't add script behaviour ... because it is an editor script") e isso aparece só como log,
   fazendo o teste falhar por motivo enganoso. A assembly `Tests.Editor` referencia `Tests.Runtime`.

## Como rodar (Unity CLI com o Editor do host aberto)

Comandos validados no M0 com Unity CLI 1.0.0-beta.8 e `com.unity.pipeline` 0.8.0-exp.1:

```bash
HOST="<caminho>/Whisker-Of-Rage-Unity-6"   # a partir de um package: HOST="../.."
unity status                                                     # Editor do host em "ready"
unity command recompile        --project-path "$HOST"         # força refresh + compilação
unity command recompile_status --project-path "$HOST"         # até "completed"
unity command console_status   --project-path "$HOST"    --format json   # groundTruth: erros/warnings
unity command console          --project-path "$HOST"    --format json   # mensagens completas
unity command run_tests        --project-path "$HOST"    --mode all \
    --filter RamiresTechGames.<Sistema> --filter_type assembly --format json
```

> Package novo em `Packages/` (submodule recém-adicionado) só é detectado após `unity command package_resolve`.
> Arquivos de package editados fora da Unity só são compilados após `recompile`. Confira o horário de
> `Library/ScriptAssemblies/RamiresTechGames.<Sistema>*.dll` em caso de dúvida.

Sem Editor aberto (CI/batch): `unity test` (ver `unity test --help`).

## Gate por milestone

- 0 erros de compilação e 0 warnings novos no console do host.
- Todos os testes EditMode e PlayMode do package verdes.
- `python tools/check_dependencies.py` sem erros.
- Validator sem erros nas definições do Sample.
