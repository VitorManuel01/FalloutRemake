# Arremesso da lança

02/11/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

A lança tinha só o golpe (thrust). Agora a mão alterna para o segundo modo do PRO, o arremesso, como o botão de item do FO2. A lança arremessada sai da mão e da bolsa e fica no chão até ser recolhida.

## Nos dados originais

`--strength-report` agora também lista os modos das armas dos mapas 3, 4 e 126:

| Arma | Primeiro modo | Segundo modo | Dano |
|---|---|---|---|
| Spear | Thrust, 2 hexes, 4 PA | Throw, 8 hexes, 6 PA | 3–10 |
| Knife | Swing, 1 hex, 3 PA | Thrust, 1 hex, 3 PA | 1–6 |
| Plant Spike | Throw, 8 hexes, 4 PA | nenhum | 1–2 |

## O que o original faz (código do CE, revisão e97087b)

- **Fato verificado:**
  - **Alcance:** `weaponGetRange` limita um arremesso a 3 × FOR hexes. O perk Heave Ho! aumentaria esse limite, mas não existe aqui.
  - **Acerto e dano:** o arremesso usa a perícia Throwing e entra no termo de distância e percepção, como um tiro. `weaponGetDamage` só soma o dano corpo a corpo em golpes corpo a corpo e desarmados, não em arremessos.
  - **`_combat_check_bad_shot`:** um tiro, um arremesso ou qualquer golpe com alcance acima de 1 precisa da linha livre. Uma parede ou um cenário no caminho recusa o ataque (`combat.msg` 104), e por isso o golpe de alcance 2 da lança também passa por essa checagem.
  - **`_combat_is_shot_blocked`:** cada criatura de pé no caminho, sem contar o alvo, tira 10 do acerto de um tiro ou arremesso.
  - **Bloqueio de tiro (`_obj_shoot_blocking_at`):** bloqueiam o tiro as criaturas vivas, os cenários e as paredes visíveis. Um objeto só não bloqueia quando deixa passar quem anda e também é *shoot-through*, como uma porta aberta. Cadáveres não bloqueiam (correção do sfall que o CE traz).
  - **Erro comum:** o projétil segue até `_tile_num_beyond`, ou seja, o alcance da arma a partir do atacante, passando pelo alvo. O primeiro obstáculo depois do alvo, ou o que bloqueia quem anda no hex onde ele cai, é atingido no lugar, com dano normal e sem crítico. Uma parede só para o projétil.
  - **`_check_ranged_miss`:** no original, esse passo nunca atinge ninguém, porque seu teste final está invertido. O projeto não o reproduz.
  - **Destino da arma (`_action_ranged`):** a arma arremessada sai da mão e da bolsa (`itemRemove`) e cai no hex do alvo atingido, ou no hex onde o erro termina. Numa falha crítica ela não é arremessada. Se houver outra unidade da pilha, ela vai para a mão (`itemReplace`).
  - **Recolher (`actionPickUp`):** é preciso chegar até o item. Em combate, só a caminhada custa PA, e o item vai para a bolsa, não para a mão.

## O que mudou

- **Geometria:**
  - `OriginalStraightPath.TileBeyond` porta `_tile_num_beyond`, e `FirstBlocked` aceita o passo de nó do tiro (32).
  - `HexNavigation.ShotBlockersAt` segue a regra de `_obj_shoot_blocking_at`, incluindo os vizinhos de objetos multihex.
- **`CombatWeapon.Secondary`:** dá o segundo modo do PRO (arremesso ou corpo a corpo), com alcance, PA e perícia próprios.
- **`CombatSession`:**
  - `Range` aplica o limite de 3 × FOR, e `CombatLineOfFire` faz a linha de tiro.
  - A prévia de ataque recusa com `AimBlocked` quando a linha está bloqueada e passa ao acerto o número de criaturas no caminho.
  - Um erro comum de arremesso ou tiro voa até o fim do alcance e pode atingir outra criatura (`StrayHit`).
  - O arremesso registra `WeaponThrown` com o hex onde a arma caiu.
  - `ChangeMode` troca o modo da mão ativa no turno do jogador, sem custo.
- **Mundo:**
  - `ToggleAttackMode` alterna o modo da mão; a mão volta ao primeiro modo quando troca de item.
  - O arremesso tira uma unidade da mão e da bolsa e a põe no chão.
  - `ItemTransferSession` ganha o registro do chão (`GroundItem`); o balanço do item passa a ter `OnGround`.
  - `CanPickUp` e `PickUp` recolhem o item estando ao lado dele ou sobre ele, também em combate no turno do jogador, respeitando a carga.
  - **Armas na mão:** além das corpo a corpo, agora também as de arremesso, como o Plant Spike; só as armas de fogo continuam fora.
