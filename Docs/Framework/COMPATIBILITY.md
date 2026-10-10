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
| 2026-10-10 | 6000.6.5f1 | 0.3.0 (sem tag) | v0.1.0 (`cfc42db`) | 0.1.0 (sem tag) | — | — | — | — | — | **M2 implementado, aguardando revisão final.** Host: 0 erros/warnings; Core 39/39, Stats 125 + 4, HFSM 110 EditMode + 5 PlayMode executados. QG12 (projeto vazio, com dispositivo gráfico): Core 39 executados, Stats 129 (com Core 0.3.0), HFSM 115; 0 ignorados. `check_dependencies` OK. |
| 2026-10-10 | 6000.6.5f1 | 0.3.0 (sem tag) | v0.1.0 (`cfc42db`) | 0.1.0 (sem tag) | — | — | — | — | — | **Correções da revisão final do M2** (triggers, Tick em listener, aviso de limiar, UX do grafo). Host: 0 erros/warnings; HFSM 118 EditMode + 5 PlayMode; Stats 125 + 4; Core 38/39 e Stats 121/125 em uma execução e Core 38/39 + Stats 125/125 na seguinte, sempre pelo risco R19 (recriação de pasta temporária), não por código do M2. QG12 (projeto vazio, com dispositivo gráfico): Core 39, Stats 129, HFSM 123 executados; 0 ignorados; 0 mensagens de compilador. `check_dependencies` OK. Validação manual do grafo: `docs/design/m2-graph-editor-validation.md`. |

Ao publicar uma tag de package, adicione uma linha com as tags efetivamente testadas.

## Notas
- **Conjunto recomendado atual:** core `v0.2.0` + stats `v0.1.0` (Unity 6000.6.5f1). O conjunto do M2 (core 0.3.0 +
  hfsm 0.1.0) só entra como recomendado após a revisão final e as tags.
- **Instabilidade observada no host (2026-10-10), não causada pelo M2 — risco técnico R19
  ([08](docs/08-architectural-risks.md)):** em algumas execuções, testes que apagam e
  recriam a mesma pasta temporária no mesmo ciclo (`SelectImplementationTests.SaveAsset_...` do Core e os casos
  `ResourceDefinition` de `StableIdTests` do Stats) falharam com "Parent directory must exist": `CreateFolder`
  devolveu o GUID da pasta recém-apagada sem recriá-la no disco. Reproduziu com todas as assemblies da HFSM excluídas
  e variou de 3 a 5 falhas entre execuções; não ocorre no QG12 (projetos novos) e as execuções finais no host
  passaram 100%. Testes aprovados não foram alterados; correção possível (pasta única por teste ou `Refresh` após
  apagar) fica para decisão do owner.
- Testes **ignorados** não contam como executados. `SelectImplementationChoicesTests.UIToolkitPath_...` exige
  dispositivo gráfico (abre uma janela de Editor); em execução headless (`-nographics`/CI sem GPU) ele é marcado
  como Ignored e a seleção por UI Toolkit fica sem verificação automática nesse ambiente.
- O `CHANGELOG.md`/`ROADMAP.md` dentro dos commits marcados ainda dizem "aguardando revisão": as tags apontam
  exatamente para os commits auditados, sem alterações posteriores. As datas de release ficam registradas aqui e
  serão refletidas nos packages no próximo commit de cada um.
