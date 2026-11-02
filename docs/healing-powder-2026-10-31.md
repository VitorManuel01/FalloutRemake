# Healing Powder e checkpoint v11

31/10/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

O Healing Powder (PID 273) do Templo agora pode ser usado, fora e dentro do
combate. Foi o primeiro bloqueio revelado pela
[auditoria ponta a ponta](temple-run-audit-2026-10-31.md): sem cura, o dano das
lutas só se acumulava.

## Efeito lido do PRO original

`--drug-report` lê o bloco de droga (`DrugPrototypeData`, layout CE
`ProtoItemDrugData`, 125 bytes) de `proto/items/00000273.pro` em `master.dat`
(SHA-256 `1442…FA51`):

| Campo | Valor | Leitura (CE `_perform_drug_effect`) |
| --- | --- | --- |
| `stat` | −2, 35, 1 | −2 = valor aleatório entre `amount[0]` e `amount[1]`, aplicado ao 2º stat (35 = HP atual); 3º stat = PE |
| `amount` | 8, 18, −1 | cura de 8 a 18 HP; PE −1 |
| 1º efeito tardio | 360 min: 0, 0, +1 | o PE volta depois de 6 h de jogo |
| Vício | 0 | sem vício |

O Antidote (PID 49) só altera o veneno (stat 36). O veneno ainda não existe no
projeto.

## Regras aplicadas

- **Perfil fixado (`HealingDrugProfile`):**
  - identidade fixada como no explosivo: PID 273, item tipo droga, sem script, 125 bytes e o SHA-256 original;
  - `Matches` confere no PRO real que o efeito é o de 8 a 18 HP;
  - a auditoria e o `--drug-report` mostram a conferência.
- **Fora do combate (`WorldSession.UseDrug`):**
  - não custa AP, como no CE;
  - consome uma unidade carregada e cura de 8 a 18 HP;
  - o que passa do máximo se perde (`critterAdjustHitPoints`);
  - usar com HP cheio é permitido e desperdiça o pó, como no original;
  - recusa enquanto anda, durante uma luta, com item que não está na bolsa ou com PRO modificado.
- **No combate (`CombatActionKind.UseHealing`):**
  - custa 4 AP: `inventoryOpen` no CE cobra 4 AP sem Quick Pockets. Aqui são 4 AP por dose, sem juntar vários itens numa só abertura;
  - a cura usa o RNG da luta;
  - a luta conhece só o número de doses carregadas;
  - `CommitCombat` gasta as unidades usadas, em ordem fixa (mapa, origem, chave);
  - se a bolsa não tiver mais as doses usadas, a luta inteira é recusada;
  - numa derrota, nada é aplicado.
- **Fora do escopo:**
  - **PE −1 por 6 h:** o projeto não tem relógio de jogo para encerrar o efeito. Aplicá-lo sem prazo acumularia uma penalidade permanente. Hoje o PE só pesa em Sequence e em ataques à distância, ainda sem munição.
  - **Stimpak, Antidote e outras drogas:** recusados por perfil (`Unsupported`).

## Checkpoint v11

Cada área ganha `Consumed`: as unidades de itens recolhidos que foram usadas.

```json
"Consumed": [ { "OwnerKey": "<hash-do-MAP>:<offset do recipiente>", "ItemKey": "<hash-do-MAP>:<offset do item>", "Quantity": 1 } ]
```

- **Versões:**
  - v11 exige `Consumed` em todas as áreas;
  - v1–v10 não podem trazê-lo;
  - um v10 restaura sem nada consumido.
- **Validação de cada linha:**
  - item recolhido da mesma origem;
  - quantidade positiva;
  - sem duplicata;
  - PRO aceito pelo perfil;
  - consumo mais posicionamentos de explosivo não passa do recolhido.
- **HP:** a cura não precisa de campo novo, porque o `PlayerHitPoints` do bloco `Combat` (v10) já guarda o HP.
- **Saldo:** `ItemBalance.Consumed` soma explosivos detonados e drogas usadas. Uma unidade usada nunca volta ao recipiente.

O v11 ocupou o número que o [documento do v10](checkpoint-v10-combat-2026-10-31.md)
sugeria para o estado da Anciã (CONT-003). Se a Frente A criar esse estado, ele
passa a ser o v12.

## Interface do preview

- **Bolsa (B):** "Usar (8–18 HP)" aparece só para o Healing Powder. Fica desabilitado enquanto o personagem anda, e o tooltip explica por quê. A barra mostra a cura e o desperdício.
- **Combate:** H usa uma dose (4 AP). A barra mostra "Healing Powder ×N", e o log mostra a cura e o desperdício.

## Validação

- **Testes sintéticos:** 365 grupos (`WorldDrugTests`, 4 grupos novos).
  - **Uso em luta:** dose a 4 AP com cura limitada ao máximo; segunda dose recusada por AP; mundo intacto até o commit; consumo e saldo aplicados.
  - **Commit recusado:** a luta inteira é recusada quando as doses saíram da bolsa; sem pó, `NoItem`.
  - **Fora de combate:** cura limitada ao máximo e desperdício com HP cheio; recusas por movimento, chave estranha e PRO modificado.
  - **v11:** ida e volta, migração de v10 e sete consumos forjados recusados. Também são recusados o consumo de PRO modificado e uma área v11 sem `Consumed`.
- **`--preview-test`:**
  - **`COMBAT_SLICE_OK`:** o personagem carrega a lança e o pó original ID 106 do pote 206348. H tira 4 AP dentro da luta; a segunda dose é recusada. A vitória gasta o pó, e o v11 grava e restaura o consumo.
  - **`PLAYER_BAG_OK`:** o pó ID 106 só pode ser usado parado, e uma unidade é consumida.
- **`tools/Validate-Temple.ps1`:** 21 passos aprovados (headless). A captura no desktop isolado (`Run-QuietGodot.ps1`) não foi rodada nesta etapa.
