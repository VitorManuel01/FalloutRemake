# Frente B · COM-004 — primeiro combate jogável (formiga gigante)

31/10/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

O núcleo da Fase 2 ([combat-core](combat-core-2026-10-31.md)) agora está ligado ao
`TemplePreview`. Quem anda pelo Templo e chega a até 5 hexes de uma formiga
gigante é percebido por ela, e a luta acontece por turnos sobre a grade hexagonal.
Este é o primeiro **gameplay de combate integrado**, ainda restrito às formigas
gigantes (PID `0x0100017E`), sem IA tática e sem persistência no checkpoint.

## Fluxo

1. **Encontro** (`WorldSession.FindEncounter`): uma formiga viva, sem `HIDDEN`, de
   time diferente de 0, na mesma elevação, a no máximo 5 hexes e com caminho de
   até 10 passos até o jogador. Uma formiga atrás de parede, em outra sala, não
   inicia luta. Se o personagem estiver andando, a luta começa no próximo hex
   inteiro.
2. **Início** (`WorldSession.BeginCombat`): a formiga que percebeu o jogador age
   primeiro (`_combat_sequence_init` do CE). As rodadas seguintes seguem a ordem de
   Sequence e Sorte. O jogador entra com o HP que restou da luta anterior.
3. **Turno do jogador:**
   - Clique no chão para mover (1 AP por hex).
   - Clique na criatura para atacar com a lança (PID 7, 4 AP).
   - **Espaço** ou o botão encerra o turno.
   - Ao passar o mouse, aparece a prévia do mesmo `Preview` que resolve a ação:
     chance, dano depois de DT/DR, custo e motivo de recusa. O teste in-engine
     compara a chance mostrada com a usada no ataque.
4. **Turno da criatura:** `ProvisionalMeleeBehavior` (aproximar e morder), com
   0,45 s de pausa para leitura.
5. **Fim:**
   - **Vitória:** `WorldSession.CommitCombat` registra o cadáver no hex em que a
     formiga caiu, sem bloquear, mais o XP do PRO (25) e o HP restante. O mundo é
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
| Testes | `tools/TempleInspector/WorldCombatTests.cs` (3 grupos); `CheckCombat` em `--preview-test` (`COMBAT_SLICE_OK`, exigido pelos dois runners). |

## Validação

- **Testes sintéticos:** 353 grupos. Os 3 novos cobrem o encontro (alcance,
  formiga murada, outro mapa ou elevação), o commit único com cadáver projetado,
  hex liberado e 25 XP, o reset, o HP ferido mantido, e as lutas perdidas,
  estrangeiras ou incompletas recusadas sem efeito.
- **`--preview-test` headless e gráfico isolado:**
  - Depois de abrir a primeira porta, o personagem caminha até uma formiga e é
    percebido no vão da porta (hex 24308).
  - A luta, com semente 2026, termina na rodada 2.
  - A prévia ao passar o mouse coincide com a resolução.
  - O marcador acompanha cada movimento.
  - As ações de mundo e o salvamento são recusados, e o arquivo de checkpoint fica
    intacto.
  - O cadáver fica onde a formiga caiu e não bloqueia.
  - Reiniciar devolve a formiga.
  - Os bytes do MAP são preservados.
- **Capturas:** `.local/kit/temple.combat-start.png` e `temple.combat-victory.png`.
- **`tools/Validate-Temple.ps1`:** 21 passos aprovados.

## Limites

- Só formigas gigantes. Radscorpions e outros ficam para depois.
- Sem animação de ataque ou morte: a criatura muda de hex e o cadáver é a cápsula deitada.
- Sem IA tática (AI-001), sem críticos, mira, cobertura ou luz.
- Arma fixa (lança), porque o equipamento do inventário ainda não alimenta o combate.
- HP do jogador só existe em combate. Não há cura nem HP no HUD de exploração.
- Uma luta aberta não pode ser salva. Só o resultado de lutas terminadas entra no checkpoint v10 (o roadmap prevê salvar no meio do turno no SAV-001).
