# Frente B · COM-004 — combate jogável no Templo (formigas e radscorpions)

31/10/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

O núcleo da Fase 2 ([combat-core](combat-core-2026-10-31.md)) agora está ligado ao
`TemplePreview`. Quem anda pelo Templo e é visto por uma formiga gigante (PID
`0x0100017E`) ou por um radscorpion (PID `0x01000005`) entra em luta, que acontece
por turnos sobre a grade hexagonal. É o primeiro
**gameplay de combate integrado**, ainda sem IA tática. O Dumar mantém o papel do
Speech e não entra em combate.

## Fluxo

1. **Encontro** (`WorldSession.FindEncounter`): uma criatura de encontro viva,
   sem `HIDDEN`, de time diferente de 0, na mesma elevação, que **vê** o jogador
   pela regra do CE ([percepção](critter-sight-2026-11-01.md), desde 01/11; antes
   era um raio fixo de 5 hexes) e tem caminho até ele. Se o personagem estiver
   andando, a luta começa no próximo hex inteiro.
2. **Início** (`WorldSession.BeginCombat`): a criatura que percebeu o jogador age
   primeiro (`_combat_sequence_init` do CE). As rodadas seguintes seguem a ordem de
   Sequence e Sorte. O jogador entra com o HP que restou da luta anterior.
3. **Turno do jogador:**
   - Clique no chão para mover (1 AP por hex).
   - Clique na criatura para atacar com a arma carregada (`WorldSession.PlayerWeapon`).
     Vale a arma corpo a corpo de maior dano médio que está na bolsa; sem nenhuma,
     o ataque é um soco (1–2, 3 AP). Armas de fogo ficam de fora até existir
     controle de munição. Não há slot de equipar: carregar a arma é empunhá-la. A
     única arma do Templo é a lança (PID 7), num recipiente atrás da porta trancada
     (hex 16262). O inventário inicial do personagem original não está modelado.
   - **Espaço** ou o botão encerra o turno.
   - Ao passar o mouse, aparece a prévia do mesmo `Preview` que resolve a ação:
     chance, dano depois de DT/DR, custo e motivo de recusa. O teste in-engine
     compara a chance mostrada com a usada no ataque.
4. **Turno da criatura:** `ProvisionalMeleeBehavior` (aproximar e atacar), com
   0,45 s de pausa para leitura.
5. **Animação mínima:** os combatentes andam hex a hex (0,12 s por hex) e dão um
   avanço curto no golpe. A ação seguinte da criatura e o fim da luta esperam a
   animação. Nas verificações automáticas tudo é instantâneo, exceto um turno
   animado de propósito para testar a espera.
6. **Fim:**
   - **Vitória:** `WorldSession.CommitCombat` registra o cadáver no hex em que a
     criatura caiu, sem bloquear, mais o XP do PRO (formiga 25, radscorpion 60) e
     o HP restante. O mundo é
     reconstruído pela projeção (`EffectiveObject`). O registro original do MAP não
     muda.
   - **Derrota:** a prévia reinicia.

Durante a luta ficam bloqueados, com aviso: salvar, sair da área, portas,
arrombar, recolher itens, trocar de elevação, trocar o modo e a arquitetura, e
caminhar livremente. A câmera, as visualizações, **R** (carregar) e **Reiniciar**
continuam disponíveis; carregar ou reiniciar descarta a luta.

## Persistência

Desde o [checkpoint v10](checkpoint-v10-combat-2026-10-31.md), o resultado das
lutas é salvo e carregado. A primeira versão deste marco mantinha o resultado só
em memória e recusava salvar depois de uma vitória. Hoje salvar fica recusado
apenas enquanto uma luta está aberta.

## Arquivos

