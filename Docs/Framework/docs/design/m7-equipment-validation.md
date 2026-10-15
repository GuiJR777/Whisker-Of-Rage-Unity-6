# M7 — Evidências de implementação do Equipment

- **Especificação:** [m7-equipment-design](m7-equipment-design.md) v2 (aprovada em 2026-10-14, WOR `02ded26` + registro
  da aprovação); ADRs 0033–0037 (aceitas).
- **Unity:** 6000.6.5f1 · **Package:** `com.ramirestechgames.equipment` 0.1.0 (sem tag), repositório privado
  `GuiJR777/com.ramirestechgames.equipment`, submodule do host.
- **Packages existentes:** nenhum alterado (Core 0.4.0, Stats 0.1.1, HFSM 0.1.0, Character 0.2.0, Combat 0.1.0,
  Abilities 0.1.0, AI 0.1.0 nos commits fixados no WOR).

As entregas são registradas abaixo à medida que são concluídas.

## Entrega 1 — fundação (Equipment `bccaecb`)

| Gate | Resultado |
|---|---|
| Compilação | 0 erros, 0 warnings (achado e corrigido: UAC0009 — `DEVELOPMENT_BUILD` obsoleto, trocado por `DEBUG`) |
| EditMode | 27/27 — `TransactionTests` (13 casos), `ReversibilityModelTests`, `EnqueueTests` (6), `AllocationTests`, `DefinitionValidationTests` (4), metadados (2) |
| PlayMode | 2/2 — `EquipmentComponent` com `StatsComponent` real (eventos, `Rebuild`, `enabled`, destruição) |
| Modelo de referência | 3 sementes × 600 operações (equipar, desequipar, trocar, mover, habilitar/desabilitar, trocas/equipamentos recusados, outras fontes, `Rebuild`) — **1803 verificações bit a bit** contra uma `StatCollection` real reconstruída; 0 violações; 0 usos indevidos de fonte |
| Atomicidade | troca recusada com falha no 1º, 2º e 3º efeito do item novo: stats bit a bit, recibos e handles do item antigo, slot e gerações intactos; efeitos provisórios revertidos; só `EquipFailed`; **nenhum `StatChanged`** |
| Violações | exceção em `Revert` e handle já removido depois do ponto sem rejeição → `Succeeded = true`, `IsClean = false`, `ContractViolations = 2`, `RecentViolations` com tipo, transação e item |
| Fila | reentrância recusada em qualquer controlador; FIFO; operação enfileirada no provisório de uma transação recusada → `Discarded`; recuperação enfileirada por `EquipFailed` executa; ciclo de falhas reenfileiradas para em 64 (`QueueOverflow`) |
| Zero GC | ciclos aquecidos de equipar, troca recusada, troca, mover, desabilitar/habilitar e desequipar com efeito (`EquipmentTokenStorage<T>`) |
| QG12 (Core + Stats) | 29 testes (27 + 2), 0 ignorados, 0 mensagens do compilador |
| `check_dependencies` | OK |

Desvios e precisões: (1) o contrato `IEquipmentEffect` foi antecipado da entrega 3 para esta, porque o kit de falhas
injetadas precisa de uma contribuição falível (os testes completos do contrato continuam na entrega 3);
(2) `EquipmentTokenStorage<T>` foi acrescentado: handles de outros packages têm construtores internos e não cabem em
inteiros do token; guardá-los num objeto (`Data`) alocaria a cada equipamento — o pool mantém zero GC em regime;
(3) motivo `InvalidModifier` acrescentado (valor não finito ou fator negativo no nível do item) e `InvalidHandle`
separado de `InvalidItem`; (4) efeitos que alteram stats precisam de `IStatsRebuildAware` para sobreviver a um
`Rebuild` (o modelo de referência usa um efeito assim); (5) `Deactivate` chamado durante uma transação (componente
desabilitado por um efeito) retorna `Reentrant` e só marca o controlador inativo. Nenhum package existente alterado.

## Entrega 2 — restrições e armazenamento (Equipment `dcc84e3`)

| Gate | Resultado |
|---|---|
| Compilação | 0 erros, 0 warnings |
| EditMode | 37/37 — novos: `RestrictionsAndStorageTests` (10) |
| PlayMode | 2/2 |
| Ocupação múltipla | arma de duas mãos com a mão secundária ocupada: `Fail` → `SlotsBlocked` com o escudo em `FailedItem` e stats intactos; `DisplaceToStorage` → escudo vai ao stash na mesma transação (contribuições revertidas), mão secundária bloqueada; no sentido inverso (escudo entrando na mão bloqueada) a arma de duas mãos é o conflito e sai com `DisplaceToStorage`, aceita pela capacidade líquida |
| Requisitos | composição `All`/`Any`, índice do requisito recusado, sem reavaliação (D8) e satisfação pelo item que sai na mesma troca (R54) |
| Armazenamento | fonte não autorizada → `ItemNotAvailable`; `StorageFull`/`StorageRejected` antes de qualquer efeito (item e stats intactos); violações de commit (pós-condição e exceção) → `IsClean = false`, item solto e em `DetachedByViolation`; eventos do stash depois de `Changed`; colocação em armazenamento recusada durante a transação |
| Zero GC | também com equipar do stash, deslocamento e desequipar para o stash |
| QG12 (Core + Stats) | 39 testes (37 + 2), 0 ignorados, 0 mensagens do compilador |
| `check_dependencies` | OK |

