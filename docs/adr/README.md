# ADRs do framework

Decisões transversais. ADRs específicas de um package ficam em `Documentation~/adr/` do próprio package.

| # | Título | Status |
|---|---|---|
| [0001](0001-package-per-domain.md) | Um Unity Package por domínio, um repositório por package | Aceita |
| [0002](0002-optional-integration-assemblies.md) | Integração entre packages por integration assemblies opcionais | Aceita |
| [0003](0003-execution-order-bands.md) | Ordem de execução por bandas definidas no Core | Aceita |
| [0004](0004-command-buffers.md) | Command buffers por domínio: Player e AI compartilham execução | Aceita |
| [0005](0005-naming-namespaces-assemblies.md) | Nomes de packages, namespaces e assemblies | Aceita |
| [0006](0006-sandbox-file-references.md) | Sandbox com referências `file:`; jogos com submodules | Aceita |

Template: [`templates/package/Documentation~/adr/0000-template.md`](../../templates/package/Documentation~/adr/0000-template.md).
