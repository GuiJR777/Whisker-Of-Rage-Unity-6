# Testes — {{PACKAGE_ID}}

Estratégia do framework: `Docs/Framework/docs/05-testing-strategy.md` do host (sem TDD; testes obrigatórios
antes de fechar o milestone).

## Assemblies de teste

| Assembly | Modo | Conteúdo |
|---|---|---|
| `{{ASSEMBLY_ROOT}}.Tests.Editor` | EditMode | Domínio, validators, metadados do package |
| `{{ASSEMBLY_ROOT}}.Tests.Runtime` | PlayMode | _Criar quando houver física/ciclo de vida relevante ou fixtures MonoBehaviour_ |

Para criar a assembly PlayMode, copie a asmdef de `Tests/Editor`, troque o nome para `.Tests.Runtime`,
deixe `includePlatforms` vazio, remova `UnityEditor.TestRunner` e `.Editor` das referências e adicione
`{{ASSEMBLY_ROOT}}.Tests.Runtime` às referências de `Tests.Editor`.
MonoBehaviours de teste ficam em `Tests/Runtime/Fixtures/` (em assembly Editor-only a Unity recusa `AddComponent`).
Exemplo pronto: `com.ramirestechgames.core/Tests/Runtime`.

## Como rodar
No projeto host (package listado em `testables`), com o Editor aberto:
```bash
unity command run_tests --project-path ../.. --mode all \
    --filter {{ASSEMBLY_ROOT}} --filter_type assembly --format json
```
Ou: Window > General > Test Runner.

## Cobertura atual

| Área | Testes | Garantias de CONTRACTS cobertas |
|---|---|---|
| Metadados | `PackageMetadataTests` | Nome e versão SemVer do package |

## Lacunas conhecidas
-
