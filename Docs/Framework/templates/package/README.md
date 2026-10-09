# RamiresTech Games — {{DISPLAY_NAME}}

`{{PACKAGE_ID}}` · Unity {{UNITY_VERSION}}+

{{DESCRIPTION}}

## Instalação

**Como submodule (jogos):**
```bash
git submodule add https://github.com/GuiJR777/{{PACKAGE_ID}}.git Packages/{{PACKAGE_ID}}
cd Packages/{{PACKAGE_ID}} && git checkout vX.Y.Z
```

Packages em `Packages/` são *embedded packages*: não precisam de entrada em `dependencies` do `manifest.json`.
Para rodar os testes, adicione `"{{PACKAGE_ID}}"` a `testables`.

Dependências obrigatórias que também precisam estar no projeto: {{HARD_DEPS_MD}}

## Quick start (Inspector)
_Passo a passo para o caso de uso mais comum, sem escrever código._

1. …
2. …

## Samples
| Sample | O que demonstra |
|---|---|
| Getting Started | Setup mínimo. |

## Ferramentas de Editor
| Ferramenta | Onde |
|---|---|
| Documentação | `Tools/RamiresTech Games/{{DISPLAY_NAME}}/Documentation` |
| Validator (Core) | `Tools/RamiresTech Games/Validator` |

## Integrações opcionais
Ativadas automaticamente quando o outro package está instalado: {{OPTIONAL_DEPS_MD}}

## Documentação
[CONTRACTS](CONTRACTS.md) · [ARCHITECTURE](ARCHITECTURE.md) · [TESTING](TESTING.md) ·
[ROADMAP](ROADMAP.md) · [CHANGELOG](CHANGELOG.md)