Desvios e precisões: (1) `IEquipmentStorageNotifications` acrescentado para que armazenamentos com eventos próprios os
entreguem depois da transação (o `EquipmentStash` usa); (2) `EquipmentItem.TryPlaceInStorage`/`TryTakeFromStorage`
são o único caminho de um armazenamento mudar a localização fora de transações (recusados durante uma); (3) uma
violação em `CommitRemove` (o armazenamento continua com o item já equipado) mantém o item equipado, registra
`StorageCommit` e o lista em `DetachedByViolation` — não há como soltar um item já consolidado no slot; (4) com
`DisplaceToStorage` e `Destination` nulo, os deslocados ficam soltos. Nenhum package existente alterado.

## Entrega 3 — conjuntos, loadouts e efeitos (Equipment `4c53faf0d6f87648c9c1376d27d6c6ec60cfe004`)

| Gate | Resultado |
|---|---|
| Compilação | 0 erros, 0 warnings |
| EditMode | 50/50 — novos: `SetsAndEffectsTests` (7 casos), `LoadoutTests` (6) |
| PlayMode | 3/3 — novo: loadout inicial aplicado no `Start` em uma transação |
| Modelo de referência | 3 sementes × 600 operações, agora também com stash, peças de conjunto (limiares com modificadores e efeito) e capturar/aplicar loadout — **1803 verificações bit a bit** contra uma `StatCollection` real reconstruída a partir dos itens habilitados e dos limiares ativos; 0 violações |
| Conjuntos | limiares cumulativos contam definições distintas habilitadas (duas cópias = uma peça; peça desabilitada não conta); reversão exata ao desequipar (livro do limiar vazio, efeito revertido) |
| Falha do bônus de conjunto | troca em que o item novo completa o limiar e o efeito do limiar recusa: **transação recusada** (`ContributionFailed`, só `EquipFailed`); o efeito do item novo foi aplicado e revertido; o item antigo nunca foi revertido (mesmos recibos e handles, mesma geração), stats bit a bit iguais, item novo solto com livro vazio, limiar inativo |
| Efeitos por posição | exceção no 1º, 2º e 3º efeito de um item: `FailedIndex` correto, stats iguais, efeitos anteriores revertidos |
| Suspensão | limiar que falha na reativação (`Activate`) fica suspenso (`SetBonusChanged` com `Suspended`), não recusa uma transação não relacionada (`IsClean`) e é ativado logo depois dela |
| Loadouts | capturar → aplicar é no-op (nenhum evento, recibos e stats iguais, estado desabilitado preservado); `ReplaceAll` mantém, move, adiciona (do stash) e remove (para o stash) em uma transação; ids ausentes, duplicados, em fonte não autorizada e com definição divergente recusados antes de qualquer efeito; `FillEmpty` pula slots ocupados e bloqueados (`SkippedEntries`); assets mantêm itens da mesma definição e nível e criam só o que falta; falha provisória no último item deixa tudo intacto |
| Kit de contrato | `EquipmentEffectContract.CheckRoundTrip` aceita um efeito reversível e uma recusa limpa; detecta resíduo de recusa e `Revert` que lança |
| Zero GC | também com limiar de conjunto ativando/desativando e reaplicação de um estado de loadout |
| QG12 (Core + Stats) | 53 testes (50 + 3), 0 ignorados, 0 mensagens do compilador |
| `check_dependencies` | OK |

Desvios e precisões: (1) o registro de estados de limiar (`SetThresholds`) é um cache por (conjunto, índice) criado no
plano — pode crescer numa transação recusada sem afetar o estado observável (ativo/suspenso/livro), e o modelo de
referência compara só limiares ativos, suspensos ou com recibos; (2) os ids de um `EquipmentLoadoutState` são
procurados só neste controlador e nas fontes autorizadas (`Source`/`Sources`): itens em outro controlador não são
resolvidos (`ItemNotFound`) — mover entre atores continua sendo uma decisão do jogo; (3) o nível de uma entrada de
estado é informativo (o nível da instância vence); (4) `EquipmentController` foi dividido em arquivos parciais
(núcleo, conjuntos, loadouts) sem mudança de comportamento; (5) a reativação dos limiares no `Activate` é uma transação
por limiar, depois dos itens, para que a falha de um limiar suspenda só ele. Nenhum package existente alterado.

## Entrega 4 — integração Abilities (Equipment `677e6641f174a53ca11f9de98dc401f93dcbddba`)

