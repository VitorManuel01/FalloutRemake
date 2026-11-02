# Requisito de Força e One Hander

01/11/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

## O que o original faz (código do CE, revisão e97087b)

- **Fato verificado:**
  - **Força mínima:** em `attackDetermineToHit`, quando o golpe usa uma arma (não um modo desarmado), o acerto perde `20 × (weaponGetMinStrengthRequired − critterGetStat(ST))` se a diferença for positiva. Isso vale para qualquer atacante, jogador ou criatura. O perk Weapon Handling tira 3 pontos da exigência, só para o jogador.
  - **One Hander:** logo antes, o trait do jogador dá −40 com uma arma de duas mãos (`weaponIsTwoHanded`, flag `0x200` do PRO) e +20 com qualquer outra. Desarmado, não conta.
  - **Aviso:** `_combat_display` avisa o jogador antes da descrição de cada ataque feito com uma arma que pede mais Força do que ele tem (`combat.msg` 107).
  - **Força com bônus:** `critterGetStat(ST)` já inclui o bônus de uma armadura vestida (ver [slots de mão](hand-slots-2026-11-01.md), "Perk da armadura").

## O que já existia

A penalidade de 20 por ponto já vinha do núcleo de combate (`11fbbcc`, `CombatResolver.ToHit`, campo `StrengthPenalty`), com o teste "Spear needs ST 4" em `CombatTests`. Faltavam o One Hander, o aviso, a exibição na prévia e a conferência com as armas reais.

## O que mudou

- **`CombatResolver`:**
  - `StrengthShortfall` dá os pontos que faltam: 0 desarmado, e a Força do jogador já inclui o perk da armadura.
  - `ToHitBreakdown.OneHanderModifier` traz o −40 ou +20 do trait, só para o jogador e só com arma.
- **`DerivedCombatStats.OneHander`:** vem da ficha do jogador, como Finesse.
- **`AttackResolved.UnderStrength`:** marca o ataque do jogador feito com arma e Força insuficiente.
- **Prévia:**
  - **Registro da luta:** o ataque marcado é precedido por "Você não tem força suficiente para usar esta arma direito."
  - **Mira sobre o alvo:** mostra "Força insuficiente −N%" e "One Hander ±N%" quando valem.
  - **Bolsa:** empunhar uma arma pesada demais avisa "pede Força N, você tem M: −X% de acerto".

## Nos dados originais

`--strength-report` cruza as armas dos mapas 3, 4 e 126 (chão e inventários) com a Força dos quatro perfis:

| Arma | Força mínima | Duas mãos | Mapas |
|---|---|---|---|
| Knife | 2 | não | 4 |
| Spear | 4 | não | 3, 4, 126 |
| Plant Spike | 2 | não | 4 |

| Perfil | Força | One Hander | Penalidade com essas armas |
|---|---|---|---|
| Escolhido (neutro) | 5 | não | nenhuma |
| Narg | 9 | não | nenhuma |
| Mingan | 5 | não | nenhuma |
| Chitsa | 4 | sim | nenhuma |

- **Penalidade:** nenhum perfil pronto sofre a de Força com essas armas. Só um personagem criado com Força 1 a 3 sofre com a lança.
- **Mudança prática: One Hander.** A lança não é de duas mãos no PRO, então a Chitsa passa a ter +20 de acerto com ela, coisa que antes era ignorada.
  - **No duelo com o Cameron:** nada muda, porque a luta é desarmada.

## Diferenças conhecidas

- Os perks do jogador (Weapon Handling) não existem.
- O original mostra o aviso com o texto de `combat.msg` 107; a prévia usa uma frase própria em português.

## Validação

- **Testes:** 420 grupos, 2 novos em `DuelRulesTests`.
  - **Força:** um personagem de Força 2 com a lança tem −40. A armadura de energia (Força 5) zera a penalidade, e ela volta ao tirar a armadura. O ataque com a lança fica marcado com o aviso; o soco com Força 1 não.
  - **One Hander:** +20 com a lança, −40 com uma arma de duas mãos, nada desarmado, nada para uma criatura e o trait vindo da ficha.
- **Auditoria do Templo** (`--temple-run-report`, 20 sementes): com o kit da lança, a Chitsa passa de 19 para 20 rodadas concluídas, e o dano médio recebido fica em 7,8. As outras variantes ficam iguais (Neutro 16 e 2, Narg 19 e 19, Mingan 18 e 18, Chitsa sem kit 20), sem travamentos.
- **Prévia:** `tools/Validate-Temple.ps1` com 23 passos aprovados; `tools/Run-QuietGodot.ps1` aprovado no desktop isolado, com 176 checagens `_OK`. Nenhuma checagem da prévia exercita a penalidade, porque nenhum perfil a sofre nos mapas suportados.