- **Checkpoint:** `SavedArea` ganha `Ground` opcional, que fica de fora quando não há nada no chão. São recusados linhas forjadas, mapas desconhecidos, rotação diferente de 0, linhas duplicadas, quantidade acima da carregada e lista vazia.
- **Prévia:**
  - **Z:** alterna o modo da mão ativa, dentro e fora da luta. A linha das mãos e o nome da arma mostram "(arremesso)".
  - **Registro da luta:** mostra o arremesso e o hex onde a arma caiu, o arremesso perdido que atinge outra criatura e a troca de modo. A recusa por linha bloqueada aparece como "a linha de tiro está bloqueada".
  - **Item no chão:** aparece como uma barra com o nome e "J recolher ao lado".
  - **J:** ao lado do item, recolhe-o, também no turno do jogador na luta. Sem item por perto, J segue abrindo o recipiente.

## Diferenças conhecidas

- **Modo da mão:** não vai no checkpoint; um jogo carregado começa no primeiro modo.
- **Criaturas:** não arremessam, porque nenhuma criatura dos mapas suportados carrega arma de arremesso. Se alguma arremessasse, a arma ficaria no hex, mas a criatura não a perderia.
- **Objetos que bloqueiam o tiro:** são os objetos de origem do mapa. Itens soltos não bloqueiam.
- **Fora do escopo:** Heave Ho!, granadas e explosões de arremesso.

## Validação

- **Testes:** 428 grupos, 5 novos em `ThrowTests`.
  - **Modos:** o golpe e o arremesso da lança. Uma pistola não tem segundo modo. O alcance é 8 com FOR 5 e 6 com FOR 2, e o dano corpo a corpo não entra no arremesso.
  - **Acerto:** a lança cai no hex da formiga e a mão volta ao soco.
  - **Erro:** a lança cai a 8 hexes do atacante; uma formiga atrás do alvo leva o arremesso perdido, e a lança cai no hex dela.
  - **Linha de tiro:** uma parede recusa o arremesso e também o golpe de alcance 2. Uma formiga no caminho tira 10 do acerto. O arbusto que deixa passar quem anda bloqueia o tiro; o capim *shoot-through* não.
  - **No mundo:**
    - A porta fechada bloqueia o arremesso; aberta, a lança é arremessada e sai da mão e da bolsa para o chão, com `OnGround` 1.
    - O item sobrevive ao checkpoint, e cinco formas forjadas são recusadas.
    - Longe demais ou andando, não dá para recolher; ao lado, a lança volta para a bolsa.
    - Um jogo novo limpa o chão.
  - **Fixture de teste:** as armas do teste ganharam o estado `ammoQuantity` 0 que as armas originais têm, porque sem ele o peso da carga ficava indisponível.
- **Prévia (`THROW_OK`):** passada a primeira porta, a lança vai para a mão esquerda.
  - Z troca a mão para o arremesso.
  - O jogador abre a luta com a criatura mais próxima e se aproxima até ter os 6 PA e a linha livre.
  - **Resultado (semente 2026):** o arremesso errou e a lança caiu no hex 25709, fora da mão e da bolsa e desenhada no chão.
  - Ao lado dela, J a devolveu à bolsa. A captura é `throw.png`.
- **Auditoria do Templo:**
  - **O que muda:** a nova checagem de linha alcança o golpe de alcance 2 da lança.
  - **Política do jogador:** quando a linha está bloqueada, o jogador simulado agora se aproxima até ficar ao lado da criatura; antes, encerrava o turno.
  - **Com o kit da lança:** o Narg passa de 19 para 18 rodadas concluídas e o Mingan de 18 para 19.
  - **O resto:** fica igual (Neutro 16 e 2, Narg sem kit 19, Mingan sem kit 18, Chitsa 20 e 20), sem travamentos.
  - **Fora da auditoria:** o arremesso, porque a política não arremessa.
- **`tools/Validate-Temple.ps1`:** 23 passos aprovados. **`tools/Run-QuietGodot.ps1`:** aprovado no desktop isolado, com 180 checagens `_OK`.
  - **Depois do arremesso:** a linha das mãos mostra a esquerda vazia, e a captura enquadra a lança no chão.
  - **Primeira rodada:** falhou no passo da natureza, porque a camada dos itens no chão não era descartada junto com o mundo reconstruído (erro do Godot `p_child->data.parent != this`).
  - **Correção:** a camada agora é zerada nos mesmos pontos que a dos explosivos posicionados.