| Gate | Resultado |
|---|---|
| Compilação | 0 erros, 0 warnings (com e sem o Abilities) |
| EditMode | 50/50 (inalterado) |
| PlayMode do package | 3/3 (`Tests.Runtime`) + **20/20** (`Tests.Runtime.Integration`, ASC real no `FixedUpdate`, configurado só por API pública e campos serializados) |
| Sandbox do host | 2/2 (`Assets/_FrameworkSandbox/Tests/Integration/Equipment`); Sandbox do AI 2/2 (regressão) |
| Concessões | no nível do item (2) com tag de input do item; revogação só pelo handle — concessão de script da mesma habilidade intacta; desequipar com a habilidade em execução → `Ended Slash Revoked` (R44) |
| Passivas | efeito na fase de aplicação seguinte (Strength 15 → 20 só depois do passo) e removido com o item; instância de outra fonte intacta com empilhamento independente **e** com o padrão do Abilities (agregado por alvo, 1 pilha) |
| Tags | contagem por fonte (`GetTagCount` 2 → 1); `OwnedTagsRequirement` lê o estado antes da transação, aceita tag de outro item equipado e recusa com tag ignorada |
| **Troca recusada (obrigatório)** | 5 causas — efeito do item novo, **limiar de conjunto completado pelo item novo**, capacidade de concessões (1 vaga), capacidade de tags soltas (16), requisito — com a arma antiga executando `Cleave` (cooldown 2 s, 3 cargas, 1 usada): stats bit a bit, contagens de tags, concessões (handle, execução, cooldown, cargas), recibos e handle do slot idênticos; uma sonda confirma que a concessão `Thrust` do item novo **existia** quando a recusa ocorreu (efeito e limiar); depois, `Cleave` termina `Completed`, nunca `Revoked`, e mantém 2 cargas |
| Resíduo R52 | passiva provisória revogada pela recusa → eventos do ASC depois da recusa: exatamente `EffectBlocked Might SourceEnded`; stats, concessões e tags restaurados |
| Modelo de referência (integração) | 3 sementes × 500 operações (equipar, trocar, desequipar, mover, habilitar/desabilitar, itens que recusam, concessões e tags de script da mesma habilidade/tag) — **1503 verificações** de concessões por habilidade, contagem de cada tag e Strength; 0 violações, 0 alvos ausentes |
| `Rebuild` e ciclo de vida | `StatsComponent.Rebuild` mantém concessões (mesmos handles), passiva e modificadores; `enabled = false` revoga tudo, `enabled = true` reconcede em ordem de slot e deixa `ReapplyFailed` o item cuja concessão não cabe mais (stats dele também fora) |
| Alvo destruído | ASC destruído antes de desequipar → `Succeeded`, `IsClean = false`, 2 violações `RevertTargetMissing` (concessão e tag), contador 2 |
| Kit de contrato | `EquipmentEffectContract` aceita os três efeitos; recusas (passiva com habilidade de comando, tags sem vaga) sem resíduo |
| Zero GC | ciclos aquecidos com concessão, tags, passiva, troca recusada, mover, desabilitar/habilitar |
| Sandbox | botas +3 Speed → velocidade real do `CharacterMotor` 6 → 9 m/s (×1,5) e volta a 6, multiplicador exatamente 1; espada (+5 `AttackPower`) concede `Strike` (`PerformAttackAction`) executado pela HFSM (`HasAbilityCommandCondition` + `AbilityBehaviour`) → dano 15; troca para machado (+15) → dano 25; desequipar remove o golpe (nenhum ataque) e o stat volta a 10 |
| QG12 (Core + Stats) | 53 testes (50 + 3), 0 ignorados, 0 mensagens do compilador — a integração e seus testes não compilam sem o Abilities |
| `check_dependencies` | OK |

Desvios e precisões: (1) `EquipmentEffectContext.ReportRevertTargetMissing` foi acrescentado: a §7.4 exige que
`RevokeAbility`/`RemoveLooseTag` falsos sejam contados e, depois do commit, reportados, mas `Revert` não tem retorno —
o efeito informa pelo contexto e o controlador registra com a transação e o recibo corretos; (2) correção no
`EquipmentComponent.TryGetTarget` (devolvia componente destruído e não via componente adicionado depois), achada ao
testar o ASC destruído; (3) as capacidades do ASC continuam sem consulta pública (§19.2): a aplicação provisória detecta
e desfaz, como projetado; (4) passivas cujo efeito usa o empilhamento padrão do Abilities (agregado por alvo, 1 pilha)
não somam a mesma passiva de duas fontes — comportamento do Abilities, documentado para os autores de conteúdo; a
remoção continua só pela concessão do item; (5) Character e Combat continuam sem assembly de integração: velocidade e
dano passam pelo Stats e o golpe por concessão. Nenhum package existente alterado.

## Entrega 5 — ferramentas (Equipment `939b49e2904271a2185b7a81a0ca00de70556f6e`)

