# Desafio desarmado de Cameron

31/10/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

Sem Speech marcado, o Templo termina com o desafio de Cameron. Com a
[criação de personagem](character-creation-2026-10-31.md), esse passou a ser o
caminho de Narg e Mingan. O desafio segue `scripts/actemvil.int`, conferido
instrução a instrução com o desmontador do inspetor (`--script … --instructions`).
Os textos vêm de `actemvil.msg` (`--msg`).

## O que o script faz

| Momento | Script | Efeito |
| --- | --- | --- |
| Aceitar a luta | Node009 (13338–13520) | `itemMoveAll(jogador → temple_chest_ptr)`; acha o portão em 19928 (PID 33555366), fecha e tranca; M20=1, L13=1; msg 145/146: "o equipamento fica no baú fora da sala" |
| Durante a luta | `critter_p_proc` (4134–4210) | enquanto L13=1, Cameron ataca o jogador (`attack_complex`) |
| Rendição | `combat_p_proc` (11158–11370) | no turno de Cameron (`fixed_param` 4), se HP atual < HP máx/2 e L13=1: `terminate_combat`, ele cria e usa um Healing Powder em si, L12=1, L13=2, msg 161 "Você me derrotou", M20=2, G10=1 |
| Partida | `critter_p_proc` (4328–4880) | com L13=2, Cameron sai pelo portão (destranca e abre), como depois do Speech |
| Morte | `destroy_p_proc` (8980–10670) | karma se o jogador matou; G369=1; M20=2 e G10=1 no ARCAVES |

O ramo de Speech grava G10=2, e o duelo vencido grava G10=1. O resto é igual (L12=1,
L13=2, M20=2). As perguntas opcionais do Node002 (L7–L9) não entram no desafio direto.

## Modelo

- **Combate:**
  - `CombatantState.YieldsBelowHalf` e `CombatantYielded`: no início do turno, abaixo da metade do HP máximo, o combatente se rende vivo e sem dar XP.
  - A checagem vale em todo turno, inclusive no primeiro.
- **Mundo (`WorldTempleDuel.cs`):**
  - `PrepareTempleDuel`:
    - exige elevação 2, a um hex de Cameron, sem prova resolvida e fora de combate;
    - vale para qualquer personagem: no original dá para lutar mesmo com Speech.
  - `BeginTempleDuel`:
    - todo o inventário vai para a custódia do baú do desafio (offset 465604, hex 20132);
    - o portão fica fechado e trancado;
    - o jogador luta desarmado e sem doses de cura;
    - Cameron (PRO 0x01000112: 40 HP, AP 9, AC 8, Unarmed 70, MD 3) age primeiro.
  - **Rendição:** o progresso do Templo segue o caminho Duel (G10=1) e a mesma partida de Cameron.
  - **Morte de Cameron:**
    - ficam registrados o cadáver, os 100 XP do PRO e o portão trancado;
    - a porta final lê G369, ainda não modelado, então a saída fica recusada e a interface explica o motivo;
    - o karma também não é modelado.
  - **Custódia:** as unidades no baú saem da quantidade carregada. `RetrieveTempleChest`, ao lado do baú, devolve tudo, como o "recolher tudo" do CE (`itemMoveAll`).
- **Checkpoint v12:** o bloco `Duel` guarda o desfecho e a custódia. Na restauração:
  - **Rendição:** precisa do progresso do Templo pelo caminho Duel e de Cameron vivo.
  - **Morte:** precisa do cadáver de Cameron, do portão trancado e de nenhum progresso. É o único caso em que o portão pode voltar trancado.
  - **Custódia:** só com desafio concluído, e nunca acima do que é carregado.
  - **Luta aberta:** não se salva, como qualquer luta.

## Interface

- **Botão "Desafio de Cameron":** fica ao lado da conversa, com o motivo de cada recusa.
- **Início:** abre a tela de combate com a fala 145 de Cameron.
- **Rendição:** mostra a fala 161.
- **Bolsa:** mostra quantas unidades estão no baú.
- **Baú do desafio:** J ao lado devolve o equipamento; a janela explica o papel do baú.
- **Sem Speech marcado:** a conversa explica que o caminho é o duelo.

## Auditoria do Templo (`--temple-run-report`)

20 sementes por variante. "Kit" é lança mais todos os Healing Powders alcançáveis. A auditoria passa a jogar o duelo e a recuperar o baú.

| Personagem | Rota direta | Com kit | Observação |
| --- | --- | --- | --- |
| Neutro (Speech) | 19/20 | 0/20 | a rota da lança cruza radscorpions |
| Narg (duelo) | 19/20 | 19/20 (nível 2 em 19 rodadas) | perde 1 duelo em 20 |
| Mingan (duelo) | 6/20 | 1/20 | perde 14 e 19 duelos |
| Chitsa (Speech) | 17/20 | 3/20 | morre na rota da lança |

Nenhuma rodada matou Cameron e nenhuma travou.

- **Mingan:** soca de 1 a 3 (52% contra AC 8). Cameron dá três golpes de 1 a 5 por turno (62%). E Mingan chega ferido. No original ele também não pode descansar antes: `arcaves` tem `pipboy_active=No` e `can_rest_here=No,No,No` ([descanso e críticos](skill-spending-2026-10-31.md)). Sobram os Healing Powders antes de aceitar o desafio.
- **Perfil neutro:** não vence nenhuma das 200 sementes testadas.

## Validação

- **Testes sintéticos:** 376 grupos (`TempleDuelTests`, 4 novos).
  - Custódia, portão trancado, Cameron primeiro e abandono.
  - Rendição com as gravações do `combat_p_proc`, partida e baú.
  - Checkpoint v12 e 7 falsificações.
  - Morte de Cameron com cadáver, XP e saída recusada, ida e volta e recusa sem desafio.
- **`--preview-test` (`TEMPLE_DUEL_OK`):**
  - Narg ao lado de Cameron pelo caminho real;
  - o botão inicia o desafio, as 2 unidades vão para o baú e o portão tranca;
  - semente escolhida por simulação no próprio mundo; Cameron se rende;
  - a partida termina e J no baú devolve o equipamento;
  - o mundo é restaurado para as verificações seguintes.
- **`tools/Validate-Temple.ps1`:** 21 passos aprovados.
- **`Run-QuietGodot.ps1` (desktop isolado):** aprovado; capturas `duel-start` e `duel-chest` em `.local/captures/characters2/`.
