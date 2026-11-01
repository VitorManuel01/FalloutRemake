# Fuga no combate do Templo: as criaturas não fogem, o jogador sai

01/11/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

## O que o original faz

### As criaturas do Templo lutam até morrer

- **Fato verificado (dados originais):**
  - `zclrat` (formigas) e `zclscorp` (radscorpions) trocam o pacote de IA em `map_enter_p_proc`, com `critter_add_trait(self, 1, 5, n)`:
    - formigas: pacote 7 (`[Rat]`), time 3;
    - radscorpions: pacote 8 (`[Scorpion]`), time 4.
    - No CE, o traço 1/5 é `CRITTER_TRAIT_OBJECT_AI_PACKET` e o 1/6 é o time.
  - No `data/ai.txt`, os dois pacotes têm `min_hp=0`, `min_to_hit=0`, `hurt_too_much=blind`, `max_dist=15` e nenhum `run_away_mode` nem `chem_use`.
- **Fato verificado (código do CE, `combat_ai.cc`):**
  - `_combat_ai` só manda fugir (`_ai_run_away`) em três casos:
    - a criatura já está fugindo;
    - o dano dela casa com `hurt_too_much`;
    - o HP atual é menor que `ai->min_hp`, um valor absoluto que só `aiSetRunAwayMode` recalcula e que nenhum dos dois scripts chama.
  - Com `min_hp=0`, a terceira condição nunca vale. A cegueira só vem de golpes críticos, e o CE não os permite antes do dia 1 de jogo ([skill-spending](skill-spending-2026-10-31.md)).
  - "I need DRUGS" (`_ai_check_drugs`) vale só para bípedes.
  - "Can't possibly hit" exige chance menor que `min_to_hit`, que aqui é 0.
- **Conclusão:** no Templo, formigas e radscorpions nunca fogem. A IA provisória, que se aproxima e ataca até o fim, já está fiel nesse ponto. A afirmação da [auditoria](temple-run-audit-2026-10-31.md) de que o original seria "mais generoso" por ter criaturas que fogem estava **errada** e foi corrigida.

### O jogador sai do combate com Enter

- **Fato verificado (código do CE, `combat.cc`):** no turno do jogador, Enter chama `combatAttemptEnd`.
  - A tentativa é recusada (`combat.msg` 103) se algum inimigo da luta ainda quer lutar (`!_combatai_want_to_stop`).
  - `_combatai_want_to_stop` só é verdadeiro quando a criatura está fugindo ou desengajando, ou quando o personagem está fora de `isWithinPerception`. Dentro de um combate, isso significa:
    - mais de **PE × 5 hexes** no cone frontal da criatura;
    - mais de **PE × 2 hexes** fora dele;
    - **sem** exigir linha de visão.
  - Formigas e radscorpions têm PE 2: o personagem precisa estar a mais de 10 hexes à frente deles, ou a mais de 4 hexes fora do cone.
- **Fato verificado (direção das criaturas):** no CE, a rotação muda quando a criatura anda (cada passo da animação) e quando ataca (`animationRegisterRotateToTile` em `_ai_attack`). Uma criatura que persegue o personagem costuma ficar de frente para ele.
- **Fato verificado (depois da saída):** as criaturas continuam onde pararam, com o HP que sobrou. Elas voltam ao `critter_p_proc` e atacam de novo se virem o personagem pela regra de [percepção](critter-sight-2026-11-01.md).

## O que o projeto faz

