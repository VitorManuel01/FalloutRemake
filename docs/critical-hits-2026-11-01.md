# Golpes críticos do CE

01/11/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`.

Até aqui o combate calculava a chance de crítico (Critical Chance), mas não a usava. Agora os críticos e as falhas críticas seguem o Community Engine, com as tabelas conferidas byte a byte no `fallout2.exe` original.

## O que o original faz

### Fatos verificados no executável

`--critical-report` lê o `fallout2.exe` da instalação, só para leitura, nos endereços que o CE documenta.

- **Relógio:** começa em **302.400 ticks**, ou seja, 8h24 do dia 0 (`gGameTime`, 0x51C720).
- **Trava do dia 1:** `randomTranslateRoll` (0x4A3030) divide o relógio por 864.000 nos dois ramos e só promove um acerto ou um erro a crítico quando o resultado é ≥ 1. Isso dá **15,6 horas de jogo** depois do início.
- **Trava do dia 6:** `attackComputeCriticalFailure` (0x4240DC) repete a divisão para o personagem e compara com 6. Antes do dia 6, a falha crítica do jogador é um erro comum.
- **Tabelas:** as 19 linhas de torso da tabela por tipo de criatura (`gCriticalHitTables`) e as 6 da tabela do jogador (`gPlayerCriticalHitTable`) são idênticas às do CE.
  - Na tabela inteira por tipo de criatura, o CE difere do executável em só 2 linhas, ambas da virilha do robô, fora do torso.
- **Falhas e penalidade:** a tabela de falhas críticas (`_cf_table`, 7 × 5) bate com o CE, e a penalidade de mira do torso é 0.
- **sfall:** o `ddraw.ini` da instalação usa `OverrideCriticalTable=2` e `RemoveCriticalTimelimits=0`, os mesmos padrões do CE. As correções do sfall não tocam em nenhuma linha de torso.

### Regras (código do CE, revisão e97087b)

- **Torso sempre:** o ataque sem mira (`HIT_LOCATION_UNCALLED`) vira torso em `attackCompute`. O projeto só faz esse ataque, então só as linhas de torso valem.
- **Crítico:** depois do d100 do acerto, um segundo d100 o promove a crítico se for ≤ margem / 10 + Critical Chance − 0.
  - O efeito sai de d100 + Better Criticals, nas faixas 20/45/70/90/100 e acima de 100.
  - A linha da tabela traz:
    - o multiplicador, que substitui o 2 do dano normal;
    - os efeitos;
    - opcionalmente, um teste de atributo em d10 (`statRoll`). Se falhar, o crítico vira maciço e ganha mais efeitos.
- **Falha crítica:** o segundo d100 a produz se for ≤ margem do erro / 10.
  - A coluna sai de d100 − 5 × (Sorte − 5), nas faixas 20/50/75/95, na linha do tipo de falha do PRO da arma.
  - Sem arma, ou com ataque natural, usa a linha 0.
- **Efeitos** (`_set_new_results`, `_combat_turn`, `knockoutEventProcess`):
  - **Ignorar armadura:** mantém 20% do DT e do DR, exceto contra EMP.
  - **Derrubado:** levantar custa 3 PA no próprio turno, e quem ataca ganha +40 de acerto.
  - **Nocauteado:** perde os turnos e acorda caído depois de 10 × (35 − 3 × EN) ticks. A criatura sai da lista da luta e só volta no fim da rodada em que acordou (`_combat_add_noncoms`).
  - **Perde o turno:** os PA do turno atual acabam, e o próximo turno é perdido.
  - **Morte instantânea:** mata, qualquer que seja o HP.
  - **Membros aleijados:**
    - pernas deixam cada hex mais caro: ×4 com uma perna, ×8 com as duas;
    - braços recusam armas: os dois braços, ou um braço com arma de duas mãos;
    - os membros aleijados ficam depois da luta.
- **Traços:**
  - Heavy Handed dá −30 em Better Criticals.
  - Finesse soma 30 ao DR do alvo em todo golpe que não ignora a armadura. A regra é a mesma ramificação do CE; nenhum personagem pronto tem Finesse.

## O que o projeto faz

| Peça | Arquivo | Regra |
| --- | --- | --- |
| Tabelas e faixas | `src/Runtime/Combat/CriticalRules.cs` | Linhas de torso dos 19 tipos de criatura e do jogador, `_cf_table`, relógio, travas de dia, atraso do nocaute, custo das pernas e aleijamento aleatório. |
| Rolagens e efeitos | `CombatSession` | Faz o segundo d100 só a partir do dia 1, depois o d100 do efeito, o d10 do crítico maciço, o d4 do aleijamento e só então o dano, na ordem do CE. Antes do dia 1 nenhuma rolagem nova acontece, então as lutas antigas se repetem idênticas. |
| Turnos | `CombatSession` | Nocauteado ou sem turno: `TurnLost`, e todos os PA contam como AC até o próximo turno. Ao acordar (no fim da rodada, quando o relógio passa da hora): `WokeUp`, e a criatura age no fim da rodada seguinte. Caído: `StoodUp` no início do turno. |
| Dano | `DamageCalculator` | Aplica o multiplicador da linha, o efeito de ignorar armadura e o bônus de Finesse. |
| Acerto | `CombatResolver` | +40 contra alvo caído ou nocauteado. |
| Relógio | `WorldSession.GameTime` | Começa em 302.400. A exploração soma 1 tick a cada 100 ms, e cada rodada de luta soma 5 s. Fica no bloco de combate do checkpoint (`GameTime`, ausente no início). |
| Membros | `WorldSession.PlayerCrippled`, `SurvivingCritter.Crippled` | Os membros aleijados do jogador e das sobreviventes entram na próxima luta e ficam no checkpoint. |
| Prévia | `CombatViewAdapter` | O registro de combate mostra o crítico, a falha, o turno perdido, o levantar e o despertar. O HUD mostra o estado de cada combatente e o custo por hex. Como o relógio agora vai no checkpoint, nas verificações automáticas ele só anda quando a própria verificação o avança (veneno, vaguear, críticos); fora delas, segue correndo a cada quadro. |

## Consequência para o Templo

- **Fato verificado:** o relógio começa 15,6 horas antes do dia 1. Com a regra original, **nenhum crítico acontece numa passagem normal pelo Templo**. A auditoria mede o tempo gasto (ver abaixo).
- **Fato verificado:** antes do dia 1, as rolagens e os resultados de todas as lutas são os mesmos de antes desta mudança. A auditoria com início no dia 0 repete a anterior número por número.
- Para ver o efeito, a auditoria aceita `TEMPLE_RUN_DAY=1`, que começa no dia 1. É o equivalente ao `RemoveCriticalTimelimits=1` do sfall, e não à regra original.

## Diferenças conhecidas

- **Margem do acerto.** O CE calcula a margem pela chance sem piso; aqui ela vem da chance com o piso de 5% do projeto, a mesma que decide o acerto. Só difere quando a chance bruta fica abaixo de 5.
- **Nocauteados não encerram a luta.** No CE, a luta termina no fim da rodada se só restam inimigos nocauteados, e atacá-los reabre outra. Aqui a luta continua e eles perdem os turnos até acordar.
- **Estados no fim da luta.** O CE levanta o jogador caído no fim da luta, como aqui. As criaturas seguiriam nocauteadas até o evento; aqui elas se levantam junto. Só os membros aleijados ficam.
- **AC de quem perde o turno.** Uma criatura nocauteada fica fora de `_combat_set_move_all` no CE e mantém os PA que tinha. Aqui ela conta todos os PA como AC.
- **Falhas não aplicadas.** Derrubar a arma, acertar outro alvo, perder munição, destruir a arma, explodir, falhar o tiro e pegar fogo são registrados no evento, mas não aplicados.
  - Nenhum ataque natural, nem a mão nua, sorteia esses efeitos (linha 0).
  - A lança e a faca usam a linha 1 (perder o turno, derrubar a arma, acertar outro alvo, acertar a si mesmo), mas só a partir do dia 6.
- **Cegueira.** Não existe nas linhas de torso. O estado é guardado, mas a penalidade de −25 no acerto e −5 em PE não está aplicada.
- **Fora do combate.** Pernas aleijadas impedem correr no CE; o andar do projeto não corre. Nenhum médico trata membros ainda.

## Validação

- **Testes sintéticos: 401 grupos**, 5 novos em `CriticalTests`:
  - **Tabelas:** linhas, faixas, travas de dia, nocaute, pernas e entradas fora de faixa recusadas.
  - **Crítico:** a trava do dia 1, sem rolagem extra no dia 0; o limiar exato; o nocaute da formiga; o maciço do radscorpion; Heavy Handed; e a morte instantânea do jogador pela própria tabela.
  - **Turnos:** levantar por 3 PA, sete turnos perdidos, o despertar no fim da rodada 7, o retorno depois do jogador, e a percepção para o Enter.
  - **Falhas:** perder o turno, cair e levantar na hora, o aleijamento aleatório, a trava do dia 6 do jogador, acertar a si mesmo, a coluna vazia, os braços e as pernas.
  - **Mundo:** o relógio na exploração e nas rodadas, os membros na luta seguinte, o checkpoint e os valores forjados recusados.
- **`--critical-report`:** `CRITICAL_TABLES_OK`, com o `fallout2.exe` identificado por tamanho e SHA-256.
- **Auditoria do Templo** (20 sementes por variante):
  - **Início original (dia 0):** as 160 rodadas são idênticas às da medição do [vaguear](creature-wander-2026-11-01.md), resultado por resultado, e nenhuma tem crítico.
    - Cada passagem gasta de 0,05 a 0,12 h de jogo em média; a mais longa leva 5.790 ticks, ou 9,7 min.
    - A auditoria não conta o tempo parado. Mesmo assim, a distância até as 15,6 h do dia 1 mostra que, na regra original, os críticos ficam para depois do Templo.
  - **Começando no dia 1** (`TEMPLE_RUN_DAY=1`, só para medir):

| Personagem | Rota direta: concluídas (dia 0) | Críticos dados / sofridos por rodada | Com kit: concluídas (dia 0) | Críticos dados / sofridos |
| --- | --- | --- | --- | --- |
| Neutro | 14/20 (16) | 1,2 / 0,35 | 2/20 (2) | 1,4 / 0,45 |
| Narg | 19/20 (18) | 1,15 / 0,25 | 15/20 (15) | 2,0 / 0,7 |
| Mingan | 5/20 (4) | 2,1 / 0,65 | 6/20 (1) | 2,55 / 0,65 |
| Chitsa | 16/20 (14) | 1,15 / 0,4 | 6/20 (6) | 1,3 / 0,35 |

  Leitura dos números:
  - **Fato medido:** nenhuma rodada travou.
  - **Fato medido:** falhas críticas com efeito aparecem em 0,05 a 0,25 vezes por rodada.
  - **Inferência técnica:** os críticos ajudam mais quem luta mais. O Mingan com kit sai de 1 para 6 rodadas concluídas, mas ainda perde a maioria no duelo com Cameron. Com 20 sementes, diferenças de uma ou duas rodadas cabem na variação.
