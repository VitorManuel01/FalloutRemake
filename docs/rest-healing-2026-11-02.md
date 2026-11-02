# Cura natural (descanso)

02/11/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

## O que o original faz (código do CE, revisão e97087b)

- **Fato verificado:**
  - **Onde o HP volta sozinho:** no FO2, o HP do jogador só volta sozinho descansando (`pipboyRest`, no Pip-Boy) ou viajando no mapa-múndi. Andar pelo mapa não cura.
  - **Mecânica de `pipboyRest(hours, minutes, 0)`:**
    - O descanso avança em passos de animação. Para horas, são `(v2 − v3) × 20` passos, com `v2 = minutos / 1440 × 3,5 + 0,25`.
    - Cada passo soma `(int)(minutos / passos)` a um contador (`_Check4Health`).
    - A cada 180 minutos contados, o contador volta a 0 e o jogador recupera a taxa de cura (`_AddHealth` → `_partyMemberRestingHeal(3)`).
    - **Consequência:** 3 horas contam só 13 × 13 = 169 minutos e não curam. A hora seguinte completa os 180.
    - **Contador:** recomeça em 0 sempre que o Pip-Boy é aberto (`pipboyOpen`).
    - **Relógio:** o tempo de jogo avança passo a passo, então os eventos (veneno) agem no caminho. No fim, avança as horas exatas.
  - **"Até curar":** descansa `HP que falta / taxa × 3` horas, em pedaços de 24. Depois descansa de 3 em 3 horas. Como o laço testa o HP medido antes de cada descanso, há mais um descanso de 3 horas depois que o HP enche.
  - **`_critter_can_obj_dude_rest`:**
    - **Onde é recusado:** num nível em que o mapa proíbe o descanso (`maps.txt`, `can_rest_here`; `wmMapSlotInit` permite por padrão), qualquer criatura viva de outra equipe recusa o descanso.
    - **Fora do combate:** o `whoHitMe` de todos é zerado ao fim de cada luta, então só essa regra conta.
  - **Pip-Boy:** ele só abre com o traje do Vault (`wmMapPipboyActive`, filme `VSUIT`), que no FO2 vem depois do Templo. O CE traz a opção do sfall `pipboy_available_at_game_start`, que libera o Pip-Boy desde o início.
- **Nos dados (`--rest-report`):**
  - **Arroyo Caves (o Templo, mapa 3):** `can_rest_here=No` nos três níveis. Ali o descanso é recusado enquanto houver formigas, escorpiões ou o Cameron (equipe 1) vivos no nível.
  - **Arroyo Village (mapa 4) e o exterior do Templo (mapa 126):** permitem descansar.

## O que mudou

- **`MapCatalog`:** lê `can_rest_here`, três valores Yes/No, e por padrão permite. Valor inválido é recusado.
- **`WorldSession`:**
  - `CanRest` (sem Pip-Boy, em combate, andando, criaturas por perto).
  - `Rest(horas, minutos)` porta o `pipboyRest` com os passos e o contador do original. O relógio avança passo a passo pelo `AdvanceTime`, então o veneno age, e uma morte por veneno interrompe o descanso.
  - `RestUntilHealed` segue o laço do original, inclusive o descanso a mais.
  - `OpenPipBoy` zera o contador.
  - **`PipBoyFromStart`:** é a opção do CE. Fica desligada, como no original, e não vai no save, por ser opção de jogo.
  - **`CanRestOn`:** recebe a regra do `maps.txt`.
- **Faster Healing:** passa a aplicado, porque a taxa de cura agora tem uso.
- **Prévia:**
  - **Botão "Descansar":** abre a janela, que mostra o dia, a hora, o HP e a taxa de cura.
  - **Durações:** 10 min, 30 min, de 1 a 6 h e "Até curar".
  - **Pip-Boy:** a caixa "Pip-Boy desde o início" liga a opção do CE só para esta sessão.
  - **Recusa e resultado:** a janela diz por que o descanso foi recusado, e depois quanto tempo se passou e quanto HP voltou.

## Diferenças conhecidas

- **Traje do Vault:** o projeto ainda não chega a ele. Sem a opção do CE, ninguém descansa.
- **Viagem pelo mapa-múndi:** a cura nela não existe, porque o mapa-múndi ainda não existe.
- **Durações de relógio:** "até o amanhecer, meio-dia, entardecer, meia-noite" e "até o grupo curar" não foram feitas.
- **Interrupções:** no original, eventos da fila podem interromper o descanso. Aqui só a morte por veneno interrompe, e outros eventos de tempo (scripts) ainda não existem.

## Validação

- **Testes:** 445 grupos, 4 novos em `RestTests`:
  - **Recusas:** sem Pip-Boy, andando, com o mapa proibindo descansar e uma formiga viva, durações fora do Pip-Boy e em combate.
  - **Passos e contador:** 3 horas não curam (169 minutos contados) e avançam o relógio em 3 horas exatas. Mais 1 hora cura 1 HP, e abrir o Pip-Boy de novo zera o contador.
  - **"Até curar":** com 4 HP a menos e taxa 1, são 12 horas, 40 passos de 18 minutos e 4 curas. Com o HP cheio, recusa. Faster Healing soma 2 por grau.
  - **`maps.txt`:** `can_rest_here` por nível, padrão e valor inválido.
- **Prévia (`REST_OK`):** no Templo, sem o traje, falta o Pip-Boy. Com a opção do CE, as cavernas ainda recusam (`maps.txt`, criaturas vivas). A captura é `rest.png`.
- **Auditoria do Templo (`--temple-run-report`):** os números não mudaram (Neutro 16/2, Narg 19/18, Mingan 18/19, Chitsa 20/20, sem travamentos).
- **`Validate-Temple.ps1`:** 23 passos aprovados. O runner isolado também aprovou, com `REST_OK` no log.
