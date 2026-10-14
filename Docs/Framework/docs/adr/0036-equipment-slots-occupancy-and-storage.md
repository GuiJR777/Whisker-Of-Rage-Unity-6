# ADR-0036 — Slots, ocupação, unicidade e o limite com inventário

- **Status:** Proposta (design v1 do M7, 2026-10-14) · **Milestone:** M7
- **Relacionada:** [ADR-0018](0018-gameplay-tags-in-core.md), [ADR-0034](0034-equipment-transactions-and-timing.md),
  [especificação do M7](../design/m7-equipment-design.md) §4, §6, §10

## Contexto
Jogos diferentes modelam equipamento de formas diferentes: slots fixos (arma, armadura), grupos de slots iguais
(10 cartas, 2 anéis), itens que ocupam mais de um slot (arma de duas mãos), itens únicos. O M0 definiu que o Equipment
"não faz inventário", mas desequipar precisa de um destino para o item e samples precisam de um armazenamento simples.

## Decisão
1. **Slots por definição** (`EquipmentSlotDefinition`: categorias aceitas/recusadas por `GameplayTag` hierárquica) e
   **layout** (`EquipmentSlotLayoutDefinition`: entradas slot × quantidade, herança opcional) expandido numa lista com
   índice total, usado em loadouts, eventos e ferramentas.
2. **Ocupação múltipla** por `AlsoOccupies` (primeira instância livre de cada definição), com política `Fail` (padrão)
   ou `DisplaceToStorage` dentro da mesma transação.
3. **Unicidade** por `UniqueGroup`/`UniqueLimit`.
4. **Requisitos** SR (`IEquipRequirement`) avaliados uma vez, na validação, antes das contribuições do item;
   reavaliação contínua fora da 0.1.0 (R48).
5. **Armazenamento** é um contrato (`IEquipmentStorage`); o package traz só `EquipmentStash` (lista com capacidade e
   ids únicos). Inventário completo (grade, peso, empilhamento, economia, UI) continua fora do package.

## Alternativas consideradas
- **Slots como enum ou strings:** sem validação, sem herança de layout e com nomes de jogo no código.
- **Inventário completo no package:** cada jogo tem regras próprias; ampliaria o escopo e conflitaria com o M0.
- **Nenhum armazenamento:** samples e jogos simples repetiriam a mesma lista; desequipar sem destino perderia o item.

## Consequências
- Positivas: cobre armas, armaduras, cartas e acessórios só com assets; jogos com inventário próprio plugam pelo
  contrato.
- Negativas / custos: ocupação resolvida por "primeira instância livre" pode não servir a layouts exóticos (o jogo
  escolhe o slot explicitamente); a nota "não faz inventário" do M0 passa a "não faz inventário completo".
- `02-package-boundaries-and-contracts.md`: atualizar a seção do Equipment depois da aprovação.
