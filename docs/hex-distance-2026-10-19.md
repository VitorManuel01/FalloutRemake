# Marco 44 — distância lógica e alcance de referência

`FalloutGrid.Distance` calcula distância sem obstáculos usando coordenadas
cúbicas equivalentes à paridade dos seis vizinhos importados. Não usa metros
ou distância na tela. A distância não é o número de passos de uma rota com
portas/bloqueadores; `HexNavigation` continua responsável pela caminhada.
Tiles de inventário (-1) e coordenadas fora do tabuleiro são recusados.

Validação: 159 grupos sintéticos aprovados. O resultado confere com BFS sem
obstáculos para amostras a partir das quatro bordas e de ambas as paridades,
com simetria e vizinhos de distância um. Uma referência independente baseada
no algoritmo angular `tileDistanceBetween` do CE confere 1000 pares na região
interna e toda a vizinhança da barreira. O CE angular pode percorrer posições
fora do tabuleiro em pares extremos; não declaramos equivalência global com
esse comportamento nem reproduzimos seu sentinela 9999.

`--temple-barrier-report` também enumera hexes alcançáveis perto da barreira
usando raio de referência 3. Esse é o padrão CE de `weaponGetRocketExplosionRadius`,
usado no despacho de dano ao cenário por `combat.cc:5961`/`scripts.cc:2879`.
Overrides de configuração/Sfall não foram aplicados a essa referência.
A consulta não posiciona, ativa ou consome explosivos, e não simula dano a criaturas.

Próximo: conferir ativação, temporizador e remoção do explosivo no CE antes de
definir uma ação jogável e seu checkpoint. GPT-6.1 Sol, esforço Alto.
