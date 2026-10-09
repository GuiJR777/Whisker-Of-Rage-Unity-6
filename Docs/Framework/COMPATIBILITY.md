# Compatibilidade

Conjuntos de versões validados juntos no projeto host (Whisker-Of-Rage-Unity-6). Jogos devem usar um conjunto desta tabela.

| Data | Unity | core | stats | hfsm | character | combat | abilities | ai | equipment | Validação |
|---|---|---|---|---|---|---|---|---|---|---|
| 2026-10-09 | 6000.6.5f1 | 0.1.0 (working copy, sem tag) | — | — | — | — | — | — | — | RamiresTech-Sandbox (descontinuada): compila sem warnings; 14/14 testes; sample OK |
| 2026-10-09 | 6000.6.5f1 | 0.1.0 (`main` 56a4ecd, submodule no host) | — | — | — | — | — | — | — | No Whisker-Of-Rage-Unity-6: compila sem warnings; 14/14 testes |
| 2026-10-09 | 6000.6.5f1 | **v0.1.0** (`f2c3522`) | — | — | — | — | — | — | — | Host: 0 erros/warnings, 14/14 testes; QG12 (projeto vazio): 0 erros/warnings, 14/14; `check_dependencies --package core` OK |
| 2026-10-09 | 6000.6.5f1 | 0.2.0 (sem tag) | 0.1.0 (sem tag) | — | — | — | — | — | — | Host: 0 erros/warnings; Core 22/22, Stats 111 EditMode + 4 PlayMode; QG12 Core e Stats OK — aguardando revisão do M1 |

Ao publicar uma tag de package, adicione uma linha com as tags efetivamente testadas.
