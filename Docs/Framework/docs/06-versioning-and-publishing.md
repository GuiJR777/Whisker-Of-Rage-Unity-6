# 06 — Publicação e versionamento

## Semantic Versioning por package

`MAJOR.MINOR.PATCH`, independente para cada package.

| Mudança | Antes de 1.0.0 | A partir de 1.0.0 |
|---|---|---|
| Quebra de API `Stable`, renomeação de campo serializado sem migração, mudança de comportamento documentado | MINOR | **MAJOR** |
| Nova feature compatível, mudança em API `Experimental` | MINOR ou PATCH | MINOR |
| Correção sem mudança de API | PATCH | PATCH |

- `0.1.0` é publicado quando o milestone do package passa nos Quality Gates.
- `1.0.0` é publicado após o **M8** validar o package integrado na Sandbox.

## Publicação

1. `CHANGELOG.md` (formato Keep a Changelog): mover `Unreleased` para a nova versão, com seção
   **Breaking Changes / Migration** quando houver.
2. Atualizar `version` no `package.json`.
3. Atualizar versões mínimas em `dependencies` (só se o package passou a exigir algo novo).
4. Commit `chore(release): vX.Y.Z` e **tag anotada** `vX.Y.Z`.
5. Push de `main` e da tag.
6. Registrar o conjunto testado em `ramirestech-framework/COMPATIBILITY.md`.

## Dependências entre packages

- `package.json` declara **somente dependências hard**, com a versão **mínima** necessária.
- Dependências opcionais **não** vão no `package.json`. Os integration assemblies usam `versionDefines`
  com faixa mínima (ex.: `[0.2.0,1.0.0)`), então não compilam com versão incompatível em vez de quebrar.
- UPM não resolve dependências por URL Git: o projeto consumidor precisa incluir cada package requerido
  (submodule ou `file:`). Se faltar um, a Unity reporta o package ausente de forma explícita.

## Consumo

| Consumidor | Mecanismo | Versão |
|---|---|---|
| **Sandbox** (desenvolvimento) | `"com.ramirestechgames.<id>": "file:../../com.ramirestechgames.<id>"` | Working copy (HEAD de cada repo) |
| **WOR e futuros jogos** | Git submodule em `Packages/com.ramirestechgames.<id>` | Tag `vX.Y.Z` conhecida |

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
5. O `COMPATIBILITY.md` registra conjuntos de versões validados juntos na Sandbox.

## Remotos (pendente de decisão do owner)

Proposta: um repositório GitHub privado por package (`ramirestech-games/com.ramirestechgames.<id>`)
e um para governança. Os repositórios locais já estão inicializados; criar os remotos é ação do owner.
