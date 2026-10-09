# 06 — Publicação e versionamento

## Semantic Versioning por package

`MAJOR.MINOR.PATCH`, independente para cada package.

| Mudança | Antes de 1.0.0 | A partir de 1.0.0 |
|---|---|---|
| Quebra de API `Stable`, renomeação de campo serializado sem migração, mudança de comportamento documentado | MINOR | **MAJOR** |
| Nova feature compatível, mudança em API `Experimental` | MINOR ou PATCH | MINOR |
| Correção sem mudança de API | PATCH | PATCH |

- `0.1.0` é publicado quando o milestone do package passa nos Quality Gates.
- `1.0.0` é publicado após o **M8** validar o package integrado na Integration Sandbox (`Assets/_FrameworkSandbox`).

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
