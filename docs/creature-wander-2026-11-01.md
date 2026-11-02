# Vaguear das criaturas do Templo

01/11/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

Até aqui, as formigas e os radscorpions ficavam no hex e na rotação do MAP, então o cone de visão ([percepção](critter-sight-2026-11-01.md)) nunca mudava. No original elas andam.

## O que o original faz

- **Fato verificado (bytecode original):**
  - **`map_enter_p_proc`** de `zclrat` e `zclscorp`: `add_timer_event(self, game_ticks(random(1, 5)), 0)`.
  - **`timed_event_p_proc` de `zclrat`** (offset 2948):

    ```
    if not combat_is_initialized and (critter_state(self) & 1) == 0 then
        reg_anim_clear(self); reg_anim_begin(1)
        animate_move_obj_to_tile(self, tile_num_in_direction(tile_num(self), random(0, 5), random(1, 3)), 0)
        reg_anim_end
    add_timer_event(self, game_ticks(random(3, 5)), 0)
    ```

  - **`timed_event_p_proc` de `zclscorp`** (offset 3130): igual, exceto que no mapa 3 (`cur_map_index == 3`) ele só mostra uma mensagem de depuração, sem se mover, quando o personagem está em outra elevação.
- **Fato verificado (código do CE):**
  - `game_ticks(n)` vale `n × 10` ticks.
  - `tile_num_in_direction` (`tileGetTileInDirection`) anda passo a passo e para na borda da grade.
  - `animate_move_obj_to_tile` não age em combate nem para criatura inativa. Com o parâmetro 0, ele anda (`animationRegisterMoveToTile`) por `pathfinderFindPath`:
    - é um A* com no máximo 2000 nós abertos e 2000 fechados;
    - o custo é 50 por passo, mais 10 por curva fora de combate;
    - a estimativa usa `_idist` em pixels de tela;
    - o destino bloqueado falha logo;
    - formigas e radscorpions não abrem portas (`canUseDoor` exige bípede ou robô);
    - o personagem bloqueia como qualquer criatura.
  - `_object_move`: a criatura termina virada para a direção do último passo.

## O que o projeto faz

| Peça | Arquivo | Regra |
| --- | --- | --- |
| Pathfinder | `src/Geometry/OriginalPathfinder.cs` | Porta do `pathfinderFindPath` (sem o custo de gosma radioativa, que não existe nos mapas suportados), mais `tileGetTileInDirection` e `tileIsEdge`. |
| Vaguear | `src/Runtime/WorldWander.cs` | Os timers do mapa atual nascem com 1–5 s, em ordem do MAP, na primeira vez que o tempo corre no mapa. Os eventos disparam tick a tick, em ordem do MAP. Cada evento sorteia direção, distância (1–3) e o próximo intervalo (3–5 s). O radscorpion no mapa 3 só sorteia o intervalo enquanto o personagem está em outra elevação. |
| Posições | `WorldSession.Survivors` | A criatura que andou fica registrada como `SurvivingCritter`, com hex, HP e rotação: o mesmo registro dos [sobreviventes de lutas abandonadas](combat-leave-2026-11-01.md). Por isso a projeção, os encontros, as lutas novas e o checkpoint já a enxergam onde ela está. |
| Tempo | `WorldSession.AdvanceTime(ticks, random, reserved)` | O mesmo relógio do [veneno](radscorpion-poison-2026-11-01.md). Não corre durante lutas. Com `random`, as criaturas vagam. `reserved` são os hexes que o personagem está prestes a ocupar. |
| Prévia | `WanderPreview.cs`, `PoisonPreview.cs` | O relógio corre sempre fora do combate (1 tick a cada 100 ms). Quem anda é reposicionado na cena e no layout. O grafo do personagem é refeito antes da próxima rota, nunca no meio dela. Depois de um movimento, o encontro é checado de novo, porque uma criatura pode chegar e ver o personagem parado. As verificações automáticas deixam as criaturas paradas, exceto a do vaguear. |
| Auditoria | `TempleRunAudit` | O tempo do andar, das rodadas e das esperas faz as criaturas vagarem, com semente própria. Se uma criatura ocupar o destino ou um corredor, o personagem espera um segundo de cada vez. |

