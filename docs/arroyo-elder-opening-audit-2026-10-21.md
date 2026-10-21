# Abertura da anciã: guardas e dependências

`--arroyo-elder-report` valida a identidade original já registrada, os hashes do
MAP/INT/GAM/scripts.lst, as instruções das guardas e os IDs das mensagens.
Não executa conversa nem exporta texto proprietário. A validação geral inclui
esse relatório como etapa própria; a reconstrução do MAP permanece idêntica.

Achados na instalação local:

- scripts.lst índice 10 declara **10 locais** (0–9). Há leituras de L10/L11 em
  outros caminhos; não aumentar silenciosamente essa declaração. A saudação
  inicial usa L1, L4 e L5 dentro do limite.
- G0 (`GVAR_PLAYER_REPUTATION`) começa em **0** e recebe **+15** na primeira
  entrada. G47 (`GVAR_TOWN_REP_ARROYO`) começa em **50**, portanto chega a **65**,
  e não a 15. Esses valores vêm do GAM resolvido, não de zeros presumidos.
- O preâmbulo da conversa recalcula G37–G45. Para karma 15, G41 fica 1. A
  reputação 65 contribui `65/2 = 32` à reação, com divisão inteira.
- G619 já começa em **1** nesta instalação. O ramo inicial só o escreve quando
  vale zero. G531 registra que a anciã foi consultada.
- Em mapa 4, L5=0, L1>=0, L4=0, inteligência efetiva >=4 e dia inteiro <=90,
  a seleção chega a **Node001**, resposta **103**, lista **11**. Antes da
  saudação, o original escreve **L4=1**.
- As opções originais são 104→Node006, 105→Node015 e 106→Node008, offsets
  11804/11836/11868. A prova da abertura não habilita automaticamente os ramos
  seguintes ou o preâmbulo completo de reação.

O relatório local está em `.local/arroyo-elder-entry-audit.json`. A compilação
do inspetor passou sem avisos/erros e o relatório real emitiu
`ARROYO_ELDER_AUDIT_OK`. A etapa seguinte é persistir o subconjunto neutro de
karma/reputação da entrada junto da recompensa única, sem tocar nos assets ou
repetir os incrementos ao retornar/carregar. Modelo: **GPT-6.1 Sol, esforço Alto**.

Semântica conferida no [Community Engine, revisão e97087b](https://github.com/alexbatalov/fallout2-ce/tree/e97087b9582f37075db347a89898887320753f8b):
`src/interpreter.h` (comparações/divisão), `src/interpreter_extra.cc`
(globais, game_time e diálogos). Não foi usado um interpretador geral de INT.
