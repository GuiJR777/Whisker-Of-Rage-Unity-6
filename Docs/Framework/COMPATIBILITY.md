# Compatibilidade

Conjuntos de versões validados juntos no projeto host (Whisker-Of-Rage-Unity-6). Jogos devem usar um conjunto desta tabela.

| Data | Unity | core | stats | hfsm | character | combat | abilities | ai | equipment | Validação |
|---|---|---|---|---|---|---|---|---|---|---|
| 2026-10-09 | 6000.6.5f1 | 0.1.0 (working copy, sem tag) | — | — | — | — | — | — | — | RamiresTech-Sandbox (descontinuada): compila sem warnings; 14/14 testes; sample OK |
| 2026-10-09 | 6000.6.5f1 | 0.1.0 (`main` 56a4ecd, submodule no host) | — | — | — | — | — | — | — | No Whisker-Of-Rage-Unity-6: compila sem warnings; 14/14 testes |
| 2026-10-09 | 6000.6.5f1 | **v0.1.0** (`f2c3522`) | — | — | — | — | — | — | — | Host: 0 erros/warnings, 14/14 testes; QG12 (projeto vazio): 0 erros/warnings, 14/14; `check_dependencies --package core` OK |
| 2026-10-09 | 6000.6.5f1 | 0.2.0 (sem tag) | 0.1.0 (sem tag) | — | — | — | — | — | — | Host: 0 erros/warnings; Core 22/22, Stats 111 EditMode + 4 PlayMode; QG12 Core e Stats OK — aguardando revisão do M1 |
| 2026-10-09 | 6000.6.5f1 | 0.2.0 (`d679181`, sem tag) | 0.1.0 (`cfc42db`, sem tag) | — | — | — | — | — | — | Após auditoria: host 0 erros/warnings; Core 27/27, Stats 125 EditMode + 4 PlayMode; QG12 Core e Stats OK; check_dependencies + 17 testes OK |
| 2026-10-09 | 6000.6.5f1 | **v0.2.0** (`d679181`) | **v0.1.0** (`cfc42db`) | — | — | — | — | — | — | **Release M1.** Host: 0 erros/warnings; Core 27/27 executados; Stats 125 EditMode + 4 PlayMode executados; QG12 Core e Stats OK com dispositivo gráfico (0 ignorados). Em headless, 1 teste do Core (UI Toolkit) é ignorado — não executado. |

Ao publicar uma tag de package, adicione uma linha com as tags efetivamente testadas.

## Notas
- **Conjunto recomendado atual:** core `v0.2.0` + stats `v0.1.0` (Unity 6000.6.5f1).
- Testes **ignorados** não contam como executados. `SelectImplementationChoicesTests.UIToolkitPath_...` exige
  dispositivo gráfico (abre uma janela de Editor); em execução headless (`-nographics`/CI sem GPU) ele é marcado
  como Ignored e a seleção por UI Toolkit fica sem verificação automática nesse ambiente.
- O `CHANGELOG.md`/`ROADMAP.md` dentro dos commits marcados ainda dizem "aguardando revisão": as tags apontam
  exatamente para os commits auditados, sem alterações posteriores. As datas de release ficam registradas aqui e
  serão refletidas nos packages no próximo commit de cada um.
