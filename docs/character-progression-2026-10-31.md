# Progressão de nível do personagem (regras do Fallout 2)

31/10/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

O personagem neutro agora sobe de nível pelo XP, com as regras do CE
(`alexbatalov/fallout2-ce@e97087b`: `stat.cc`, `character_editor.cc`,
`stat_defs.h`). Antes, a recompensa de Arroyo exigia ficar abaixo de 400 XP porque
a subida de nível não existia. Com o combate do Templo valendo até 425 XP só na
elevação 0, essa trava bloqueava a saída para a vila.

## Regras (`src/Runtime/CharacterProgression.cs`)

| Regra | CE | Personagem neutro |
| --- | --- | --- |
| XP por nível | `pcGetExperienceForLevel`: ímpar `1000 × ⌊n/2⌋ × n`, par `1000 × ⌊n/2⌋ × (n−1)` | 2 → 1000, 3 → 3000, 4 → 6000, 5 → 10000 |
| Nível máximo | `PC_LEVEL_MAX` | 99 |
| HP máximo por nível | `EN/2 + 2` (sem Lifegiver); o HP atual sobe o mesmo tanto (`critterAdjustHitPoints`) | +4 |
| Pontos de perícia por nível | `5 + 2 × IN` (sem Educated, Skilled ou Gifted); total não gasto limitado a 99 | +15 |
| Perk | a cada 3 níveis (`nextLevel % 3`; Skilled usaria 4) | níveis 3, 6, 9… |

## Integração

- **`WorldSession`:** `Level`, `MaxHitPoints`, `HitPoints`, `UnspentSkillPoints` e
  `PerksAvailable` são derivados do XP total (portas, Arroyo e combate). Nada novo
  vai para o checkpoint: o v10 já guarda XP e HP, e o nível sai deles.
- **HP como dano:** o mundo guarda o dano sofrido. Ao subir de nível, o máximo
  cresce e o atual cresce junto, como no CE. Uma luta começa com o HP atual e os
  atributos do nível (`DerivedCombatStats.ForCharacter(..., level)`).
- **Checkpoint v10:** `PlayerHitPoints` precisa ficar abaixo do HP máximo do nível
  restaurado (por exemplo, até 33 no nível 2). HP cheio continua `null`.
- **Arroyo:** a recompensa de primeira entrada (600 XP, fixa no `arvillag.int`)
  vale em qualquer nível (`WorldEntries.cs`). Só um XP negativo ainda é recusado.
  A descrição de `ArroyoRewardProfile` foi atualizada.
- **HUD:**
  - O perfil mostra nível, XP, HP e, quando existirem, os pontos de perícia e o perk disponível.
  - A vitória em combate e a recompensa de Arroyo anunciam a subida, por exemplo:
    "subiu para o nível 2: +4 HP, +15 pontos de perícia".

## Fora do escopo

- **Gastar pontos de perícia e escolher perks:** ainda não há tela de personagem.
  Os valores aparecem, mas não alteram perícias nem dão perks.
- **Traits e perks que mudam a progressão:** Swift Learner, Lifegiver, Educated,
  Skilled e Gifted não estão modelados.
- **Cura com o tempo:** o HP perdido volta ao subir de nível, com o
  [Healing Powder](healing-powder-2026-10-31.md) ou ao reiniciar. A cura por descanso
  depende de um relógio de jogo, que ainda não existe.
- **Party:** o nível dos companions (`_partyMemberIncLevels`) não existe.

## Validação

- **Testes sintéticos:** 360 grupos.
  - Fórmulas: limiares, níveis, HP, pontos com teto, perks, ganhos e entradas inválidas.
  - Vitória que cruza 1000 XP: nível 2, máximo de 34 HP e HP atual de 26 para 30,
    15 pontos; salvar e restaurar com HP limitado pelo nível.
  - Recompensa de Arroyo concedida com 500 XP prévios: 1100 XP e nível 2.
- **`tools/Validate-Temple.ps1`:** 21 passos aprovados.
- **`tools/Run-QuietGodot.ps1`:** aprovado.
- **Tempo das baterias:** o passo `godot-preview` levou 215 s em headless e cerca
  de 286 s no desktop isolado. O teto permitido de `-TimeoutSeconds` nos dois
  runners subiu de 300 para 900 s; os padrões não mudaram. Use
  `-TimeoutSeconds 600` nas baterias completas.
