# Auditoria ponta a ponta do Templo com combate

31/10/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

`--temple-run-report` (`tools/TempleInspector/TempleRunAudit.cs`) percorre o
Templo inteiro com os dados originais e os encontros ligados. É uma medição
somente leitura: não grava nada e não é gameplay integrado. Ela mede onde um
personagem neutro morre ou trava.

## Roteiro

Cada passo é um movimento real de um hex (`FindRoute` + `Travel.MoveTo`). Depois
de cada passo, `FindEncounter` decide se alguma criatura percebeu o personagem.

O caminho:

1. Primeira porta (offset 244040).
2. Porta trancada (offset 137396), com arrombamento de sucesso controlado.
3. Lança no pote 179896, nas variantes com lança.
4. Saída para a elevação 1.
5. Explosivo (ID 364), posicionado a até 3 hexes da barreira 14322, com prazo de 60 s.
6. Efeito na barreira.
7. Saída para a elevação 2.
8. Portão inicial.
9. Speech com Cameron e partida do NPC.
10. Porta 100.
11. Saída para Arroyo.

As lutas seguem uma política simples:

- **Jogador:** aproxima-se e ataca a criatura mais próxima; quando não há o que fazer, encerra o turno.
- **Criaturas:** usam `ProvisionalMeleeBehavior`.
- **Variantes com cura:** recolhem todos os Healing Powders de recipientes acessíveis em cada elevação. Usam um pó quando o HP fica na metade ou abaixo: no combate custa 4 AP, fora dele não custa nada ([Healing Powder](healing-powder-2026-10-31.md)).

## Resultado

25 sementes por variante (`SeededCombatRandom` 1–25).

| Lança | Cura | Chegam a Arroyo | Mortes (onde) | Lutas | Dano médio | Pós achados / usados |
| --- | --- | --- | --- | --- | --- | --- |
| não | não | 22/25 | Cameron 3 | 6,9 | 21,8 | 0 / 0 |
| sim | não | 0/25 | lança 18, explosivo 2, barreira 2, Cameron 3 | 5,4 | 30 | 0 / 0 |
| não | sim | 5/25 | desvio aos pós e1 12, e2 7, explosivo 1 | 11,0 | 27,2 | 3 / 1,7 |
| sim | sim | 0/25 | lança 18, desvio aos pós e0 4, e1 3 | 5,8 | 30 | 1 / 0,1 |

Nenhuma rodada travou por regra ou caminho: todas terminaram em Arroyo ou em
morte. A rota inteira funciona com os encontros ligados.

### Por inimigo

| Inimigo (PRO original) | Lutas | Rodadas | Dano por luta | Derrotas |
| --- | --- | --- | --- | --- |
| Giant Ant (0x0100017E): 6 HP, Unarmed 65%, MD 2 | 173 (sem lança, sem cura) | 3,8 | 3,2 | 3 |
| Radscorpion (0x01000005): 10 HP, Unarmed 75%, MD 4 | 63 (com lança, sem cura) | 5,4 | 8,9 (até 22) | 18 |

## Leitura

- **Fato verificado:**
  - **A rota da lança é o que mata, não a lança.** O caminho até o pote 179896 (hex 16262) cruza três radscorpions da elevação 0: as lutas acontecem nos hexes 12676, 15263 e 15862. Cada luta contra um deles custa cerca de 9 HP de 30.
  - **A lança não é melhor para o personagem neutro.** Ela tem Melee 40%, 4 AP e um ataque por turno. O soco tem Unarmed 50%, 3 AP e dois ataques por turno.
  - **Os desvios até os pós cobram mais do que devolvem.** Cada pó rola 13 HP em média. Os desvios trazem cerca de 4 lutas a mais, com radscorpions.
  - **A rota direta, sem lança e sem desvios, é viável:** 22 de 25 rodadas chegam com 9 HP em média e 800 XP no total, já contando os 600 de Arroyo. Ficam no nível 1.
- **Inferência técnica:** o original é mais duro e mais generoso ao mesmo tempo.
  - **Mais duro:** no CE, uma criatura entra na luta quando vê o jogador (alcance de PE × 5 hexes, com linha de visão). Aqui o raio é de 5 hexes, alcançáveis em até 10 passos. O veneno do radscorpion também não está modelado.
  - **Mais generoso:** um personagem real tem 3 tags (+20% e progressão dobrada) e traits. Também conta com criaturas que fogem com pouco HP e com a saída do combate fora da vista. Críticos não entram: o CE só os ativa a partir do primeiro dia de jogo, que o Templo não alcança ([correção](skill-spending-2026-10-31.md)).
  - Nenhuma dessas partes existe ainda.
- **Desconhecido:** a taxa de sobrevivência de um personagem real no Templo original. Não há medição do jogo original para comparar.

## Próximos bloqueios candidatos

Em ordem de impacto medido:

1. **Escolha de personagem com tags e traits:** um Unarmed de 70% muda a conta contra os radscorpions.
2. **Fuga das criaturas por AI packet e saída do combate:** é a forma do original de não lutar até a morte.
3. ~~**Críticos:** a chance já é calculada (LK); falta a tabela de efeitos.~~ No Templo eles não acontecem: o CE exige o dia 1 de jogo.
4. **Percepção do CE no início dos encontros:** PE × 5 com linha de visão, no lugar do raio fixo.

O duelo desarmado com Cameron não foi necessário: a variante usa o ramo de
Speech, e o duelo continua fora do escopo.

## Como rodar

```powershell
dotnet run --project tools/TempleInspector/TempleInspector.csproj -- --data-dir "D:\Games\fallout2\Fallout 2 English" --temple-run-report
```

Leva cerca de 6 minutos (100 rodadas).

## Atualização: por personagem

Depois da [criação de personagem](character-creation-2026-10-31.md) e do
[duelo de Cameron](temple-duel-2026-10-31.md), o relatório roda o perfil neutro,
Narg, Mingan e Chitsa, com duas variantes cada: rota direta, e lança mais cura.
São 20 sementes por variante (`TEMPLE_RUN_SEEDS` encurta uma conferência).
Quem não marcou Speech joga o duelo e recupera o equipamento no baú. Os números
estão no documento do duelo. As tabelas acima são da medição anterior, só com o
perfil neutro.
