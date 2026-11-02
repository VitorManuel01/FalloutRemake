# Percepção das criaturas do Templo (regra do CE)

01/11/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

Até aqui, uma formiga ou um radscorpion começava a luta quando o personagem
chegava a 5 hexes e a criatura tinha um caminho de até 10 passos até ele. Esse
raio era provisório. Agora a luta começa quando a criatura **vê** o personagem,
pela mesma conta que o Community Engine faz.

## O que o original faz

- **Fato verificado (bytecode original):**
  - As 12 formigas do Templo rodam `scripts/zclrat.int` e os 9 radscorpions rodam `scripts/zclscorp.int`. Os vínculos SID → script vêm do próprio `arcaves.map`.
  - Nos dois scripts, `critter_p_proc` é apenas:
    `if obj_can_see_obj(self_obj, dude_obj) then attack_complex(dude_obj, 0, 1, 0, 0, 30000, 0, 0)`.
    - Em `zclrat`, o corpo começa no offset 2552.
    - Em `zclscorp`, começa no offset 2662.
  - O relatório `--critter-sight-report` confere essas instruções, o tamanho e o SHA-256 dos dois scripts.
- **Fato verificado (código do CE, `alexbatalov/fallout2-ce`, branch `main`):**
  - `opObjectCanSeeObject` (`interpreter_extra.cc`) exige a mesma elevação, depois `isWithinPerception(critter, dude)`, e então `_make_straight_path(critter, ..., 16)`. O primeiro obstáculo da linha tem que ser o próprio personagem.
  - `isWithinPerception` (`combat_ai.cc`):
    - Dentro do cone frontal (`_can_see`), o alcance é **PE × 5** hexes.
    - Em qualquer direção, o alcance é **PE** (PE × 2 em combate).
    - Esgueirar-se reduz os dois. O personagem não se esgueira aqui.
  - `_can_see` (`actions.cc`): a diferença entre a rotação da criatura e a direção do alvo (`tileGetRotationTo`) precisa ser 0, 1 ou 5. Isso dá três das seis direções.
  - `objectGetDistanceBetween`: uma criatura multihex fica um hex mais perto.
  - `critterGetStat(PE)` soma a base e o bônus do PRO e limita o resultado a 1–10.
  - `_make_straight_path_func` (`animation.cc`):
    - Traça uma linha de Bresenham em pixels de tela, entre os centros (+16, +8) dos dois hexes.
    - Testa o hex de partida e depois cada hex novo que a linha cruza, com `_obj_blocking_at`.
    - Para ao chegar à linha ou coluna de tela do alvo.
  - `_obj_blocking_at` (`object.cc`): bloqueiam critter, cenário e parede sem `HIDDEN` nem `NO_BLOCK`. Porta aberta tem `NO_BLOCK` (`OBJECT_OPEN_DOOR`), então não bloqueia; porta fechada bloqueia. Cadáveres não bloqueiam, e itens no chão também não.
  - `_script_chk_critters` (`scripts.cc`) roda o `critter_p_proc` de **uma** criatura por ciclo do jogo, em rodízio, e nunca durante combate ou diálogo.

## O que o projeto faz

| Peça | Arquivo | Regra |
| --- | --- | --- |
| Geometria de tela do CE | `src/Geometry/OriginalStraightPath.cs` | Implementa `tileToScreenXY`, `tileFromScreenXY` (com a máscara 32×16 de `tile_init`), `tileGetRotationTo` e o percurso de `_make_straight_path_func`, incluindo o limite de 200 nós. |
| Percepção | `src/Runtime/CritterSight.cs` | `Perception` (base + bônus, 1–10), `Facing` (`_can_see`), `Sees` (alcance, multihex e linha reta, com os bloqueadores da `HexNavigation` da sessão). |
| Encontro | `WorldSession.FindEncounter` | Criaturas de encontro vivas que veem o personagem, da mais próxima para a mais distante. A mais próxima age primeiro. |
| Prévia | `CombatViewAdapter.CheckEncounter` | Sem mudança: verifica a cada hex inteiro. |

## Diferenças conhecidas

- **Inferência técnica: frequência.** No CE, cada criatura olha uma vez a cada rodízio do `_script_chk_critters`. Aqui, todas olham a cada hex que o personagem pisa. O efeito deve ser perceber um pouco mais cedo, mas isso não foi medido.
- **Direção estática** (resolvido em 01/11 pelo [vaguear](creature-wander-2026-11-01.md)). As formigas e os radscorpions do original vagam: `timed_event_p_proc` anda até 3 hexes numa direção aleatória a cada 3–5 s fora de combate, e isso muda a rotação.
- **Guarda do projeto.** O CE começa a luta mesmo que a criatura não consiga chegar ao personagem. Aqui ela também precisa ter caminho até um hex vizinho dele. Sem essa guarda, uma luta sem saída nunca terminaria, porque sair do combate fora da vista ainda não estava modelado. Desde a [saída com Enter](combat-leave-2026-11-01.md), a guarda continua só por causa da auditoria e da IA provisória.
- **Fora do escopo:**
  - esgueirar-se (Sneak);
  - cegueira (`DAM_BLIND`);
  - criaturas que entram numa luta já aberta ao ver o personagem (`PE × 2` em combate);
  - o veneno do radscorpion (`combat_p_proc` de `zclscorp`). Ele chama `poison` quando falha o teste de **Sorte**, e não de Resistência como esta nota dizia antes; está [modelado depois](radscorpion-poison-2026-11-01.md).