| Peça | Arquivo | Regra |
| --- | --- | --- |
| Direção | `CombatantState.Rotation`, `CombatSession` | Começa na rotação do MAP. Passa a ser a direção do último passo andado, ou a direção do alvo atacado. Vale também para o jogador. |
| Saída | `CombatActionKind.LeaveCombat`, `CombatSession.AwareEnemies` | Só o jogador pode sair. A saída é recusada (`EnemiesAware`) enquanto algum inimigo ainda o percebe pela regra de combate de `CritterSight.WithinPerception`. Saindo, a luta termina sem vencedor (`CombatLeft`, `Turn.WinningTeam = null`, `CombatSession.Left`). |
| Sobreviventes | `WorldSession.Survivors`, `SurvivingCritter` | Ao confirmar uma luta abandonada, cada criatura viva fica registrada com hex, HP e rotação. O XP das mortes da luta é pago normalmente. |
| Projeção | `EffectiveObject` | O sobrevivente aparece no hex onde parou, com a rotação e o HP dele. O hex de origem fica livre. `FindEncounter` olha a partir dali, e uma nova luta começa com esse HP. Se ele morrer depois, vira cadáver e sai da lista. |
| Checkpoint | `SavedCombat.Survivors` | Campo opcional dentro do v13: arquivos antigos carregam sem sobreviventes. Na restauração, é recusado o sobrevivente que: está duplicado, já está morto, tem outra chave, PID ou elevação, tem HP fora de 1..máx ou rotação fora de 0–5, divide o hex com outro sobrevivente, ou está sobre uma grade de saída ou geometria bloqueada. |
| Duelo | `CommitCombat`, prévia | O desafio de Cameron não pode ser abandonado: o portão fica trancado atrás dos dois. |
| Prévia | `CombatViewAdapter`, `CombatHudBar` | Enter, ou o botão "Sair do combate [Enter]", no turno do jogador. A recusa lista quem ainda percebe o personagem e a que distância. |

## Diferenças conhecidas

- **Guarda mantida:** uma criatura que vê o personagem mas não tem caminho até ele continua não iniciando luta. Agora já existe uma saída, mas a guarda foi mantida porque a auditoria e a IA provisória não sabem sair, e uma luta sem caminho travaria a medição.
- **A política da auditoria nunca sai do combate.** Os números da auditoria continuam medindo lutas até o fim.
- **Fora do escopo:**
  - criaturas fora da luta que entram nela (`_combatai_want_to_join`);
  - desengajar por `max_dist`;
  - o anel de criaturas não combatentes que `combatAttemptEnd` também consulta.

## Validação

- **`--critter-sight-report`** passou a conferir também:
  - o `map_enter_p_proc` dos dois scripts (time 3 e pacote 7; time 4 e pacote 8);
  - os pacotes `[Rat]` e `[Scorpion]` do `ai.txt` (`min_hp` 0, `min_to_hit` 0, `hurt_too_much` blind, sem `run_away_mode` nem `chem_use`);
  - que as 21 criaturas são quadrúpedes (tipo de corpo 1, e não bípedes).
- **Testes sintéticos: 389 grupos** (4 novos em `CombatLeaveTests`):
  - Saída:
    - recusada com a criatura de frente a 10 hexes, ou de costas a 4;
    - aceita a 11 hexes de frente, ou a 5 de costas;
    - depois de sair, a luta termina sem vencedor e nenhuma ação é mais aceita;
    - só o jogador pode sair.
  - Direção: a criatura fica virada pelo último passo e pelo golpe; o jogador também.
  - Sobrevivente:
    - fica no hex onde parou, com HP e rotação, e libera o hex de origem;
    - não percebe o personagem de longe;
    - passa pelo checkpoint, que recusa 9 variações forjadas;
    - uma nova luta começa com o HP dele;
    - `Reset` o devolve à origem;
    - morto numa luta seguinte, vira cadáver e sai da lista.
- **`tools/Validate-Temple.ps1`: 21 passos aprovados.**
  - Em `COMBAT_SLICE_OK`, Enter é recusado no primeiro turno do jogador, ao lado das formigas.
  - Em `COMBAT_LEAVE_OK`, numa luta aberta contra a criatura alcançável mais distante:
    - o radscorpion 154736 anda do hex 13071 até o 13476, a 71 hexes, com rotação 3;
    - Enter encerra a luta;
    - o radscorpion aparece onde parou;
    - salvar, reiniciar e carregar o mantém lá.
- **Auditoria do Templo:** não foi remedida. A política dela nunca sai do combate, e sem sobreviventes `FindEncounter` e `BeginCombat` leem os mesmos objetos de antes.
