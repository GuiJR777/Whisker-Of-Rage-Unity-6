# ADR-0008 — Seletor genérico de `[SerializeReference]` no Core

- **Status:** Aceita (M1, decisão D5 do design de Stats)

## Contexto
Vários packages configuram comportamento polimórfico com `[SerializeReference]` (CONVENTIONS C12): fórmulas de
Stats (M1), estados e condições da HFSM (M2), etapas de dano e efeitos de acerto (M4), operações e targeting de
Abilities (M5), nós da Behaviour Tree (M6). O Unity 6 não oferece um seletor de tipo pronto no Inspector para esses
campos; sem ele o designer não consegue escolher a implementação sem código. Já há dois consumidores reais (Stats e
HFSM) e outros três previstos: cumpre a regra de entrada no Core.

## Decisão
- **Runtime (`RamiresTechGames.Core`):** `Serialization.SelectImplementationAttribute` — marca um campo
  `[SerializeReference]` para usar o seletor. Não tem lógica, nem dependência de Editor.
- **Editor (`RamiresTechGames.Core.Editor`):**
  - `Serialization.SelectImplementationUtility` — lista os tipos atribuíveis (concretos, não genéricos, `[Serializable]`,
    com construtor sem parâmetros, não `UnityEngine.Object`), cria instâncias com Undo, detecta e limpa referências
    com **tipo ausente** (`SerializationUtility.GetManagedReferencesWithMissingTypes`).
  - `Serialization.SelectImplementationDrawer` — UI Toolkit e IMGUI: dropdown de tipo, campos da instância,
    opção "None", aviso com o nome do tipo ausente e botão para limpar.
- **Validator do Core:** objetos validados que contenham referências gerenciadas com tipo ausente geram erro,
  independentemente do package.
- O Core não conhece nenhum tipo concreto de outro package; os tipos vêm de `TypeCache` em tempo de Editor.

## Limitação conhecida (verificada no Unity 6000.6.5f1)
Quando o tipo de um valor `[SerializeReference]` deixa de existir, a Unity mantém os dados no asset, mas o campo é lido
como nulo (`managedReferenceId = -2`) e nenhuma API indica **qual** campo guardava o valor (nem
`EditorJsonUtility`). Por isso o seletor mostra o aviso em todo campo vazio de um objeto com tipos ausentes, lista os
tipos ausentes **do objeto** e o botão Clear remove todos os dados órfãos daquele objeto (com Undo).

## Alternativas
- Drawer específico em cada package: duplicação em 5 packages.
- Package de terceiros: dependência externa e licença; contrário à regra de dependências do framework.

## Consequências
- Core passa a `0.2.0` (feature compatível). Stats exige `core ≥ 0.2.0`.
- Testes obrigatórios: listagem de tipos, Undo/Redo, serialização após salvar/recarregar, recarga de domínio,
  tipo ausente (detecção e limpeza).
- Renomear/mover tipos usados por `[SerializeReference]` continua exigindo `[MovedFrom]` (R4).
