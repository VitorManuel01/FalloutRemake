# Slots de mão

01/11/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

Até aqui, carregar uma arma era o mesmo que empunhá-la: o projeto escolhia sozinho a arma de maior dano da bolsa. Agora o personagem tem as duas mãos do FO2.

## O que o original faz (código do CE, revisão e97087b)

- **Fato verificado:**
  - **Duas mãos:** o personagem tem dois slots de item, `critterGetItem1` (mão esquerda) e `critterGetItem2` (mão direita).
  - **Mão vazia:** a esquerda soca e a direita chuta, cada uma com o melhor golpe desarmado que a ficha permite (`interface.cc`, `unarmedGetPunchHitMode` / `unarmedGetKickHitMode`).
  - **Mão ativa:** só a mão ativa ataca (`gInterfaceCurrentHand`). Um jogo novo começa na esquerda, trocar alterna as duas (`interfaceBarSwapHands`), e a mão atual vai no save.
  - **Itens na bolsa:** um item apanhado vai para a bolsa e só ataca depois de posto numa mão.

## O que o projeto faz

| Peça | Arquivo | Regra |
| --- | --- | --- |
| Mãos | `src/Runtime/WorldHands.cs` | `Wield(item, mão)` põe uma arma carregada numa mão e `Unwield(mão)` a guarda. Se a mesma unidade já estava na outra mão, ela passa de uma mão para a outra; uma pilha de duas pode ocupar as duas. `HandWeapon(mão)` dá a arma da mão ou o golpe desarmado dela. `PlayerWeapon()` é a mão ativa. |
| Soltar | `ItemTransferSession.Changed` | Toda mudança no inventário de uma área avisa o mundo. Um item que deixa de ser carregado sai da mão na hora: devolvido, consumido ou guardado no baú do desafio. Pegá-lo de volta não o devolve à mão. |
| Luta | `CombatantState.AlternateWeapon`, `RightHandActive` | O jogador entra com a arma da mão ativa e a da outra mão. `SwitchHand` troca sem custo de PA. A mão ativa no fim da luta continua ativa. |
| Duelo | `WorldTempleDuel` | Os itens vão para o baú, as mãos esvaziam e o duelo usa o soco ou o chute da mão ativa. |
| Checkpoint | `SavedCombat.Hands` (opcional, dentro da v13) | `SavedHands(Left, Right, RightActive)`. Fica de fora quando as duas mãos estão vazias e a esquerda está ativa. Cada item precisa ser uma arma corpo a corpo carregada, em unidades suficientes. Um `RightActive: false` explícito e um bloco vazio são recusados. |
| Prévia | `HandPreview.cs`, `PlayerBagWindow` | A bolsa (B) tem os botões "Mão esquerda", "Mão direita" e "Guardar na bolsa", e marca cada linha com a mão que a segura. O X troca a mão ativa, dentro e fora da luta. O painel de status mostra as duas mãos e qual está ativa. |
| Auditoria | `TempleRunAudit` | Ao pegar a lança, a auditoria a põe na mão esquerda. Depois fica com a mão de mais dano por turno, a lança ou o chute. |

Este trabalho substitui o `UnarmedKick` dos dois commits anteriores, que guardava só a escolha entre soco e chute.

## Diferenças conhecidas

- **Só armas corpo a corpo.** Armas de fogo (`Unsupported`) esperam o controle de munição, e itens que não são armas não vão para a mão.
- **Empunhar só fora da luta.** No CE, abrir o inventário em combate custa 4 PA, e ali se pode trocar o item da mão. Aqui só fora da luta. A troca da mão ativa (X) funciona nos dois casos.
- **Armadura.** O slot de armadura do FO2 não faz parte desta mudança.
- **Modos de uso.** O FO2 alterna modos de uso na mesma mão, como o golpe ou o arremesso da lança. Aqui só o modo primário.

## Validação

- **Testes sintéticos: 412 grupos**, 2 novos em `DuelRulesTests`:
  - **Mãos:** carregar não é empunhar; uma arma de fogo é recusada; não se empunha andando; a lança passa de uma mão para a outra; a luta começa com ela, com o soco na outra mão; a mão ativa fica; guardar; o item que sai do inventário solta a mão.
  - **Checkpoint:** ida e volta, os padrões deixados de fora, cinco mãos forjadas recusadas e saves antigos com as mãos vazias.
  - O teste do mundo que esperava "a lança carregada é empunhada" agora a empunha na mão esquerda.
- **Prévia:**
  - `COMBAT_SLICE` põe a lança na mão esquerda pelo botão da bolsa antes da luta;
  - `HAND_OK` confere o X fora da luta e a mão ativa depois de salvar, reiniciar e carregar.
- **`tools/Validate-Temple.ps1`:** todos os passos aprovados (`COMBAT_SLICE`, `HAND_OK`, `TEMPLE_DUEL`, `TEMPLE_KEY`).
- **`tools/Run-QuietGodot.ps1`:** aprovado. A captura do início da luta mostra "Mãos: esquerda Spear (ativa) · direita Chute".
- **Auditoria do Templo:** as rodadas concluídas são as mesmas de antes em todas as variantes (Neutro 16 e 2, Narg 19 e 19, Mingan 18 e 18, Chitsa 20 e 19), sem travamentos. O dano médio recebido pelo Narg com o kit sobe de 25,8 para 29,6, porque agora ele fica com a mão de mais dano por turno, entre a lança na esquerda e o Strong Kick na direita.
