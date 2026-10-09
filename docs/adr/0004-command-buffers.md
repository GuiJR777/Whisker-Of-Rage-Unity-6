# ADR-0004 — Command buffers por domínio: Player e AI compartilham execução

- **Status:** Aceita (M0)

## Contexto
O Master Prompt exige que Player e AI usem os mesmos sistemas de execução, e que
"Behaviour Tree decide; HFSM executa". Se estados lerem input diretamente, jogador e AI não podem compartilhar
a mesma execução.

## Decisão
1. Cada domínio executável expõe um **command buffer** (componente) com comandos tipados:
   `CharacterCommandBuffer`, `CombatCommandBuffer`, `AbilityCommandBuffer`.
2. **Fontes de comando** escrevem no buffer: adapter de Input System (jogador), nós de ação da BT (AI),
   scripts de cutscene/teste.
3. **Executores** (estados da HFSM, motor, combo runner) só leem o buffer. Não sabem quem escreveu.
4. Comandos "pressed" são consumidos no tick; comandos "held" persistem até a fonte mudar.
   Buffering temporal (jump buffer, input buffer de combo) é responsabilidade do executor, não do buffer.

## Alternativas
- **Interface `IInputProvider` consultada pelos estados:** funciona, mas incentiva lógica de decisão dentro da
  execução e dificulta gravar/reproduzir comandos.
- **BT chamando estados da HFSM diretamente:** acopla AI à HFSM e impede o jogador de usar a mesma HFSM.

## Consequências
- Mesmo prefab pode ser controlado por jogador ou AI trocando só a fonte de comando.
- AI não depende da HFSM no grafo.
- Possível gravação/replay de comandos para testes.