| Gate | Resultado |
|---|---|
| Compilação | 0 erros, 0 warnings |
| EditMode | **57/57** — novos: `EditorToolsTests` (7) |
| PlayMode | 3/3 + integração **21/21** (novo: validator de capacidades do ASC); Sandbox 2/2 |
| Log de transações | registro por transação (tipo, resultado/motivo, item, plano, recibos provisórios/desfeitos/revertidos, violações) escrito **antes** dos eventos (listeners de `Changed`/`EquipFailed` leem o próprio registro); troca recusada: 1 recibo provisório, 1 desfeito, **0 revertidos** (o antigo nunca tocado); 64 mais recentes |
| Debugger e inspectors | modelos de texto testados (slots com bloqueio, limiares, contadores, transações, violações; prévia por nível, slots compatíveis, layout expandido com índice, tabela de compatibilidade do loadout); janela abre e seleciona; em Play Mode, capturas reais via `GUIView.GrabPixels`: [`d5_equipment_debugger.png`](m7-equipment-validation/d5_equipment_debugger.png) (8 transações: troca recusada `RequirementNotMet [0]` sem recibo revertido, `SetEnabled` desativando/reativando o limiar `Guardian`, violação `RevertTargetMissing` com o item) e [`d5_equipment_inspector.png`](m7-equipment-validation/d5_equipment_inspector.png) (slots ao vivo com `Disable`/`Unequip`, mão secundária bloqueada pela arma de duas mãos); 0 erros/exceções no Console |
| Validators (sem falso negativo) | cena: `StatsComponent` ausente, stats ausentes no loadout inicial (item e limiar de conjunto), slot fora do layout, validators registrados chamados; Abilities: sem ASC (erro), estouro de concessões (erro) e de tags (erro), folga de troca (aviso), sem falso positivo sem conteúdo do Abilities; assets: ocupação além do layout (dois anéis válido, três não), item que nenhum slot aceita |
| Zero GC | `AllocationTests` e o teste de integração continuam verdes com o log de transações |
| QG12 (Core + Stats) | 60 testes (57 + 3), 0 ignorados, 0 mensagens do compilador |
| `check_dependencies` | OK |

Checklist manual das ferramentas para o owner: `TESTING.md` do package, seção "Checklist manual das ferramentas"
(8 itens: inspectors de equipamento, layout, loadout e conjunto; `EquipmentComponent` em Edit e Play Mode; Equipment
Debugger; validação do projeto e Validator do Core). **Pendente do owner** (com mouse).

Desvios e precisões: (1) acrescentados `EquipmentTransactionRecord`/`GetTransactionRecord` — a §14 pede o histórico das
últimas 64 transações com recibos provisórios e removidos, que os eventos públicos não carregam; o log é um anel fixo de
structs (sem alocação) e não guarda os recibos um a um, só as contagens por etapa (os recibos atuais ficam nos livros);
(2) `IEquipmentActorValidator` acrescentado para a integração Abilities contribuir com a validação de cena sem que o
Equipment dependa do Abilities; a capacidade de concessões do ASC é lida do campo serializado no Editor (não há consulta
pública no Abilities 0.1.0, §19.2) e as tags usam a capacidade fixa 16; (3) "`AlsoOccupies` com o próprio slot" da §14
foi refinado: ocupar outras instâncias do próprio slot (dois anéis) é válido; o erro é precisar de mais instâncias do
que o layout tem; (4) "ids duplicados" já são cobertos pelo `StableId` = GUID do asset (validator do asset). Nenhum
package existente alterado.

## Entrega 6 — samples e fechamento (Equipment `83cec35`)

| Gate | Resultado |
|---|---|
| Samples | **Equipment Basics** (Stats) e **Equipment Abilities** (Stats + Abilities) em `Samples~`, substituindo o Getting Started do template; assets e cenas validados sem nenhum problema (0 erros, 0 avisos) |
| Smoke tests versionados | `Assets/_FrameworkSandbox/Tests/Samples/Equipment/Editor` (`EquipmentSampleSmokeTests`, 3): importação real pelo Package Manager → assets (referências, tipos SR, validação) → cena → domain reload com a cena aberta → Play Mode → estado do herói → remoção do que foi importado; **prova de detecção**: o verificador acusa referência quebrada e erro de validação montados para isso |
| Equipment Basics em Play Mode | loadout A no início: Strength 17, Defense 7 (4 recibos); loadout B: Strength 22, Defense 8, **3 recibos aplicados e 2 revertidos** (Guardian Plate fica, Ring of Might se move sem reaplicar); segunda cópia do anel único → `UniqueLimit`; montante sem o anel → `RequirementNotMet` (Strength 14 < 15); montante com o anel → escudo deslocado ao stash na mesma transação, mão secundária bloqueada; desabilitar → stats na base, reabilitar → reaplicado; 0 violações. Captura: [`d6_basics_game.png`](m7-equipment-validation/d6_basics_game.png) |
| Equipment Abilities em Play Mode | início: `FlameSlash` + `VigorAura`, Strength 12, MaxHealth 125 (passiva na fase de aplicação seguinte); golpe usado, troca para a Heavy Blade sem a tag Warrior → `RequirementNotMet` e **cooldown do golpe 5,38 → 5,38**; com a tag, troca feita (`FlameSlash` revogado, `CrushingBlow` concedido, Strength 16) e de volta; bênção do santuário + amuleto → MaxHealth 150, desequipar o amuleto → 125 (a bênção de outra fonte fica); Arcane Ring + Belt → `ArcaneSurge` (+5 Strength) e remoção ao desequipar; 0 violações. Captura: [`d6_abilities_game.png`](m7-equipment-validation/d6_abilities_game.png) |
| Escala, zero GC e custo | 50 atores sem Abilities: **50 000 transações em 156,4 ms (3,13 µs/transação)**, zero GC (conjunto, troca recusada, mover, habilitar/desabilitar); 50 atores com ASC real: **7 000 transações em 16,9 ms (2,41 µs/transação)**, zero GC (concessão, 2 tags, passiva, troca recusada, mover, desabilitar). Sem custo por frame (sem tick) |
| Regressão completa do WOR | EditMode **869/869** (16 assemblies, contagem única: Core 62, Stats 126, HFSM 118, Character 138 + 17 + 4 + 5, Combat 153 + 9 + 2, Abilities 68 + 2, AI 102, Equipment 58, smoke AI 2, smoke Equipment 3) · PlayMode **406/406** (14 assemblies: Stats 4, HFSM 5, Character 85, Combat 85 + 17 + 3, Abilities 70 + 38, AI 45 + 25, Equipment 3 + 22, Sandbox AI 2, Sandbox Equipment 2); 0 erros e 0 warnings de compilação |
| QG12 (Core + Stats) | 61 testes (58 + 3), 0 ignorados, 0 mensagens do compilador |
| `check_dependencies` | OK |

