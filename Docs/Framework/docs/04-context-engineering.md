# 04 — Template de Context Engineering

Objetivo: qualquer agente (Claude Code, Codex) ou engenheiro consegue trabalhar em **um** repositório
sem depender do histórico de conversas. Cada documento tem um leitor e uma pergunta que responde.

| Arquivo | Leitor | Responde | Atualizar quando |
|---|---|---|---|
| `README.md` | Game developer usando o package | O que é? Como instalo? Como uso no Inspector? Quais samples? | Feature visível ao usuário muda. |
| `CLAUDE.md` | Agente/engenheiro alterando o package | O que posso/não posso fazer aqui? Como compilo e testo? Quando estou pronto? | Regras, comandos ou limites mudam. |
| `AGENTS.md` | Codex e outros | Ponteiro para `CLAUDE.md`. | Nunca (só o ponteiro). |
| `ARCHITECTURE.md` | Quem vai mudar a estrutura | Quais camadas e componentes existem, como os dados fluem, em que ordem rodam? | Estrutura interna muda. |
| `CONTRACTS.md` | Quem consome o package (outros packages, jogos) | Qual API é pública, quão estável é, quais eventos e extension points existem? | **Toda** mudança de API pública. |
| `TESTING.md` | Quem vai testar ou revisar | Como rodo os testes? O que está coberto? O que falta? | Testes adicionados ou estratégia muda. |
| `ROADMAP.md` | Quem planeja | Em que milestone estamos? O que falta para o próximo? | Ao fechar/abrir milestone. |
| `CHANGELOG.md` | Quem atualiza versão no jogo | O que mudou, o que quebrou, como migrar? | Todo merge relevante (seção `Unreleased`). |
| `Documentation~/adr/NNNN-*.md` | Quem questiona uma decisão | Por que é assim? Quais alternativas foram descartadas? | Decisão de arquitetura não trivial ou reversão. |

## Regras de escrita

1. **CLAUDE.md é curto e operacional** (≤ ~150 linhas). Ele aponta para os outros documentos em vez de repeti-los.
2. **CONTRACTS.md é a fonte da verdade da API pública.** Tipo público fora dele é bug de documentação.
3. Cada contrato tem nível de estabilidade:
   - `Stable` — muda somente com MAJOR.
   - `Experimental` — pode mudar em MINOR (documentado no CHANGELOG).
   - `Internal` — não use fora do package.
4. Exemplos de uso no README são para **Inspector primeiro**, código depois.
5. Documentação em pt-BR; identificadores, XML docs e comentários de código em inglês.
6. Nunca documente nomes do jogo consumidor (WOR) dentro de um package.

## Seções obrigatórias do CLAUDE.md de package

```
# CLAUDE.md — <package>
1. Propósito (1 parágrafo)
2. Leia antes de alterar (ordem dos docs)
3. Limites: o que este package NÃO faz
4. Dependências permitidas (hard / optional) — fonte: dependency-graph.json
5. Regras essenciais (resumo de CONVENTIONS.md)
6. Comandos: compilar, testar, validar dependências (Unity CLI + Sandbox)
7. Definition of Done (Quality Gates)
8. Armadilhas conhecidas
```

## ADR (Architecture Decision Record)

Formato curto (template em `Documentation~/adr/0000-template.md`):
`Status · Contexto · Decisão · Alternativas consideradas · Consequências`.
ADRs do framework (transversais) vivem em `ramirestech-framework/docs/adr/`;
ADRs de um package vivem no próprio package.

## Template

Os arquivos-modelo estão em [`templates/package/`](../templates/package/) com tokens `{{...}}`
substituídos por `tools/new_package.py`.
