# ADR-0002 — Integração entre packages por integration assemblies opcionais

- **Status:** Aceita (M0)

## Contexto
Os sistemas precisam conversar (knockback do Combat no motor do Character, abilities executando ataques,
BT comandando personagens), mas cada package deve funcionar sozinho e o grafo não pode ter ciclos.

## Decisão
1. O código que conhece dois packages vive no package de **camada superior**, em uma assembly separada:
   `Runtime/Integration/<Outro>/RamiresTechGames.<Sistema>.Integration.<Outro>.asmdef`.
2. Essa asmdef declara:
   ```json
   "defineConstraints": ["RAMIRESTECHGAMES_<OUTRO>"],
   "versionDefines": [
     { "name": "com.ramirestechgames.<outro>", "expression": "0.1.0", "define": "RAMIRESTECHGAMES_<OUTRO>" }
   ]
   ```
   Sem o outro package (ou com versão fora da faixa), a assembly simplesmente não compila.
3. Para fluxo da camada inferior para a superior, a inferior define um **extension point** (interface),
   e a superior o implementa em sua integration assembly; o usuário escolhe a implementação no Inspector
   (`[SerializeReference]`) ou por componente.
4. Os símbolos de define estão em `dependency-graph.json` e são verificados por `tools/check_dependencies.py`.

## Alternativas
- **Packages de integração separados** (`com.ramirestechgames.combat-character`): fragmenta domínios coesos.
- **Glue apenas no jogo:** cada jogo reescreveria os mesmos adapters.
- **Dependências hard em tudo:** perde independência e cria risco de ciclo.

## Consequências
- O jogo recebe adapters prontos ao instalar os packages juntos (Editor-first).
- Risco R2 (explosão de adapters): adapters só com caso de uso concreto.

## Verificação (M0, Unity 6000.6.5f1, projeto RamiresTech-Sandbox da época)
Três assemblies de prova com código propositalmente inválido quando a condição não é atendida:

| Caso | Configuração | Resultado |
|---|---|---|
| Package ausente | referência `RamiresTechGames.Stats`, `versionDefines` em `com.ramirestechgames.stats` | Não compilou; console sem erros |
| Package presente | referência `Unity.InputSystem`, `versionDefines` em `com.unity.inputsystem` `1.0.0` | Compilou; símbolo `RAMIRESTECHGAMES_INPUTSYSTEM` ativo |
| Versão fora da faixa | `versionDefines` em `com.ramirestechgames.core` `[9.0.0,10.0.0)` | Não compilou; console sem erros |

As provas foram removidas após a verificação.
