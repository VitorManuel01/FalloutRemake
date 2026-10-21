# Saída do NPC: estado transacional e projeção

`TempleNpcDepartureProfile` vincula o ensaio aos hashes do MAP, dos programas,
do GAM e dos metadados já conferidos. Requer o resultado Speech aceito. Não é
uma VM de INT nem uma alteração do checkpoint ativo.

Um snapshot imutável reúne fase, índice do percurso e ticks restantes. Posição,
rotação, visibilidade, bloqueador e estado da porta são derivados desse mesmo
snapshot. Cada passo muda o conjunto inteiro; token/revisão antigos ou um
destino ocupado recusam a operação sem alteração. Restaurar valida o conjunto
antes de criar uma sessão com token novo.

O percurso usa três trechos: 13728 → 19728 → 22712 → 28715. Na chegada ao
primeiro destino, abre a porta e agenda 20 ticks. O avanço é manual e para no
evento; sobra de tempo não move o NPC. Ao final ele fica oculto e sem bloqueio,
mantendo chave e ID originais. O grafo do jogador continua parando nas saídas;
o grafo do NPC preserva esses marcadores e permite cruzá-los.

Validação: 281 grupos sintéticos e o relatório local
`--temple-npc-departure-report`. O relatório exige a auditoria independente dos
opcodes, percorre 91 passos adjacentes (32/28/31), restaura 93 estados, verifica
recusas por ocupação/repetição, atravessa dois hexes de saída e preserva os
22 marcadores. O checkpoint ativo permanece idêntico byte a byte e o MAP é
reconstruído sem diferenças. Resultado local:
`.local/temple-npc-departure-report.json`.

Próximo: persistir este ensaio em arquivo separado com leitura estrita e
substituição atômica, depois disponibilizar os controles na consulta Speech.
