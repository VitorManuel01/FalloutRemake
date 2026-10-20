# Depois da aceitação de Speech: sequência original

O relatório `--temple-aftermath-report` valida MAP, ator, porta e programas locais
por hash, reconstrói os bytes do MAP e confere os operandos físicos das fases.
Não executa INT nem altera o mundo ou o ensaio salvo.

Após Node015a definir local 13=2, `critter_p_proc` usa local 14 como fase:

1. Fase 0: destranca a porta final e pede ao NPC que corra até 19728.
2. Ao chegar: define fase 1, pede abertura da porta e agenda evento de parâmetro
   1 em `game_ticks(2)`, equivalente a 20 ticks nativos.
3. O evento define fase 2. A próxima atualização pede corrida até 22712;
   a chegada define fase 3.
4. A fase 3 pede corrida até 28715; a chegada define fase 4 e oculta o NPC.

Não é destruição do objeto: o handler CE `set_obj_visibility(self,1)` oculta o
critter e adiciona NO_BLOCK. `animate_move_obj_to_tile` com flag 1 solicita corrida;
se o critter estiver inativo ou em combate, o handler não agenda a animação.
Pedir movimento ou abertura não prova que a animação terminou com sucesso.

O relógio desse evento não está ligado ao relógio manual do explosivo da prévia.
Integrá-los por suposição criaria uma transição indevida. Tampouco se deve mover
só a malha: a identidade fonte, a posição efetiva, o bloqueador e a fase precisam
ser persistidos e restaurados juntos.

Nenhum opcode direto `give_exp_points` foi encontrado nos programas completos
`actemvil.int` e `arcaves.int` desse perfil. Isso não prova ausência de recompensa
em outros programas ou transições; nenhum valor de XP foi inventado.

Referência local: Fallout 2 CE `e97087b9582f37075db347a89898887320753f8b`,
`src/interpreter_extra.cc`: registros 0x80CE/0x80F0/0x80F2/0x80E3/0x812F/0x8131,
handlers de movimento, evento, conversão de ticks, visibilidade e porta.

O relatório inclui auditoria de rotas na geometria da prévia, retirando somente o
bloqueador do próprio NPC e abrindo a porta na segunda/terceira perna. Não é uma
execução do pathfinder ou das animações do CE. Dados e evidência permanecem em
`.local/temple-aftermath.json`; nenhum asset original entra no Git.
As duas primeiras pernas passam nessa geometria (32 e 28 passos); a terceira,
22712 → 28715, resulta `Unreachable`. Esse resultado é um bloqueio de integração
a investigar, não licença para teletransportar o NPC ou remover obstáculos.
O diagnóstico isolou a diferença: ao retirar somente os marcadores de saída do
grafo privado de auditoria, a terceira perna passa com 31 passos, sem remover
nenhum bloqueador. A navegação atual encerra a busca ao chegar a uma saída,
regra adequada ao fluxo explícito de viagem do jogador. Isso explica a recusa
na prévia. A confirmação nativa está em `src/object.cc`, no bloco
`if (obj == gDude)` que lê a exit grid e agenda `mapSetTransition`: essa viagem
é do jogador. A navegação ativa não foi alterada neste relatório.

Próximo marco: definir estado persistido e reposicionamento do bloqueador do NPC,
com eventos de execução única, cancelamento e rollback, antes de habilitar a
aceitação de Speech na partida ativa. GPT-6.1 Sol, esforço Alto.