| Camada | Arquivos |
| --- | --- |
| Domínio | `src/Runtime/WorldCombat.cs` (encontro, início, commit, cadáver, XP, HP); `WorldSession.Experience` e `Reset`; `EffectiveObject` projeta cadáveres. |
| Godot | `src/Preview/CombatViewAdapter.cs` (gatilho, turnos, cliques, prévia, eventos, cadáver, teste in-engine); `src/Preview/CombatHudBar.cs` (barra de combate). `ProjectTempleLayout` também projeta cadáveres. Bloqueios em portas, saída, recolher, salvar, elevação, modo e arquitetura. |
| Testes | `tools/TempleInspector/WorldCombatTests.cs`; `--combat-report` (duelos com semente contra uma formiga e um radscorpion reais); `CheckCombat` em `--preview-test` (`COMBAT_SLICE_OK`, exigido pelos dois runners). |

## Validação

- **Testes sintéticos:** 357 grupos. Os de `WorldCombat` cobrem:
  - o encontro (alcance, formiga murada, outro mapa ou elevação, radscorpion);
  - o commit único com cadáver projetado, hex liberado e XP;
  - o reset;
  - o HP ferido mantido;
  - as lutas perdidas, estrangeiras ou incompletas recusadas sem efeito;
  - a arma carregada: lança empunhada, pistola sem munição ignorada, mãos vazias;
  - o checkpoint v10.
- **`--combat-report`:** formiga (hex 20066), vitória na rodada 5 com 19 HP e
  25 XP; radscorpion (hex 13071), vitória na rodada 5 com 17 HP e 60 XP. A
  ferroada tem 70% (Unarmed 75 − AC 5).
- **`--preview-test` headless e gráfico isolado:**
  - Depois de abrir a primeira porta, o personagem caminha até uma formiga e é
    percebido no vão da porta (hex 24308). Desde a [percepção do
    CE](critter-sight-2026-11-01.md), a formiga 215712 o vê a 6 hexes (hex 21885) e
    uma segunda formiga entra na luta.
  - A lança carregada é a arma usada.
  - O primeiro turno da criatura é animado e a ação seguinte espera a animação, que termina no hex lógico.
  - A luta, com semente 2026, termina na rodada 2. Com as duas formigas, a vitória
    leva pelo menos 6 turnos do jogador e rende 50 XP.
  - A prévia ao passar o mouse coincide com a resolução.
  - O marcador acompanha cada movimento.
  - As ações de mundo e o salvamento no meio da luta são recusados, e o arquivo de
    checkpoint fica intacto.
  - Salvar depois da vitória, reiniciar e carregar mantém o cadáver, o XP e o HP.
  - O cadáver fica onde a formiga caiu e não bloqueia.
  - Reiniciar devolve a formiga.
  - Os bytes do MAP são preservados.
- **Capturas:** `.local/kit/temple.combat-start.png` e `temple.combat-victory.png`.
- **`tools/Validate-Temple.ps1`:** 21 passos aprovados.
- **`tools/Run-QuietGodot.ps1`:** aprovado em cerca de 245 s, perto do limite de
  300 s do runner. A bateria gráfica completa está crescendo.

## Limites

- **XP e nível:** resolvido pela [progressão de nível](character-progression-2026-10-31.md). A recompensa de Arroyo vale em qualquer nível, e o XP das lutas faz o personagem subir de nível pelas regras do CE.
- O veneno do radscorpion está modelado desde 01/11 ([veneno](radscorpion-poison-2026-11-01.md)). Críticos e efeitos de dano não estão.
- A animação é mínima (deslizar e avançar). Não há animação de morte; o cadáver é
  a cápsula deitada.
- Sem IA tática (AI-001), sem críticos, mira, cobertura ou luz.
- Sem slot de equipar e sem armas de fogo em combate (falta controle de munição).
- HP do jogador só existe em combate. Não há cura nem HP no HUD de exploração.
- Uma luta aberta não pode ser salva. Só o resultado de lutas terminadas entra no checkpoint v10 (o roadmap prevê salvar no meio do turno no SAV-001).