## Dados originais (`--critter-sight-report`)

- **Percepção:** as 12 formigas e os 9 radscorpions do `arcaves.map` têm PE 2. Isso dá **10 hexes** no cone frontal e **2 hexes** em volta. Nenhuma é multihex.
- **Rotações no MAP:**
  - formigas: 1×1, 2×6 e 3×5;
  - radscorpions: 1×1, 2×1, 3×4, 4×2 e 5×1.
- **Antes:** o raio fixo era de 5 hexes em todas as direções, atravessando portas fechadas desde que o caminho a pé tivesse até 10 passos.
- **Agora:** a criatura vê o dobro à frente, menos da metade atrás, e porta fechada ou outra criatura no meio esconde o personagem.

## Validação

- **Testes sintéticos: 385 grupos** (eram 382).
  - Geometria de tela do CE: todo hex volta a si mesmo pela máscara, e cada vizinho de `FalloutGrid.Neighbor` tem a rotação de `tileGetRotationTo`. Isso confirma que a numeração de direções do projeto é a do CE.
  - Percepção: cone de 3 direções a 10/11 hexes e as outras a 2/3 hexes, multihex um hex mais perto, PE limitado a 1–10.
  - Linha reta:
    - bloqueiam: parede, cenário, outra criatura e porta fechada;
    - não bloqueiam: porta aberta, cadáver, objeto escondido, item no chão e parede ao lado da linha.
  - Mundo:
    - a formiga atrás da porta fechada não percebe; com a porta aberta, ela percebe e age primeiro;
    - o radscorpion a 2 hexes percebe em qualquer direção;
    - uma formiga morta não percebe.
  - As lutas sintéticas que dependiam da formiga atrás da porta agora abrem o encontro diretamente (`WorldCombatTests.NearAnt`), sem mudar a sequência de cada luta.
- **`tools/Validate-Temple.ps1`: 21 passos aprovados.** Em `COMBAT_SLICE_OK`:
  - a formiga 215712 (hex 21080, rotação 3) vê o personagem a 6 hexes, no hex 21885, pelo cone frontal;
  - uma segunda formiga também o vê e entra na luta;
  - a luta com semente 2026 é vencida, com 50 XP.

  A verificação passou a aceitar todas as criaturas que entram na luta. Antes ela exigia uma só.
- **`tools/Run-QuietGodot.ps1` (desktop isolado):** aprovado em 306 s, com o limite elevado para 600 s, porque a bateria gráfica já passa dos 300 s. A captura `.local/captures/sight/preview.combat-start.png` mostra as duas formigas na luta.
- **Auditoria do Templo** (`--temple-run-report`, 20 sementes por variante, comparada com a medição anterior com raio fixo):

| Personagem | Rota direta | Com kit | Lutas com 2 formigas |
| --- | --- | --- | --- |
| Neutro (Speech) | 15/20 (antes 19) | 0/20 (antes 0) | 4 com kit |
| Narg (duelo) | 18/20 (antes 19) | 16/20 (antes 19) | 20 com kit |
| Mingan (duelo) | 8/20 (antes 6) | 1/20 (antes 1) | 19 com kit |
| Chitsa (Speech) | 11/20 (antes 17) | 1/20 (antes 3) | 3 com kit |

- **Leitura:**
  - **Fato medido:** os perfis frágeis em combate (neutro, Chitsa) perdem mais na rota direta. As mortes acontecem a caminho de Cameron, da barreira e do explosivo. As lutas com duas formigas só aparecem nas rotas com kit.
  - **Fato medido:** as derrotas de Narg e de Mingan continuam vindo quase todas do duelo com Cameron. Os dois chegam ao duelo com pouco dano das formigas: em média 0,8 HP (Narg) e 1,3 HP (Mingan) por luta na rota direta.
  - **Inferência técnica:** a medição continua mais dura que o original em dois pontos. As criaturas não vagam (no original, o cone gira), e a política simples da auditoria nunca usa a saída do combate com Enter, [modelada depois](combat-leave-2026-11-01.md). As criaturas do Templo não fogem nem no original. A comparação com o jogo original continua desconhecida.

## Como rodar

```powershell
dotnet run --project tools/TempleInspector/TempleInspector.csproj -- --data-dir "D:\Games\fallout2\Fallout 2 English" --critter-sight-report
```
