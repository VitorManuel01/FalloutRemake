# Duelo de Cameron: duas regras do CE que faltavam

01/11/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

A auditoria mostrava o Mingan perdendo 16 de 20 duelos com o Cameron. Conferindo as contas do duelo contra o código do CE, apareceram duas regras faltando. Os números das fichas estavam certos.

## Números conferidos (`--duel-report`)

- **Fichas:** a do Cameron (PRO Dumar) e as dos personagens prontos batem com o FO2.
  - **Cameron:** SPECIAL 8 5 5 5 5 8 5, 40 HP, 9 PA, CA 8, Sequence 15, Melee Damage 3, Desarmado 70, se rende abaixo de 20 HP.
  - **Mingan:** ST 5, AG 10, 28 HP, 10 PA, CA 10, Sequence 16 e Desarmado 60, sem tag.
- **Fato verificado:** a Sequence 16 do Mingan supera os 15 do Cameron. Depois da primeira rodada, aberta pelo Cameron, o Mingan age primeiro.

## O que faltava

### 1. A CA soma os PA de quem não está no turno

- **Fato verificado no CE:**
  - `critterGetStat(STAT_ARMOR_CLASS)` soma os PA atuais do combatente sempre que não é o turno dele.
  - `_combat_set_move_all` recarrega os PA de todos no início de cada rodada.
  - Por isso, quem ainda não agiu na rodada defende com todos os PA. Quem já agiu defende com o que sobrou.
- **O projeto fazia diferente:** somava só os PA que sobraram do turno anterior. Quem esperava a vez defendia quase sem bônus.
- **Agora:** os PA recarregam no início da rodada e a CA lê os PA atuais fora do turno (`CombatantState.BonusArmorClass`). Um nocauteado fica fora da lista e não recarrega, como no CE.

### 2. Os golpes desarmados avançados

- **Fato verificado no CE:** o soco da mão esquerda e o chute da mão direita usam o melhor golpe da tabela `unarmedInitVanilla` (`unarmedGetPunchHitMode` / `unarmedGetKickHitMode`).
  - O golpe escolhido é o que a perícia Desarmado, o nível, ST e AG permitem.
  - O dano é sorteado entre bônus + mínimo e bônus + Melee Damage + máximo (`weaponGetDamage`).
  - Alguns golpes penetram a armadura, ficando com só 20% do DT.
  - Outros têm chance própria de crítico, sorteada depois de um acerto comum em qualquer dia de jogo (`attackCompute`).
- **No nível 1:**
  - **Strong Punch:** exige Desarmado 55 e AG 6; +3 de dano, 3 PA.
  - **Strong Kick:** exige Desarmado 40 e AG 6; +5 de dano, 4 PA.
  - Os demais exigem nível 5 ou mais.
- **O projeto fazia diferente:** só tinha o soco básico (1–2 mais Melee Damage). O Mingan dava de 1 a 3 de dano e precisava de uns 10 acertos para o Cameron se render.
- **Agora:**
  - `UnarmedBlows` porta a tabela, e `CombatWeapon.UnarmedFor` escolhe o golpe pela ficha.
  - O jogador desarmado usa o soco da mão esquerda, que é o padrão do FO2 ao começar. No duelo e fora dele, sem arma na mão, o Mingan dá Strong Punch (4–6).

## Efeito no duelo (1.000 duelos simulados por personagem; política da auditoria)

| Personagem | Antes | Agora, soco | Agora, chute |
| --- | --- | --- | --- |
| Neutro (5 em tudo) | 0,9% | 2,2% | 2,2% |
| Narg | 97,6% | 99,4% | 99,0% (Strong Kick) |
| Mingan | 38,9% | 95,0% (Strong Punch) | 90,8% (Strong Kick) |
| Chitsa | 0,5% | 1,7% | 75,6% (Strong Kick) |

- **Fato medido:** o Mingan passa a vencer quase sempre. A CA nova pesa contra ele, porque ele age primeiro e bate num Cameron com CA 8 + 9 PA. Mesmo assim, o Strong Punch compensa com folga.
- **Fato medido:** a Chitsa só vence chutando. O soco dela é o básico (Desarmado 50 < 55), mas o Strong Kick está liberado. A prévia e a auditoria ainda não trocam de mão.

## Auditoria do Templo (20 sementes por variante; entre parênteses, antes destas correções)