Contagem: as rodadas por assembly usam filtro por prefixo (assemblies aninhadas aparecem também na rodada da mãe; por
isso o M6 registrou 847/440); os números acima são de uma rodada única com o filtro `RamiresTechGames` (cada teste
uma vez).

**Incidentes de ambiente durante a regressão (registrados, não mascarados):** (1) a primeira rodada dos smoke tests de
samples falhou com "Errors occurred during domain reload": o Editor.log mostra o compilador JIT do Burst sem conseguir
gravar `Library/BurstCache/JIT/Hashes/*.bhc` ("being used by another process") durante o reload que segue a importação;
nada do Equipment participa. Remoção da causa: as suítes que importam samples rodam com a compilação Burst do Editor
desligada (opção por usuário, restaurada ao fim; nenhuma alteração de projeto); (2) depois de recompilações seguidas
(importar/apagar samples), o smoke do AI Combat Arena falhou com "Internal build system error": `bee_backend` não
conseguiu renomear `Library/Bee/TundraBuildState.state` (o Restart Manager mostrou o mapa preso pelo `bee_backend` da
compilação anterior, ainda em andamento). Recuperação: esperar a compilação terminar, apagar a importação deixada pela
falha e recompilar limpo; o smoke do AI passou 2/2 em seguida e a rodada final completa passou sem falhas. Nenhum teste
foi alterado, ignorado ou repetido sem a causa resolvida.

## Relatório final do M7 — Equipment 0.1.0 release candidate (sem tag)

Estado: **seis entregas implementadas; Equipment 0.1.0 é release candidate** em
`83cec3589a3abda5d723af2b648e63668e778844`, aguardando a auditoria final e a validação manual do owner. Nenhuma tag;
M7 não está encerrado; M8 não iniciado. Nenhum package existente alterado (Core, Stats, HFSM, Character, Combat,
Abilities e AI nos mesmos commits fixados antes do M7).

### Commits
| Entrega | Equipment (package) | WOR (host) |
|---|---|---|
| Design v2 aprovado, ADRs 0033–0037 aceitas | — | `dbd8b67b8b2291743c725e4f431f2719e69cb999` |
| 1 Fundação | `bccaecb78458ebffd943c8f97755343a2171be36` | `c9616d1fa992ee99f25933e92d2f6d0432398d4c` |
| 2 Restrições e armazenamento | `dcc84e3e31df59a89a078a3bf742ddf55ab008ab` | `98a508178538941ec5f7bc329fbd3befabc49536` |
| 3 Conjuntos, loadouts e efeitos | `4c53faf0d6f87648c9c1376d27d6c6ec60cfe004` | `a74be93b74d1ecd2d53ac0bed44e22632f3c707a` |
| 4 Integração Abilities e Sandbox | `677e6641f174a53ca11f9de98dc401f93dcbddba` | `72e97ee29a0d8f2a779e6b6e0a1a3acd440d3c29` |
| 5 Ferramentas | `939b49e2904271a2185b7a81a0ca00de70556f6e` | `db98703b9e07e21a52fa2492b186b2b6b6f85fd8` |
| 6 Samples e fechamento (**RC**) | `83cec3589a3abda5d723af2b648e63668e778844` | commit que registra esta seção (fixa `83cec35`) |

