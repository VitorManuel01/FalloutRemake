# Percurso do NPC e ponto de obstrução

**Percurso NPC**, junto aos controles de caminhada, desenha em lilás apenas o
trecho atual da saída e enquadra seus pontos no espaço livre dos painéis.
Um hex vermelho indica a primeira obstrução, inclusive o próprio jogador.
A consulta não move personagens, muda estado ou carrega outra área.

O traçado acompanha os passos aceitos e a posição do jogador. Durante a espera
fica temporariamente oculto e reaparece no trecho seguinte; a câmera permanece
onde o usuário a deixou. Desative/ative o botão para enquadrar novamente. Ocultar
criaturas também oculta esse traçado. Ao final da sequência, ele é desativado.
As malhas do percurso não têm colisão nem identidade de objeto original.

A consulta do domínio devolve arrays próprios e conserva bloqueadores, saídas e
chaves. O enquadramento usa o mesmo cálculo validado para a rota do jogador,
incluindo a porta final quando esse for o destino do trecho.

Validação: **302 grupos sintéticos** (+3), bateria **18/18** em
`.local/temple-validation-20261021-034344-30d179ab/manifest.json`, builds sem
avisos/erros e `TEMPLE_NPC_ROUTE_OK`. A integração confere o hex do jogador em
vermelho, três rotações, filtros e igualdade do checkpoint. Captura isolada
1280×720 aprovada: `.local/npc-route-isolated.json`, logs limpos e entrada
`Default` preservada.