| Personagem | Rota direta | Com lança e cura |
| --- | --- | --- |
| Neutro (Speech) | 16/20 (18) | 2/20 (0) |
| Narg (duelo) | 20/20 (17) | 20/20 (19) |
| Mingan (duelo) | 18/20 (8) | 18/20 (4) |
| Chitsa (Speech) | 14/20 (15) | 7/20 (6) |

- **Fato medido:**
  - nenhuma rodada travou;
  - o Mingan perde só 2 duelos em cada variante;
  - o dano médio que o Mingan recebe na rota direta cai de 25,8 para 15,6.
- **Fato medido:** a CA nova vale em todas as lutas. Os personagens de Speech mudam por uma ou duas rodadas para cima ou para baixo, o que cabe na variação de 20 sementes.

## Validação

- **Testes sintéticos: 409 grupos**, 2 novos em `DuelRulesTests`:
  - **CA e PA:** a CA com os PA fora do turno e a recarga da rodada.
  - **Tabela de golpes:** Mingan, Narg, os portões de nível e AG, o bônus nas duas pontas, a penetração e o crítico próprio do Hammer Punch antes do dia 1.
  - Os testes antigos que contavam com a CA errada foram recalculados.
- **`tools/Validate-Temple.ps1`:** todos os passos aprovados (`COMBAT_SLICE`, `TEMPLE_DUEL`, `TEMPLE_KEY`).
- **`tools/Run-QuietGodot.ps1`:** aprovado. A captura do início do duelo mostra o Narg com "9/9 AP · AC 17" no turno do Cameron.

## Troca de mão (soco e chute)

- **Como no FO2:** a mão esquerda vazia soca e a direita chuta (`interface.cc`). O jogo começa com a esquerda, e trocar de mão não custa PA.
- **No projeto:**
  - `WorldSession.UnarmedKick` guarda a mão escolhida, que vale para o golpe desarmado dentro e fora das lutas.
  - Na luta, o jogador tem o golpe da outra mão (`CombatantState.AlternateWeapon`) e troca com `CombatActionProposal.SwitchHand`, sem custo de PA (evento `HandSwitched`).
  - A mão com que ele termina a luta fica escolhida, e um jogo novo volta à esquerda.
  - O checkpoint guarda a mão, como o save do FO2. O bloco de combate leva `UnarmedKick: true` para a mão direita; a esquerda é o padrão e deixa o campo de fora, então saves antigos carregam na esquerda. Um `false` explícito é recusado.
  - A verificação `HAND_OK` da prévia confere o X fora da luta e a mão depois de salvar, reiniciar e carregar.
  - Na prévia, a tecla X troca a mão; o B do FO2 já abre a Bolsa. O HUD indica o golpe da outra mão.
- **Na auditoria:** o jogador fica com a mão de maior dano esperado por turno (dano médio × golpes que os PA permitem). Por esse critério, a Chitsa (2 chutes de 6–8 contra 2 socos de 1–3) e o Narg chutam; o Mingan soca, porque 3 Strong Punches de 4–6 rendem mais que 2 Strong Kicks de 6–8. `TEMPLE_RUN_HAND=punch` mantém o soco.

- **Auditoria com a mão escolhida** (20 sementes por variante; entre parênteses, socando sempre):

| Personagem | Rota direta | Com lança e cura |
| --- | --- | --- |
| Neutro | 16/20 (16) | 2/20 (2) |
| Narg (chuta) | 19/20 (20) | 19/20 (20) |
| Mingan (soca) | 18/20 (18) | 18/20 (18) |
| Chitsa (chuta) | 20/20 (14) | 19/20 (7) |

  - **Fato medido:** a Chitsa é quem muda.
    - O Strong Kick dela (6–8) mata as formigas e os radscorpions com folga.
    - O dano médio que ela recebe na rota direta cai de 21 para 4,2.
    - Nenhuma rodada travou.
- **Validação:**
  - **Testes:** 410 grupos sintéticos, 1 novo para a troca na luta e no mundo.
  - **`TEMPLE_DUEL`:** aprovado; nele, o X troca o Narg para o Strong Kick e de volta, sem gastar PA.
  - **Captura isolada:** aprovada.

## Diferenças conhecidas

- **Arma carregada.** Com uma arma carregada não há troca de mão. O projeto ainda não tem slots de mão e trata carregar como empunhar.
- **Modos secundários.** Os golpes secundários (Chop Punch e outros) exigem nível 5 ou mais e não são usados.