### Matriz de contratos obrigatórios
| Contrato | Onde está | Evidência |
|---|---|---|
| Transação em sete etapas (plano → validação sem efeitos → provisório → desfazer → ponto sem rejeição → reverter o antigo → commit/eventos) | `EquipmentController` (`RunSteps`) | `TransactionTests`, log de transações (provisórios/desfeitos/revertidos por etapa) |
| Troca recusada nunca revoga/reconcede o antigo, nem muda cooldown, cargas ou execução | etapa 5 só depois do ponto sem rejeição | `RefusedSwapPreservationPlayModeTests` (5 causas, sonda prova a concessão nova existente na recusa), `SetsAndEffectsTests` (limiar), sample Abilities (cooldown 5,38 → 5,38) |
| Recibo por contribuição, reversão por handle, nunca remove outras fontes | `EquipmentLedger`/`EquipmentReceipt` | modelos de referência (1803 e 1503 verificações), concessões/tags/passivas de script da mesma habilidade/tag intactas |
| `IEquipmentEffect` tudo-ou-nada, reversão exata por token, sem apresentação, exceções tratadas, zero GC | contrato + `EquipmentEffectContract` | efeitos que recusam/lançam em cada posição, `RefuseWithResidue` detectado, kit aplicado aos três efeitos da integração, zero GC |
| Localização única, `ItemId` estável | `ItemLocation`, transações | `RestrictionsAndStorageTests`, `LoadoutTests` |
| `IEquipmentStorage` transacional | validação sem efeitos, commit infalível, pós-condições | stash cheio/recusa/violação de commit (`DetachedByViolation`) |
| `TryApplyLoadout(state)` sem recriar itens; itens que ficam mantêm concessões | `EquipmentController.Loadouts` | capturar → aplicar é no-op; `ReplaceAll`/`FillEmpty`; sample Basics (3 aplicados, 2 revertidos, item que fica e item que se move sem reaplicar) |
| `ReplaceAll`, `FillEmpty`, `TryMove`, ocupação múltipla, deslocamento | plano | `LoadoutTests`, `RestrictionsAndStorageTests`, sample Basics (escudo deslocado) |
| `Rebuild` com substituição de handles e recibos dormentes | `RebindStats` | `TransactionTests`, PlayMode do componente, integração (concessões e passivas mantidas) |
| `OnDisable`/`OnEnable` item a item; `OnDestroy` só recibos próprios | `Deactivate`/`Activate`/`ReleaseAll` | PlayMode do componente, integração (`ReapplyFailed` por capacidade), sample Basics (D) |
| Abilities só por `GrantAbility`/`RevokeAbility`, `AddLooseTag`/`RemoveLooseTag` e passivas (nunca `ApplyEffect`) | `Equipment.Integration.Abilities` | código (nenhuma chamada a `ApplyEffect`/`RevokeAbilitiesFromSource`), testes da integração |
| Precisão 1: operações enfileiradas no provisório de transação recusada são descartadas; `EquipFailed` pode enfileirar recuperação; FIFO; sem reentrância; ciclos contidos | `EquipmentTransactionGuard` | `EnqueueTests` (descartadas, recuperação, FIFO, `Reentrant`, limite de 64 por drenagem) |
| Precisão 2: sucesso íntegro ≠ `Succeeded` com `ContractViolations > 0`; itens `Detached` expostos; Debugger mostra problemas | `EquipResult.IsClean`, `DetachedByViolation`, Equipment Debugger | violações injetadas (exceção em `Revert`, handle ausente, commit de armazenamento, ASC destruído), captura do Debugger |

### Matriz de integrações
| Package | Como | Evidência |
|---|---|---|
| Core | hard (validação, tags, SR) | QG12 |
| Stats | hard (modificadores, requisitos, lote, `Rebuild`) | todas as suítes; QG12 só com Core + Stats (61 testes) |
| Abilities | opcional, `Equipment.Integration.Abilities` (`RAMIRESTECHGAMES_ABILITIES`) | 22 testes com ASC real; validator de capacidades; sample Abilities |
| Character | sem assembly: velocidade pelo stat (`StatMovementModifierSource`) | Sandbox: 6 → 9 m/s e volta exata |
| Combat | sem assembly: `AttackPower` pelo stat; golpe de arma = habilidade concedida com `PerformAttackAction` | Sandbox: dano 15/25 conforme a arma; desequipar remove o golpe |
| HFSM | sem assembly: o golpe concedido roda por `HasAbilityCommandCondition` + `AbilityBehaviour` | Sandbox |
| AI | nenhuma | — |

### Testes de falha por etapa (I4)
| Etapa | Falhas injetadas | Resultado |
|---|---|---|
| 1 Plano | item/handle inválido, slot inexistente ou ocupado, ids duplicados/ausentes/de fonte não autorizada/definição divergente no loadout | `EquipFailed` com o motivo, nada muda |
| 2 Validação | categoria, ocupação (`Fail`), unicidade, requisitos (stat e tags), armazenamento cheio/recusado, livro cheio, stat ausente, modificador inválido, componente inativo | idem, antes de qualquer efeito |
| 3 Provisório | efeito que recusa/lança no 1º, 2º e 3º lugar; último item de um loadout; **limiar de conjunto que recusa depois de o item novo estar aplicado**; capacidade de concessões e de tags soltas do ASC | etapa 4 desfaz só o provisório; stats bit a bit, concessões, tags, recibos e slots idênticos; nenhum `StatChanged` |
| 5–6 Depois do ponto sem rejeição | exceção em `Revert`, handle já removido, ASC destruído, commit de armazenamento violado | consolidado com `IsClean = false`, violação registrada, item solto listado |
| Fila | operação enfileirada no provisório de transação recusada; ciclo de recuperações | `Discarded`; parada em 64 (`QueueOverflow`) |

