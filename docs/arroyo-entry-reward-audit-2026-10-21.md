# Auditoria da recompensa de entrada em Arroyo

O inspetor agora oferece `--arroyo-entry-report`; a mesma verificação também
faz parte de `--temple-state-report` na bateria habitual. O relatório lê apenas
os dados instalados e não concede XP, executa filmes ou modifica variáveis.

Fontes verificadas: `maps/arvillag.map`, ID **4**, índice de script **27**,
216200 bytes, SHA256
`0EDCDFF2AFB6FAC7E8203CE9EAE8BA4663D37F3BE112D3EF4713AF3093D8D52A`;
`scripts/arvillag.int`, 5188 bytes, SHA256
`DB21D0CEA597C67D8597D7B98FEB252823DDF27D747FE9C710DB17720E319D48`.
MAP reconstruído byte a byte e GAM original também verificados.

`map_enter_p_proc` começa em **2362**. A condição em **2370–2390** usa
`metarule(14,0)` e salta para **3318** quando falsa, ultrapassando todos os
três ramos de recompensa.

| Condição adicional na primeira entrada | XP base | Escrita em G10 | Chamada XP |
| --- | ---: | --- | ---: |
| G10=0 | 600 | G10=3 | 3112 |
| G10=2, resultado da conversa verificada | 600 | Nenhuma neste ramo | 3218 |
| Demais valores de G10 | 300 | Nenhuma neste ramo | 3276 |

O código verificador confere sequências completas nos pontos de guarda,
comparação, soma, escrita e chamadas, além do censo das três chamadas XP.
O valor do ramo de Speech é **600 XP base**, separado dos 25 XP já presentes
no cenário de teste. O projeto ainda não o aplica à partida.

No [Community Engine consultado](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/interpreter_extra.cc#L3008),
a regra 14 consulta `MAP_SAVED`; a chamada XP passa pelo sistema de personagem.
O [ciclo de gravação do mapa](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/map.cc#L1348)
marca o bit 1. O [cálculo de experiência](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/stat.cc#L686)
inclui Swift Learner, limites e efeitos de nível. Essas referências explicam
por que a guarda não pode ser substituída simplesmente por “G10 deixou de ser 2”.
Pela fórmula nativa de nível, a passagem de nível 1 para 2 exige 1000 XP:
o cenário controlado atual, sem perks, iria de 25 para 625 XP sem cruzar esse
limiar. Isso delimita um primeiro adaptador possível, sem implementar níveis.

Plano do próximo incremento:

1. Persistir entrada efetiva separadamente do cache de áreas. Consultar uma
   saída pode carregar Arroyo antecipadamente e não deve consumir a recompensa.
2. Vincular o prêmio à transição explícita concluída, de forma atômica com
   posição, mapa e registro de primeira entrada; uma falha não aplica parte dela.
3. Para checkpoints v1–v7 com visita anterior desconhecida, migrar de forma
   conservadora sem conceder retroativamente um segundo prêmio.
4. Limitar inicialmente ao personagem neutro comprovado, sem Swift Learner
   ou passagem de nível, até modelar esses efeitos. Não ampliar combate.
5. Testar consulta antecipada, retorno, K/R antes/depois, migração, arquivo
   inválido e falha no destino. Filmes, reputação e demais efeitos ficam
   explicitamente fora do adaptador de recompensa.

Modelo recomendado: **GPT-6.1 Sol, esforço Alto**. Evidência local:
`.local/arroyo-entry-audit.json`. Verificação executada com os dados reais,
build do inspetor sem avisos/erros. Nenhum dado proprietário entrou no Git.
