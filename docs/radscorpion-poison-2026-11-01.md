# Veneno do radscorpion

01/11/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

## O que o original faz

- **Fato verificado (bytecode original, `zclscorp.int`, `combat_p_proc` no offset 3350):**

  ```
  if fixed_param == 2 and target_obj == dude_obj
      and not is_success(do_check(dude_obj, 6, -1)) then
      poison(target_obj, random(1, 6))
  ```

  - O stat 6 é **Sorte** (`STAT_LUCK`), não Resistência. A descrição anterior desta pendência estava errada.
  - `zclrat` (formigas) não tem `combat_p_proc`.
  - O `--critter-sight-report` confere as 25 instruções.
- **Fato verificado (código do CE):**
  - `_combat_attack` (`combat.cc`) roda o `combat_p_proc` do atacante com parâmetro 2 depois de cada acerto em uma criatura que estava viva antes do ataque, inclusive acerto com 0 de dano.
  - `statRoll` (`stat.cc`): sucesso quando `random(1, 10) <= stat + modificador`. Com Sorte 5, o personagem resiste com 1–4 (40%) e é envenenado com 5–10 (60%).
  - `critterAdjustPoison` (`critter.cc`), que só vale para o personagem:
    - a dose perde `dose × Resistência a Veneno / 100`;
    - o novo nível recoloca o evento de veneno na fila para daqui a `10 × (505 − 5 × nível)` ticks.
  - Resistência a Veneno é `5 × Resistência` (`stat.cc`); com o trait Fast Metabolism, é 0 (`trait.cc`). O projeto já calcula isso em `PlayerCharacter.PoisonResistance`.
  - `poisonEventProcess`:
    - nível −2 (com reagendamento, ou cura em 0);
    - −1 HP, e `critterAdjustHitPoints` mata em 0.
  - Tempo de jogo: 10 ticks por segundo. Fora do combate, avança 1 tick a cada 100 ms reais (`_script_chk_timed_events`). Em combate, avança 5 s ao fim de cada rodada (`_combat_sequence`), e a fila processa os eventos vencidos.
- **Consequência:** um nível 5 tira 1 HP depois de 480 s de jogo, e outro 490 s depois. O veneno pesa pouco numa passagem rápida pelo Templo, mas acumula se o personagem parar ou for picado muitas vezes.

## O que o projeto faz

| Peça | Arquivo | Regra |
| --- | --- | --- |
| Regras | `src/Runtime/Combat/PoisonRules.cs` | `PoisonRules` (atraso, absorção, teste de Sorte −1, dose 1–6) e `PoisonState` (nível e ticks até o próximo evento). `Advance` serve para a exploração, tick a tick. `AdvanceRound` serve para o combate: 50 ticks de uma vez, com o evento vencido reagendado a partir do fim da rodada. |
| Picada | `CombatSession` | A cada acerto de um combatente `PoisonsOnHit` no jogador, rola Sorte em 1–10 contra LK − 1. Se falhar, rola a dose 1–6, aplica a resistência e registra `PoisonChecked`. |
| Rodadas | `CombatSession.RoundElapsed` | O veneno age no fim de cada rodada e registra `PoisonDamage`. Se matar, gera `CombatantDied` com `KillerId` `poison`. |
| Mundo | `WorldSession.Poison`, `AdvanceTime`, `BeginCombat`, `CommitCombat` | O veneno entra e sai das lutas, inclusive do desafio de Cameron. Só o radscorpion (PID `0x01000005`) pica. `AdvanceTime` roda o tempo de exploração. Um evento letal é informado (`Died`) sem ser aplicado, e quem chamou encerra o jogo. |
| Checkpoint | `SavedCombat.Poison` | Campo opcional dentro do v13. Na restauração, nível ou timer fora da faixa são recusados. |
| Prévia | `PoisonPreview.cs`, `CombatViewAdapter`, `TempleDoors` | 1 tick a cada 100 ms fora do combate, pausado com janelas de inspeção abertas. O log de combate mostra a picada e o dano, e o HUD mostra "envenenado (nível)". Morrer envenenado reinicia a prévia. |
| Auditoria | `WalkTiming.cs`, `TempleRunAudit` | Cada hex andado custa o tempo de caminhada do FRM original: `hmwarrab.frm` 3,78 ticks (0,38 s), `hfprim` 4,36 ticks, a 10 fps e 8 quadros, pelo `_object_move` do CE. As esperas do explosivo e de Cameron contam como tempo; o tempo parado do jogador não conta. |