### Preservação de concessões na troca recusada
Arma antiga executando `Cleave` (cooldown 2 s, 3 cargas, 1 usada) e troca recusada por 5 causas (efeito do item novo,
limiar de conjunto completado pelo item novo, 1 vaga de concessão, 16 tags soltas, requisito por tag): `AbilityHandle`,
execução em andamento, cooldown, cargas, contagens de tags, stats e recibos idênticos; a concessão provisória do item
novo existia na hora da recusa (sonda) e foi revogada; depois, `Cleave` termina `Completed` com as 2 cargas. Sample:
cooldown 5,38 → 5,38. Desequipar com a habilidade em execução encerra com `Revoked` (R44, documentado).

### Modelos de referência
- **Núcleo:** 3 sementes × 600 operações (equipar, desequipar, trocar, mover, habilitar/desabilitar, recusas, outras
  fontes, `Rebuild`, stash, peças de conjunto, capturar/aplicar loadout) → **1803 verificações** bit a bit contra uma
  `StatCollection` real reconstruída; 0 violações.
- **Integração:** 3 sementes × 500 operações com concessões e tags de script da mesma habilidade/tag → **1503
  verificações** de concessões por habilidade, contagem de cada tag e Strength; 0 violações, 0 alvos ausentes.

### Riscos (estado no RC)
R44 (desequipar durante habilidade encerra com `Revoked`) documentado e testado; R45 (`Current` de recurso) política do
Stats; R46 (make-before-break exige capacidade para novo + antigo) — recusa atômica testada e **validator de cena do
pior caso** entregue; R47 (moveset do Combat) — golpes por concessão; R48/R54 (requisitos só no ato; satisfeitos pelo
item que sai) documentados e testados; R49 (ids globais) do jogo; R50 (tipos SR) — tipos `sealed`, smoke tests dos
samples; R51 (efeito que viola o contrato) — kit e contadores; R52 (resíduo `EffectBlocked(SourceEnded)` da passiva
provisória) **conferido: exatamente essa notificação**; R53 (armazenamento que viola o commit) testado. Lacunas que
dependem de outros packages continuam na §19.2 do design (consulta de capacidade do ASC, tags soltas configuráveis,
cancelamento silencioso de passiva, moveset do Combat).

### Desvios acumulados (todos registrados nas seções das entregas)
`IEquipmentEffect` antecipado para a entrega 1; `EquipmentTokenStorage<T>`; motivos `InvalidModifier`/`InvalidHandle`;
`IEquipmentStorageNotifications`; violação em `CommitRemove` mantém o item equipado; `DisplaceToStorage` sem destino
solta os itens; registro de limiares como cache; ids de estado só neste controlador e nas fontes autorizadas;
controlador em arquivos parciais; `EquipmentEffectContext.ReportRevertTargetMissing`; correção do `TryGetTarget`;
`EquipmentTransactionRecord`/log; `IEquipmentActorValidator`; capacidade de concessões lida do campo serializado no
Editor; validação de ocupação refinada (dois anéis válido); samples substituem o Getting Started.

### Checklist manual (owner)
1. **Ferramentas:** os 8 itens de `TESTING.md` do package ("Checklist manual das ferramentas").
2. **Samples:** importar os dois samples pelo Package Manager e seguir o roteiro do README de cada um (teclas, troca
   recusada com cooldown, outra fonte, loadouts, duas mãos, único, requisito, conjunto, desabilitar).
3. Abrir o Equipment Debugger durante os roteiros e conferir transações e ausência de violações.

## Correções da auditoria técnica final (2026-10-15)

Auditoria sobre Equipment `83cec35` / WOR `976a9ce`: arquitetura e mecanismos centrais aprovados; dois ajustes
pontuais. Commits do package separados: runtime `1bedf406f791b1a8b2bb1d15f8674529ac00bd92`, testes
`4cc9edf0ee2bc28ba75542de8ea3385899c0f465`, documentação `42d8767e448cda28d8843385631b698bc4862020`.

### Correção 1 — modificadores de instância no `EquipmentLoadoutState`
- Cada entrada exporta uma cópia independente dos modificadores da instância (`EquipmentLoadoutModifierState`): stat
  pelo `StableId` (sem referência de objeto), operação, valor, valor por nível e curva de nível copiada;
  `ToModifierEntry(stat)` permite ao jogo recriar a instância depois de resolver o stat. Nada de sistema de save.
- Formato do estado: **versão 2** (antes da primeira tag); estados da versão 1 continuam aplicáveis (os modificadores
  do estado são informativos).
- `TryApplyLoadout(state)` continua resolvendo instâncias existentes (controlador, depois fontes autorizadas) e usa os
  modificadores do **item** resolvido.
- Testes (`LoadoutTests`, 3 novos): duas instâncias da mesma definição com modificadores próprios (linear com valor
  por nível e curva); cópia independente (alterar a curva do item ou o array exposto não muda o estado);
  `JsonUtility` ida e volta (versão 2); o jogo recria as instâncias a partir dos dados (definição e stats por
  `StableId`), coloca-as num armazenamento e aplica o estado em outro ator: stats **bit a bit iguais** ao original,
  ids e slots preservados, armazenamento vazio (sem duplicação), reaplicar é no-op (0 recibos provisórios e 0
  revertidos); um estado com modificadores exportados diferentes move as instâncias existentes sem reaplicar e os
  stats seguem os modificadores dos itens.
