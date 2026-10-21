# Avanço automático opcional da saída

Depois de aplicar Speech à partida, **Avançar automaticamente** executa os
passos e a espera sem repetir os botões. Começa desligado. O ritmo provisório é
cinco hexes por segundo; não pretende reproduzir a duração das animações do CE.
A espera conserva os 20 ticks verificados do evento, em dez ticks por segundo.

Cada hex continua passando pela transação e pelos bloqueadores atuais. O NPC
aguarda se o jogador ocupar o próximo destino. O modo pausa durante janelas,
caminhada do jogador ou outra elevação/área; o tempo da pausa não se acumula.
O trabalho após um travamento é limitado a 16 operações por quadro. Malha e
seleção só refletem passos efetivamente aceitos.

**K** interrompe a reprodução e salva os hexes/fases/ticks inteiros no v6.
Carregar mantém a reprodução desligada; o usuário pode retomá-la. Frações
visuais ainda não completadas são descartadas ao parar, sem criar passos ou
ticks. O relógio do explosivo continua separado. A ocultação final desliga a
reprodução e libera o hex do NPC.

Validação: **299 grupos sintéticos** (+4 do relógio), bateria **18/18** em
`.local/temple-validation-20261021-033501-197e5e77/manifest.json`, builds sem
avisos/erros. `TEMPLE_NPC_AUTOMATIC_OK` confere primeiro intervalo fracionário,
pausas, limite de catch-up, espera parcial, K/R e final oculto, preservando os
dois itens e os 25 XP reais. As instâncias do usuário não são usadas.
Captura isolada 1024×768 aprovada: `.local/npc-playback-isolated.json`,
`Passed=true`, `LogFailure=null`, desktop de entrada `Default` antes/depois.
