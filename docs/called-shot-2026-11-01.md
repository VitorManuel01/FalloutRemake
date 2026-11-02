# Mira do jogador

01/11/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

Até aqui, só as criaturas miravam (o `_ai_called_shot` do pacote de IA). O jogador agora mira como no FO2.

## O que o original faz (código do CE, revisão e97087b)

- **Fato verificado:**
  - **Quando dá para mirar:** `critterCanAim`. O jogador com Fast Shot nunca mira. Também ninguém mira dano explosivo, de fogo ou EMP, nem uma arma de plasma arremessada, nem rajadas. Socos e chutes podem ser mirados.
  - **Custo:** `weaponGetActionPointCost` soma 1 PA quando há mira. Em `_combat_attack`, a criatura só paga esse PA fora do tronco, porque seu golpe comum já nomeia o tronco. Já o jogador, mirando pela janela, paga o PA a mais em qualquer parte, inclusive o tronco. Fast Shot tira 1 PA das armas com alcance acima de 2, para o jogador.
  - **Janela de mira:** `calledShotSelectHitLocation` mostra oito partes em duas colunas: cabeça, olhos, braço direito e perna direita; tronco, virilha, braço esquerdo e perna esquerda. Cada uma traz a chance de `_determine_to_hit`, calculada de onde o jogador está. Escolher uma ataca; Esc não faz nada.
  - **Acerto:** a penalidade da parte (`hit_location_penalty`) já entrava no cálculo, pela metade em corpo a corpo.
  - **Crítico:** o crítico usa a linha da parte atingida na tabela do tipo de criatura (`gCriticalHitTables[killType][location]`).

## O que mudou

- **Tabela de críticos completa:**
  - Antes o runtime só tinha o tronco de cada um dos 19 tipos de criatura e recusava golpes mirados em criaturas.
  - `CriticalRules.KillTypeTable` agora tem os 19 × 9 × 6 = 1026 registros, gerados do `combat.cc` do CE. `--critical-report` compara cada um com o `fallout2.exe` (0x510978).
  - **Divergência:** dois registros do código do CE diferem do executável. No Robot, virilha, efeitos 4 e 5, o executável tem `DAM_CRIP_RANDOM` como efeito do crítico massivo, e o CE não tem. O projeto segue o executável.
- **`CombatSession.AttackCost`:** o jogador paga +1 PA em qualquer parte nomeada; a criatura, só fora do tronco. Fast Shot tira 1 PA das armas com alcance acima de 2. O custo nunca fica abaixo de 1.
- **`CombatSession.CanAim`:** é a regra de `critterCanAim`, usada pela IA e pelo jogador. Um golpe mirado que não pode ser mirado é recusado com `CannotAim`.
- **`DerivedCombatStats.FastShot`:** vem da ficha do jogador.
- **Prévia:**
  - **Clique direito numa criatura, no seu turno:** abre a janela "Mirar" ao soltar o botão sem arrastar; arrastar com ele continua movendo a câmera. A janela (`CalledShotWindow`) tem as oito partes na ordem do original. Cada uma mostra a chance e o custo, ambos tirados da prévia da luta. Partes recusadas ficam desligadas e dizem o motivo.
  - **Na janela:** escolher uma parte ataca; cancelar não gasta nada.
  - **Sem mira possível:** com Fast Shot ou um golpe que não pode ser mirado, a janela não abre e o status diz por quê.
  - **Mira sobre o alvo:** passa a lembrar "clique direito: mirar".
  - **Registro:** mostra "mirando os olhos" e, para o jogador, também "mirando o tronco".

## Diferenças conhecidas

- **Nomes das partes:** o original usa nomes por tipo de criatura (`combat.msg`, 1000 + 10 × alias da arte + parte). A janela usa nomes genéricos em português.
- **Janela:** não reproduz a arte do original (a silhueta da criatura).
- **Auditoria do Templo:** a política do jogador continua sem mirar, então a mira não muda o resultado da auditoria.

## Validação

- **Testes:** 423 grupos, 3 novos em `DuelRulesTests`:
  - **Custo:** o soco custa 3, ou 4 mirado (o tronco também); a criatura paga a mais só fora do tronco. Os olhos têm metade da penalidade em corpo a corpo, e o ataque aos olhos gasta 4 PA e fica registrado.
  - **Fast Shot e tipos de dano:** Fast Shot recusa a mira e mantém o golpe comum. Ele tira 1 PA só com alcance acima de 2, e o trait vem da ficha. Fogo, explosão e EMP nunca são mirados; dano normal e laser são.
  - **Tabela:** a linha crítica segue a parte (sem mira é o tronco), os olhos da formiga têm linhas próprias e a virilha do robô segue o executável. São 19 × 9 × 6 registros.
- **`--critical-report`:** `CRITICAL_TABLES_OK`, com os 1026 registros das criaturas iguais ao executável.
- **Prévia (`CALLED_SHOT_OK`):** passada a primeira porta, o jogador abre uma luta com a criatura mais próxima e se aproxima.
  - O clique direito abre as oito partes, com as chances da luta e o custo do golpe + 1.
  - Cancelar não gasta nada.
  - Escolher os olhos ataca os olhos por esse custo, e o registro mostra "mirando os olhos". A captura é `called-shot.png`.
  - **Valores medidos:** contra a formiga, com o soco do perfil neutro, o tronco tem 41%, a cabeça 21%, os olhos 11%, a virilha e os braços 26% e as pernas 31%, cada um por 4 PA.
- **Auditoria do Templo:** idêntica ao commit anterior em todas as variantes (Neutro 16 e 2, Narg 19 e 19, Mingan 18 e 18, Chitsa 20 e 20), sem travamentos.
- **Godot:** `tools/Validate-Temple.ps1` aprovou os 23 passos, e `tools/Run-QuietGodot.ps1` deu 178 checagens `_OK` no desktop isolado.
  - **Correções na checagem:** na primeira rodada isolada, a criatura aparecia sob o painel do topo, e o clique foi corretamente recusado. Agora a checagem centraliza a câmera na criatura antes de clicar. O clique direito também passou a testar o painel na posição do próprio evento.
  - **Correção na janela:** a janela ocupava a altura inteira da tela e foi ajustada para 560 × 320.