- Achado durante a correção: o analisador do Unity (UAC1010) avisou que `Keyframe[]` não é serializado; a curva é
  guardada como `AnimationCurve` própria do estado (cópia), e o teste de ida e volta comprova a serialização.

### Correção 2 — violação em `CommitRemove`
- **Reprodução antes da correção** (sonda no Editor com `TestStorage.SkipRemove = true`, código de `83cec35`):
  `Succeeded`, `IsClean = false`, 1 violação, item com localização `Slot` e no slot, armazenamento ainda com o item,
  **e o item listado em `DetachedByViolation`** — exatamente o apontado pela auditoria.
- **Política fechada:** a localização segue a pós-condição real do armazenamento (`Contains`), não a exceção. Origem
  que mantém o item → item fica no slot (estado do Equipment é a verdade), `StorageCommit`, `IsClean = false`,
  `StorageInconsistency(StillInSource)` com o armazenamento, **sem** `Detached`; a entrada obsoleta é inerte (nenhuma
  transação aceita o item a partir de armazenamento enquanto ele estiver num slot). Exceção depois de remover →
  violação sem inconsistência. Destino que não recebe o item → `Detached`, `MissingFromDestination`,
  `DetachedByViolation`; exceção depois de adicionar → item no destino, violação sem inconsistência. Sem nova
  tentativa contra o armazenamento, sem recibos novos, sem tocar outras fontes. `StorageInconsistencies` exposto e
  mostrado pelo Debugger. O que é e o que não é garantido: garantia 34 do `CONTRACTS.md`.
- Testes (`StorageCommitPolicyTests`, 6): `SkipRemove` (estado real, diagnósticos, log de transações, inconsistência,
  inércia em outro ator, em outro slot e num loadout de estado, recibos idênticos depois das recusas, desequipar
  reverte exato, armazenamento não é tocado de novo), `ThrowOnRemove`, `ThrowAfterRemove`, `SkipAdd`, `ThrowOnAdd`,
  `ThrowAfterAdd`, troca com origem quebrada e destino íntegro; outra fonte de modificador intacta em todos; teste
  antigo (`SkipAdd`/`ThrowOnAdd`) mantido.

### Gates finais
| Gate | Resultado |
|---|---|
| Equipment EditMode | **67/67** (58 + 6 da política de commit + 3 do estado com modificadores) |
| Equipment PlayMode | 3/3 + integração Abilities **22/22** (trocas recusadas por 5 causas, modelo da integração, zero GC com 50 atores) |
| Modelos de reversibilidade (3 sementes) | núcleo **1803** verificações bit a bit; integração **1503** verificações; 0 violações |
| Smoke tests dos samples | Equipment Basics e Equipment Abilities (importação real, assets, cena, domain reload, Play Mode) + prova de detecção: **3/3**; AI 2/2 |
| Zero GC com 50 atores | sem Abilities: 50 000 transações em 376,3 ms (**7,53 µs**/transação, medido dentro da rodada completa do Editor; 3,13 µs na medição isolada da entrega 6); com ASC real: 7 000 transações em 13,9 ms (**1,99 µs**/transação); nenhuma alocação |
| Regressão completa do WOR (contagem única) | EditMode **878/878** (16 assemblies: Core 62, Stats 126, HFSM 118, Character 138 + 17 + 4 + 5, Combat 153 + 9 + 2, Abilities 68 + 2, AI 102, Equipment 67, smoke AI 2, smoke Equipment 3) · PlayMode **406/406** (14 assemblies: Stats 4, HFSM 5, Character 85, Combat 85 + 17 + 3, Abilities 70 + 38, AI 45 + 25, Equipment 3 + 22, Sandbox AI 2, Sandbox Equipment 2); 0 erros e 0 warnings de compilação |
| QG12 (Core + Stats) | **70** testes (67 + 3), 0 ignorados, 0 mensagens do compilador |
| `check_dependencies` | OK |
| Clone limpo com submodules | conferido (abaixo) |

**Execução da regressão (registrado, não mascarado):** duas rodadas EditMode completas falharam antes da final, ambas
por interação minha com o Editor durante a rodada, sem relação com o código: (1) um comando para religar o Burst,
que tinha dado timeout, foi executado no meio da rodada e trouxe de volta o lock do cache JIT do Burst (a causa
diagnosticada na entrega 6) no reload do smoke do AI Combat Arena; (2) uma consulta de status pela CLI enquanto a
thread principal estava ocupada fez o servidor da CLI registrar "Main thread operation timed out" como erro durante o
reload do smoke do Equipment Basics, e o Test Framework falha testes que veem erros no reload. Em ambos o teste já
tinha passado pela parte verificada (o Basics chegou ao Play Mode com o estado esperado). A rodada final foi feita
sem nenhum comando ao Editor até o resultado e passou 878/878; nenhum teste foi alterado ou ignorado.