## Diferenças conhecidas

- **Inferência técnica: movimento instantâneo.** A criatura chega ao destino no instante do evento. No CE, ela anda pela animação durante uma parte dos 3–5 s seguintes. O efeito sobre a percepção é pequeno, porque as distâncias são de 1 a 3 hexes.
- **Inferência técnica: rota reservada.** No CE, a criatura pode cruzar o caminho do personagem, e ele refaz a rota (`_object_move`). Aqui, as criaturas evitam os hexes que ainda estão na rota em andamento.
- **Timers depois de carregar.** O CE guarda a fila de eventos no save. Aqui, carregar ou reiniciar volta aos timers do `map_enter` (1–5 s), e as posições ficam no checkpoint.
- **Mortos e combate.** O timer de uma criatura morta sai da fila. Em combate, o relógio de exploração não corre. O CE ainda dispararia o evento e sortearia o próximo intervalo; essas rolagens não aparecem aqui.

## Validação

- **Testes sintéticos: 396 grupos** (3 novos em `WanderTests`):
  - Pathfinder:
    - reto quando livre;
    - contorna a parede em 3 passos;
    - recusa destino bloqueado, início cercado e movimento nulo;
    - para na borda.
  - Vaguear, com rolagens roteirizadas na ordem do script (timers do `map_enter`, direção, distância e próximo intervalo):
    - a formiga anda 2 hexes e fica virada para o último passo;
    - o hex de origem é liberado;
    - um destino reservado pelo personagem e um destino na parede são recusados, mas o timer recomeça mesmo assim;
    - as posições passam pelo checkpoint;
    - `Reset` limpa tudo;
    - o radscorpion em outra elevação só sorteia o intervalo;
    - o hex do personagem nunca é destino;
    - nada anda durante uma luta.
- **`tools/Validate-Temple.ps1`: 21 passos aprovados.** Em `WANDER_OK`, depois da primeira porta, com semente 7:
  - 120 s de jogo dão 394 movimentos de 17 das 21 criaturas. As 4 paradas são os radscorpions das elevações 1 e 2, que esperam o personagem chegar à elevação deles, como o `zclscorp` manda.
  - Todas as criaturas ficam em hexes livres, aparecem no hex projetado e nunca no hex do personagem.
  - Uma caminhada de 8 a 12 hexes termina enquanto o tempo passa.
  - As posições sobrevivem a salvar, reiniciar e carregar.
- **Auditoria do Templo** (20 sementes por variante; entre parênteses, a medição sem vaguear):

| Personagem | Rota direta | Com kit | Lutas por rodada (direta) |
| --- | --- | --- | --- |
| Neutro (Speech) | 16/20 (15) | 2/20 (0) | 4,85 (5,85) |
| Narg (duelo) | 18/20 (18) | 15/20 (20) | 6,1 (7) |
| Mingan (duelo) | 4/20 (8) | 1/20 (3) | 6,15 (7) |
| Chitsa (Speech) | 14/20 (11) | 6/20 (0) | 5,75 (5,55) |

  Nenhuma rodada travou: as esperas por criaturas no caminho sempre terminaram.
- **Leitura:**
  - **Fato medido:** nas rotas diretas há menos lutas por rodada em três dos quatro perfis. As criaturas saem do lugar de origem, e algumas deixam de ficar na rota.
  - **Fato medido:** os perfis que dependem do duelo com Cameron (Narg e Mingan) perdem mais. As rodadas de Mingan terminam quase todas no duelo (16 de 20 na rota direta).
  - **Inferência técnica:** com 20 sementes, parte das diferenças é variação. O vaguear muda a sequência aleatória de cada rodada e quais criaturas encontram o personagem.
- **`tools/Run-QuietGodot.ps1` (desktop isolado):** aprovado em 274 s. A captura `.local/captures/wander/preview.wander.png` mostra o radscorpion 225080 depois de carregar o checkpoint: no hex 22525 com rotação 3, e não mais no hex 21729 com rotação 5 do MAP.
