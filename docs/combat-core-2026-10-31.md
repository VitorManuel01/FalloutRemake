# Frente B · Fase 2 — núcleo de combate em C# puro

31/10/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

Pacotes do roadmap `docs/roadmap/parallel-work-packages.md` (checkout principal):
ARCH-001, COM-001, ARM-001, COM-002 e COM-003. O núcleo é só domínio. **Não**
está ligado ao `TemplePreview` nem ao `WorldSession`, então ainda não há combate
jogável no jogo. A ligação é a Fase 3 (COM-004).

## Arquivos

| Pacote | Arquivos | Conteúdo |
| --- | --- | --- |
| ARCH-001 | `src/Runtime/Combat/ICombatSession.cs`, `CombatContracts.cs` | Contrato `ICombatSession` (`Preview`/`Execute` com a mesma regra), propostas, resultados, eventos, estado do turno e `SeededCombatRandom` (SplitMix64). |
| COM-001 | `DerivedCombatStats.cs`, `CombatantState.cs` | Criatura: estatística efetiva = base + bônus do PRO, limitada como em `critterGetStat`. Personagem: fórmulas de `critterUpdateDerivedStats` e `skillGetValue`. `CombatWeapon` reúne o soco do CE ou o modo primário da arma. Criaturas do mapa usam o `hp` e o `team` da instância. |
| ARM-001 | `src/Import/WeaponPrototypeData.cs`, `ArmorPrototypeData.cs`, `AmmoPrototypeData.cs` | Blocos de arma, armadura e munição nos offsets do CE (`protoItemDataRead`), após o cabeçalho de item de 57 bytes. Modos de ataque vêm dos nibbles das flags estendidas. |
| COM-002 | `CombatInitiative.cs`, `CombatMovement.cs` | Ordem por Sequence e depois Sorte (`_compare_faster`). Empates restantes: Agilidade, Percepção e id. O iniciador age primeiro na rodada 1. Movimento de 1 AP por hex sobre `HexNavigation`: combatentes vivos bloqueiam, cadáveres liberam o hex e grades de saída ficam fora. |
| COM-003 | `CombatResolver.cs`, `DamageCalculator.cs`, `CombatSession.cs` | Chance de acerto (`attackDetermineToHit`) e dano (`attackComputeDamage`). A sessão cuida de turnos, AP, ataque, morte, XP e fim do combate. `ProvisionalMeleeBehavior` controla o lado não jogador até o AI-001. |

## Regras confirmadas no CE

A referência é `alexbatalov/fallout2-ce@e97087b`, nos arquivos `combat.cc`,
`stat.cc`, `skill.cc`, `item.cc` e `proto.cc`.

- **Derivadas:** HP = 15 + ST + 2×EN; AP = 5 + AG/2; AC = AG; Melee Damage =
  max(ST−5, 1); Sequence = 2×PE; Crítico = Sorte.
- **Perícias:** valor-base + multiplicador × atributos + pontos do PRO. Unarmed é
  30 + 2×(AG+ST); Melee, 20 + 2×(AG+ST); Small Guns, 5 + 4×AG; Throwing, 0 + 4×AG.
- **Acerto:**
  - Começa pela perícia da arma; sem arma, Unarmed.
  - Distância, só à distância: `max(dist − 2×(PE−2), −2×PE) × −4`. Para quem não é o jogador, o termo é 2×PE.
  - Força mínima: −20 por ponto que faltar.
  - Subtrai-se a AC do alvo mais o modificador de AC da munição, com piso 0.
  - O CE limita só o teto, em 95.
- **Dano por projétil:**
  - O sorteio vai de mín a (máx + Melee Damage), e o Melee Damage só entra em ataques corpo a corpo e desarmados.
  - Depois: ×2 × multiplicador da munição ÷ divisor ÷ 2.
  - Subtrai-se o DT.
  - Por fim, `dano −= dano × DR / 100`, truncando. O DR inclui o modificador da munição e fica em 0..100.