## Diferenças conhecidas

- **Inferência técnica:** o CE processa a fila de eventos durante o jogo inteiro. Aqui, o tempo de exploração para enquanto uma janela de inspeção (bolsa, ficha, diálogo) está aberta. Não verifiquei se o CE pausa o tempo nessas telas.
- **Fora do escopo:**
  - mensagens originais do `misc.msg` (3000–3003);
  - antídoto e Fast Metabolism em combate (o valor da resistência já respeita o trait);
  - veneno em outras criaturas: o CE só envenena o personagem.

## Validação

- **Testes sintéticos: 393 grupos** (4 novos em `PoisonTests`):
  - absorção, atraso, eventos e rodadas;
  - picada com Sorte 5 (rolagem 5 envenena: dose 6, que vira 5 depois de 25% de resistência; rolagem 4 resiste);
  - erro e formiga não picam;
  - dano no fim da rodada e morte por veneno em combate;
  - veneno levado para fora da luta, tempo de exploração (4799 ticks sem efeito; o 4800º tira 1 HP);
  - checkpoint, com 4 venenos forjados recusados;
  - evento letal informado sem ser aplicado;
  - `Reset` cura.
- **`--critter-sight-report`** confere o `combat_p_proc` de `zclscorp` e a ausência dele em `zclrat`.
- **`--walk-timing-report`:** `hmwarr` 378 ms por hex e `hfprim` 436 ms por hex.
- **`tools/Validate-Temple.ps1`: 21 passos aprovados.** Em `POISON_OK`:
  - nível 3, com o evento a 5 ticks, perde 1 HP depois de 0,5 s de exploração;
  - o nível 1 resiste a salvar, reiniciar e carregar;
  - o evento seguinte cura;
  - um evento letal reinicia a prévia.
- **Auditoria do Templo** (`--temple-run-report`, 20 sementes, comparada com a [medição da percepção](critter-sight-2026-11-01.md)):

| Personagem | Rota direta | Com kit | Envenenamentos por rodada (kit) | HP perdido para o veneno |
| --- | --- | --- | --- | --- |
| Neutro (Speech) | 15/20 (15) | 0/20 (0) | 4,05 | 0 |
| Narg (duelo) | 18/20 (18) | 20/20 (16) | 1,75 | 0 |
| Mingan (duelo) | 8/20 (8) | 3/20 (1) | 3,2 | 0 |
| Chitsa (Speech) | 11/20 (11) | 0/20 (1) | 4,75 | 0 |

- **Leitura:**
  - **Fato medido:** o veneno não tirou HP em nenhuma rodada.
  - **Inferência técnica:** entre o primeiro envenenamento e a saída para Arroyo, cada rodada contou menos tempo que o atraso do primeiro evento. Um nível 5 precisa de 4800 ticks, o equivalente a uns 1.270 hexes andados a 3,8 ticks por hex.
  - **Fato medido:** as rotas diretas não cruzam radscorpions, então não mudaram.
  - **Inferência técnica:** as diferenças nas rotas com kit vêm das rolagens de Sorte e de dose, que mudam a sequência aleatória da mesma semente, e não do dano do veneno. Com 20 sementes, isso é variação.
  - **Inferência técnica:** um jogador que para ou explora devagar sente o veneno. A auditoria não conta o tempo parado; conta só o andar, as rodadas e as esperas do explosivo e de Cameron.
- **`tools/Run-QuietGodot.ps1` (desktop isolado):** aprovado em 271 s, com `POISON_OK` entre os marcadores exigidos.
