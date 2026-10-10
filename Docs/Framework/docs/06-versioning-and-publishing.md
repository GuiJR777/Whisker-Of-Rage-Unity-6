# 06 — Publicação e versionamento

## Semantic Versioning por package

`MAJOR.MINOR.PATCH`, independente para cada package.

| Mudança | Antes de 1.0.0 | A partir de 1.0.0 |
|---|---|---|
| Quebra de API `Stable`, renomeação de campo serializado sem migração, mudança de comportamento documentado | MINOR | **MAJOR** |
| Nova feature compatível, mudança em API `Experimental` | MINOR ou PATCH | MINOR |
| Correção sem mudança de API | PATCH | PATCH |

- `0.1.0` é publicado quando o milestone do package passa nos Quality Gates (QG1–QG12).
- Cada package chega a `1.0.0` **individualmente**, quando cumpre os critérios abaixo. Nenhum milestone (inclusive o
  M8) promove versões em bloco. Packages diferentes podem estar em majors diferentes.

### Critérios para 1.0.0
Um package só recebe `1.0.0` quando todos os itens valem e estão registrados no seu `ROADMAP.md`:

| # | Critério | Evidência |
|---|---|---|
| V1 | **API pública estável:** toda API pública revisada e marcada `Stable` ou explicitamente `Experimental` em `CONTRACTS.md`; nenhuma mudança incompatível na API `Stable` nos dois últimos MINOR. | Diff de `CONTRACTS.md` entre versões; revisão do owner. |
| V2 | **Serialização estável:** assets criados com a versão anterior carregam sem perda de dados; renomeações cobertas por `[FormerlySerializedAs]`/`[MovedFrom]`; nenhum dado de `[SerializeReference]` perdido. | Teste que carrega os assets do sample gerados pela versão anterior. |
| V3 | **Integração validada:** o package funciona com cada integration assembly que fornece e foi exercitado na Integration Sandbox (`Assets/_FrameworkSandbox`) com os packages com que integra. | Testes de integração verdes; cena de integração. |
| V4 | **Instalação isolada:** QG12 verde na versão candidata. | Saída de `verify_isolated_install.py`. |
| V5 | **Documentação de migração:** `CHANGELOG.md` com todas as quebras desde `0.1.0` e passos de migração. | Revisão. |
| V6 | **Aprovação do owner** na revisão de arquitetura. | Registro no `ROADMAP.md` do package. |

## Publicação

1. `CHANGELOG.md` (formato Keep a Changelog): mover `Unreleased` para a nova versão, com seção
   **Breaking Changes / Migration** quando houver.
2. Atualizar `version` no `package.json`.
3. Atualizar versões mínimas em `dependencies` (só se o package passou a exigir algo novo).
4. Commit `chore(release): vX.Y.Z` e **tag anotada** `vX.Y.Z`.
5. Push de `main` e da tag.
6. Registrar o conjunto testado em `Docs/Framework/COMPATIBILITY.md` do host.

## Dependências entre packages

- `package.json` declara **somente dependências hard**, com a versão **mínima** necessária.
- Módulos nativos da Unity usados pelo Runtime (ex.: `com.unity.modules.physics` no Character) também vão no
  `package.json`: o QG12 roda num projeto sem módulos opcionais e acusa o módulo ausente (M3).
- Dependências opcionais **não** vão no `package.json`. Os integration assemblies usam `versionDefines`
  com faixa mínima (ex.: `[0.2.0,1.0.0)`), então não compilam com versão incompatível em vez de quebrar.
- UPM não resolve dependências por URL Git: o projeto consumidor precisa incluir cada package requerido
  (submodule em `Packages/`). Se faltar um, a Unity reporta o package ausente de forma explícita.

## Consumo

| Consumidor | Mecanismo | Versão |
|---|---|---|
| **Whisker-Of-Rage-Unity-6** (host de desenvolvimento) | Git submodule em `Packages/com.ramirestechgames.<id>` | `main` do package durante o desenvolvimento; tag `vX.Y.Z` em releases do jogo |
| **Futuros jogos** | Git submodule em `Packages/com.ramirestechgames.<id>` | Tag `vX.Y.Z` conhecida |

Atualizar um package no jogo:
```bash
cd Packages/com.ramirestechgames.stats
git fetch --tags && git checkout v0.2.0
cd ../.. && git add Packages/com.ramirestechgames.stats
git commit -m "chore(deps): stats v0.2.0"
```

## Regras de compatibilidade

1. Uma alteração em um package **não presume** que os outros serão atualizados juntos.
   Quem consome declara versão mínima; quem é consumido mantém compatibilidade dentro do MAJOR.
2. Remover API `Stable`: marcar `[Obsolete("Use X. Será removido em vN.0.0")]` por pelo menos um MINOR antes.
3. Mover/renomear tipos serializados por `[SerializeReference]`: usar `[MovedFrom]` (obrigatório).
4. Renomear campos serializados: `[FormerlySerializedAs]` (obrigatório).
5. O `COMPATIBILITY.md` registra conjuntos de versões validados juntos no host.

## Remotos

Um repositório GitHub **privado** por package: `https://github.com/GuiJR777/com.ramirestechgames.<id>.git`.
A governança não tem repo próprio: vive em `Docs/Framework` do host ([ADR-0007](adr/0007-develop-inside-game-host.md)).
