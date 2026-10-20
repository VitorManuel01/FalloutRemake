# Política explícita de travessia das saídas

`HexNavigation` conserva como padrão `StopAtExits`: o jogador chega à saída e
precisa confirmar a viagem. Somente um grafo construído explicitamente com
`ContinueAcrossExits` pode continuar a busca através desses hexes.

Essa distinção segue a evidência local do CE em `src/object.cc`, bloco
`if (obj == gDude)` que lê a saída e agenda a transição. Não habilita viagem
para NPCs nem executa a saída do NPC na prévia. Todos os grafos do mundo ativo
continuam usando o padrão anterior.

O relatório posterior agora conserva os marcadores de saída, todos os
bloqueadores e sua identidade. A perna 22712 → 28715 do NPC passa em 31 passos;
o mesmo percurso com a política do jogador continua inalcançável. Contagens
de saídas e hexes bloqueados são idênticas entre os dois grafos.

Dois grupos sintéticos novos verificam travessia/recusa com as mesmas fontes,
identidade das saídas, obstáculos mantidos e enumeração desconhecida recusada.
Total: **275 grupos aprovados**. O relatório real também passou.

Próximo: representar posição/fase/bloqueador do NPC e evento nativo em um
checkpoint restaurável antes de integrar os efeitos da conversa à partida.
Modelo recomendado: GPT-6.1 Sol, esforço Alto.
