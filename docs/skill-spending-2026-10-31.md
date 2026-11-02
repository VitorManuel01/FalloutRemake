# Gasto de pontos de perícia, descanso e críticos no Templo

31/10/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

Depois da [criação de personagem](character-creation-2026-10-31.md), os pontos de
perícia de cada nível podem ser gastos na ficha, com as regras do editor original.
Antes disso, conferi no Community Engine (`alexbatalov/fallout2-ce@e97087b`) duas
lacunas apontadas antes, descanso e críticos. As duas não existem no Templo
original.

## Descanso: não existe no Templo

- **Pip-Boy desligado:** o `data/maps.txt` original marca `arcaves` (mapa 3) com
  `pipboy_active=No`. No CE, `pipboyOpen` recusa a abertura enquanto
  `wmMapPipboyActive()` for falso, e essa função só lê se o filme do Vault Suit já
  passou (`gameMovieIsSeen(MOVIE_VSUIT)`). O traje é entregue em Arroyo, depois do
  Templo.
- **Sem descanso no mapa:** `arcaves` também tem `can_rest_here=No,No,No`.
  `_critter_can_obj_dude_rest` recusa o descanso nessas elevações se houver criatura
  viva de outro time.
- **Quando houver descanso:** a regra está anotada para depois. A cada 180 minutos
  acumulados numa abertura do Pip-Boy, `_partyMemberRestingHeal(3)` cura
  `taxa de cura` HP. "Até curar" descansa `HP faltando / taxa × 3` horas em blocos de
  24 h, depois em blocos de 3 h.

Fato verificado: o Mingan do original também não tem como descansar antes do
desafio de Cameron. A observação do [documento do duelo](temple-duel-2026-10-31.md)
foi corrigida.

## Críticos: inativos no primeiro dia de jogo

- **Regra do CE:** `randomTranslateRoll` (`random.cc`) só transforma um acerto em
  crítico, ou um erro em falha crítica, quando `gameTime / GAME_TIME_TICKS_PER_DAY >= 1`.
  O sfall pode remover esse limite (`remove criticals time limits`), mas o padrão
  mantém.
- **Relógio:** o jogo começa em 302400 ticks (dia 0, 8:24) e um dia tem 864000.
  Fora do combate, o relógio anda 1 tick a cada 100 ms reais
  (`_script_chk_timed_events`). Cada rodada de combate soma 5 s
  (`_combat_sequence`).
- **No Templo:** o dia 1 só chega depois de cerca de 15,6 horas de jogo dentro dele.
  Sem críticos, o combate do projeto está fiel ao Templo.
- **Falhas críticas do jogador:** `attackComputeCriticalFailure` ainda exige 6 dias de
  jogo.
- **Arrombamento:** já usava esse limite (`PreviewSkillResolver`, críticos desligados
  no primeiro dia).

A auditoria de 31/10 tratava os críticos como algo que tornaria o original "mais
generoso". Isso está corrigido em [temple-run-audit](temple-run-audit-2026-10-31.md).
A tabela de efeitos críticos continua necessária, mas só para depois do primeiro
dia.

## Regras de gasto (`src/Runtime/WorldSkills.cs`)

Seguem `skill.cc` (`skillAdd`, `skillSub`, `skillsGetCost`) e
`characterEditorHandleAdjustSkillButtonPressed`.

- **Custo de um ponto pelo valor atual da perícia:**

  | Valor atual | Custo |
  | --- | --- |
  | até 100% | 1 |
  | 101–125% | 2 |
  | 126–150% | 3 |
  | 151–175% | 4 |
  | 176–200% | 5 |
  | acima de 200% | 6 |

- **Valor:** cada ponto soma 1 ao valor-base da perícia. Perícia marcada conta o
  valor-base em dobro, então sobe 2% por ponto. O teto é 300%.
- **Mais:** sem pontos, a recusa é a da mensagem 136 do editor. No teto, a das
  mensagens 132/133. Se o ponto custa mais do que resta, nada acontece.
- **Menos:** devolve `custo(valor − 1)`. Numa perícia marcada que muda de faixa, o
  `skillSub` retira um ponto da devolução, e o par mais/menos volta exatamente ao
  ponto de partida. O menos só vale acima do valor que a perícia tinha quando a ficha
  abriu (mensagens 134/135), e o que foi comprado numa abertura anterior é definitivo.
- **Concluir ou cancelar:** "Pronto" aplica o gasto. "Cancelar" descarta tudo, como a
  restauração do editor.
- **Pontos não gastos:** cada nível soma `5 + 2 × IN` (Skilled +5, Gifted −5), e o
  total guardado não passa de 99. O excedente se perde, como no `pcSetStat` do CE. O
  `WorldSession` concede os níveis um a um quando lê o total.
- **Durante a luta:** a ficha não muda (`SkillCommitStatus.InCombat`). É regra do
  projeto: o combate tira uma cópia da ficha quando começa.

### Uso no mundo

`WorldSession.Sheet` é a ficha com os pontos comprados. Ela alimenta o combate
(`DerivedCombatStats.ForPlayer`), o desafio de Cameron e o arrombamento.
`Character` continua sendo a ficha de criação ou do GCD, a mesma que o checkpoint
compara com o arquivo original.

### Checkpoint v13

O bloco `Skills` guarda os pontos comprados por perícia e o total sem gastar. Na
restauração:

- `total sem gastar` deve ficar entre 0 e 99;
- o custo dos pontos comprados, refeito ponto a ponto desde a ficha de criação, mais o
  total sem gastar não pode passar do que os níveis concederam;
- checkpoints v1 a v12 entram sem nada gasto, com os pontos do nível atual.

## Interface

- **Ficha [H]:** botão no HUD e tecla H, que abre a ficha do personagem em jogo. O
  botão de escolher personagem virou "Novo jogo [N]".
- **Janela:** mostra nível, XP, HP, SPECIAL e os pontos por nível. Cada uma das 18
  perícias aparece com valor, custo do próximo ponto, menos e mais. A janela explica
  cada recusa.
- **HUD:** mostra "N pontos de perícia [H]" enquanto houver pontos.

## Validação

- **Testes sintéticos:** 380 grupos, 4 novos (`SkillSpendingTests`).
  - Faixas de custo.
  - Mais, menos e o valor de abertura.
  - Aplicação só em "Pronto"; Reset descarta a ficha aberta.
  - Perícia marcada +2% e a devolução entre faixas, recusa durante a luta.
  - Teto de 99, checkpoint v13 e falsificações.
- **`--preview-test` (`SKILL_SHEET_OK`):**
  - um mundo com as criaturas do Templo mortas chega ao nível 2;
  - H abre a ficha e o menos é recusado;
  - mais e menos ficam pendentes até "Pronto", que sobe o Arrombamento;
  - o checkpoint v13 guarda o gasto, e uma nova abertura não devolve a compra;
  - "Cancelar" descarta.

## Fora do escopo

- Perks, que o nível 3 oferece, e Tag!/Educated.
- Relógio de jogo visível, descanso e cura por tempo: dependem do Pip-Boy, que vem
  depois do Templo.
- A tabela de efeitos críticos, para depois do primeiro dia.
