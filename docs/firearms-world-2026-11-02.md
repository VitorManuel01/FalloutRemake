# Armas de fogo no mundo (parte 2)

02/11/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`. Continua
`firearms-2026-11-02.md` (regras de combate).

## O que o original faz (código do CE, revisão e97087b)

- **Carga da arma:** o objeto da arma guarda as balas carregadas e o tipo de munição (`ammoQuantity`, `ammoTypePid`). No MAP elas vêm em cada item.
- **Pilha de munição (`itemRemove`/`itemAdd`):** é uma caixa parcial em cima de caixas cheias.
  - **Pegar parte de uma pilha:** quem pega leva a caixa de cima; as que ficam ficam cheias.
  - **Juntar duas pilhas:** soma as caixas de cima. Se cabem numa caixa só, some um objeto de caixa.
- **Recarga (`weaponReload`):** tira balas da caixa de cima, e a caixa esvaziada é destruída (`_obj_destroy`). Fora de combate não custa AP.
- **Peso (`itemGetWeight`):** a arma carregada pesa o próprio peso + peso da caixa × ((balas − 1) / caixa + 1).

## O que mudou

- **`WorldFirearms.cs`:**
  - **Carga:** cada arma usa a carga do MAP até o jogo mudá-la; daí em diante vale a do mundo.
  - **Munição:** cada origem guarda quantas balas faltam na caixa de cima de cada lado, a do jogador e a de onde veio.
  - **Pegar e devolver:** a caixa parcial passa para quem recebe. Se as duas caixas de cima cabem numa só, uma unidade é gasta, como o objeto que some no CE.
  - **Recarga fora de combate:** `Reload(mão)`, sem AP.
  - **Carga desconhecida:** `ammoTypePid` −1 fica −1; a recarga então pega qualquer munição do calibre.
- **Mãos:** arma de fogo pode ser empunhada, mesmo vazia. Ficam recusadas:
  - lança-chamas e outros modos contínuos;
  - armas cuja origem é uma pilha de mais de uma unidade, porque no CE a pilha compartilha uma carga;
  - carga com munição que o jogo não lê.
- **PRO de munição:** a munição que nenhum objeto do mapa usa é lida dos dados do jogo (`runtimePrototype`).
- **Combate:**
  - **Início:** o jogador entra com a munição da bolsa e as armas carregadas.
  - **Inventário no meio da luta:** quando a luta reabre as armas pelo inventário, as balas já disparadas continuam valendo.
  - **Fim:**
    - **Luta aplicada:** a munição recarregada sai das caixas e as balas finais viram a carga das armas.
    - **Luta descartada:** nada muda.
    - **Munição faltando:** se a bolsa não tem mais a munição que a luta usou, o resultado é recusado.
- **Checkpoint (opcional, dentro do v13):**
  - **Campo novo:** `Firearms`, com as cargas das armas e as caixas parciais.
  - **Recusas:**
    - carga acima do pente, de outro calibre, de arma que não usa munição, de pilha ou de item inexistente;
    - déficit fora da caixa;
    - campo vazio gravado explicitamente.
  - **Unidades de munição gastas:** valem como consumíveis.
- **Peso:** a arma carregada pesa como no CE, e arma com balas já não fica com peso "indisponível".
- **Prévia:**
  - **Nome da arma:** mostra as balas (`7/12`) e "(rajada)" no segundo modo.
  - **Log do combate:** mostra balas gastas, recargas e rajadas, com quem cada uma atingiu.
  - **Botão "Recarregar":** 2 AP em combate, grátis fora dele.
  - **Recusas novas:** "sem munição" e "já carregada".

## Limitações

- **Nos mapas jogáveis:** nenhuma arma de fogo nem munição nos mapas 3, 4 e 126, então tudo isso só aparece em testes sintéticos. Na prévia, o botão "Recarregar" responde e recusa a mão vazia.
- **Fora do modelo:** descarregar a arma (`weaponUnload`), a ordem real do inventário do CE (a bolsa do projeto segue a ordem das chaves) e a IA com armas de fogo.

## Validação

- **Testes:** 462 grupos, 5 novos em `WorldFirearmTests`:
  - **Pilhas:**
    - a caixa de cima (10) vem primeiro, depois as cheias (34);
    - devolver leva a parcial;
    - pegar tudo dá 58.
  - **Recarga fora de combate:** sem AP; 12 de 58 esvazia e descarta a caixa de 10; arma cheia é recusada; 12 balas pesam uma caixa.
  - **Junção de tops:** 12 + 4 cabem numa caixa, ao pegar e ao devolver, e some uma unidade.
  - **Luta:** recarga de 1 bala por 2 AP e um tiro.
    - A arma fica com 11 e a pilha com 57.
    - Checkpoint salvo e restaurado; forjados recusados.
  - **Luta descartada:** nada muda.
- **Regras de combate:** `FirearmTests` continuam passando.
- **Auditoria do Templo:** os números não mudaram (Neutro 16/2, Narg 19/18, Mingan 18/19, Chitsa 20/20).
- **`Validate-Temple.ps1`:** 23 passos aprovados. O runner isolado também aprovou, com `RELOAD_OK`, `MEDICAL_OK` e `REST_OK`.
