# Arquitetura — {{PACKAGE_ID}}

## Visão geral
_Um parágrafo: qual problema o package resolve e qual é a ideia central do design._

## Camadas

| Camada | Assembly | Pastas | Conteúdo |
|---|---|---|---|
| Domain | `{{ASSEMBLY_ROOT}}` | `Runtime/<Area>/` | Classes puras |
| Unity Integration | `{{ASSEMBLY_ROOT}}` | `Runtime/<Area>/` | MonoBehaviours, ScriptableObjects |
| Presentation | _(opcional)_ | `Runtime/Presentation/` | Adapters de Animator/VFX/SFX |
| Integration | `{{ASSEMBLY_ROOT}}.Integration.<Outro>` | `Runtime/Integration/<Outro>/` | Adapters para packages opcionais |
| Editor | `{{ASSEMBLY_ROOT}}.Editor` | `Editor/` | Inspectors, janelas, validators, debuggers |

## Componentes principais

| Tipo | Categoria | Responsabilidade |
|---|---|---|
| `{{SYSTEM_NAME}}PackageInfo` | static | Identidade do package e raízes de menu |

## Fluxo de dados
```
Definition (SO) ──cria──> Instance (Pure) <──tica── Component (MB) ──eventos──> Presentation/Glue
```

## Ordem de execução
| Componente | Banda (`ExecutionOrder`) | Update/FixedUpdate |
|---|---|---|
| — | — | — |

## Decisões
Ver `Documentation~/adr/`.