- **Sem arma:** 3 AP, alcance 1 e dano de 1 a 2 (soco).

## Correções aos números do roadmap

Verificado com `--combat-report` nos PROs e no mapa originais.

| Item | Roadmap | Dado real |
| --- | --- | --- |
| Formiga gigante (`0x0100017E`) | 18 HP · 7 AP · Seq 50 · Unarmed 25 | **6 HP · 5 AP · Seq 9** · AC 4 · Unarmed 65 · 25 XP. As 12 instâncias guardam `hp=6`. |
| Radscorpion (`0x01000005`) | 32 HP · 6 AP · Seq 150 | **10 HP · 5 AP · Seq 4** · AC 3 · DT/DR laser 2/30 · 60 XP |
| Personagem neutro | Unarmed 35% | **Unarmed 50%**, Melee 40%, 30 HP, 7 AP, AC 5, Seq 10 |
| Pistola 10mm | PID 4 | **PID 8**. O PID 4 é a faca. |
| Iniciativa | Sequence + D6 | Sequence e depois Sorte, sem D6. |
| DR | `floor(dano × (1 − DR/100))` | `dano − dano×DR/100`. Exemplo: 7 de dano com DR 30% dá 5 no CE e 4 na fórmula do roadmap. |

O roadmap lia só a coluna base do PRO e ignorava o bloco de bônus. Para as
criaturas, o CE soma base e bônus.

## Regras do projeto (não-CE)

- A chance de acerto fica entre 5 e 95, conforme o critério COM-003. O valor bruto
  do CE continua no detalhamento.
- AP não gastos viram AC até o próximo turno do combatente (especificação, §3.2). **Correção (01/11):** no CE, a AC soma os AP que o combatente tem fora do próprio turno, e toda rodada recarrega os AP de todos (`_combat_set_move_all`); quem ainda não agiu na rodada defende com todos eles ([duelo de Cameron](cameron-duel-2026-11-01.md)).
- O turno termina só com `EndTurn`.
- XP só por morte causada pelo jogador. O total fica na sessão; somar ao
  `WorldSession` é a Fase 3.

## Fora do escopo

- Críticos, tiros mirados, rajada, linha de tiro, cobertura e luz.
- Perks, traits, tags e dificuldades diferentes de normal.
- Knockback e aleijamento.
- Recarga e consumo de munição.
- Persistência no checkpoint.
- A IA tática e a interface no Godot.

## Validação

- **Testes sintéticos:** 350 grupos (eram 340). Os dez novos de combate cobrem o
  parser de itens, as derivadas do personagem e da criatura, a iniciativa, o
  movimento com bloqueios e saídas, a chance de acerto, o dano com munição, DT e
  DR, uma sessão completa com paridade entre prévia e resolução, as recusas
  atômicas, o comportamento provisório e a reprodução por semente.
- **`--combat-report` (dados reais):** derivadas das três criaturas do Templo,
  lança, faca, pistola, SMG, armadura de couro e munição 10mm JHP. O duelo com
  semente 2026 contra a primeira formiga da elevação 0 (hex 20066) terminou na
  rodada 5 com vitória do jogador (19/30 HP) e 25 XP. A instância original da
  formiga e os bytes do MAP ficaram intactos.
- **`tools/Validate-Temple.ps1`:** 21 passos aprovados. O relatório de combate
  agora faz parte da validação.

## Próximo passo (Fase 3 / COM-004)

Ligar a `CombatSession` ao `TemplePreview`:

- Detectar a formiga e entrar em modo de combate.
- Turnos com AP visível.
- Clique para mover e atacar, com a prévia de `Preview`.
- Ao vencer, remover a formiga do mapa vivo e somar 25 XP ao `WorldSession`.

Antes disso, é preciso decidir a persistência em checkpoint: o roadmap pede CONT-003 (v10) primeiro.
