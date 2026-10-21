# Ensaio separado do estado da anciã

`ArroyoElderStateProfile` projeta os locais e globais dos caminhos textuais
já auditados. Cada pedido cria armazenamento novo e limitado; não recebe ou
modifica `WorldSession`, não entrega itens e não grava checkpoint. O perfil
é somente para a primeira conversa neutra, INT/CAR 5 e sem modificadores.

| Caminho original | Reação final L0 | Ajuste persistente L3 | L1 |
| --- | ---: | ---: | ---: |
| Saudação, sem escolher | 32 | 0 | 0 |
| 106→Node008→136→fim | 22 | -10 | 1 |
| 104→Node006→129→Node006a→Node008→136→fim | 12 | -20 | 1 |
| 104→Node006→130→Node013→153→fim | 12 | -20 | 1 |

Node006a ajusta -10 e chama Node008, que ajusta mais -10. Node013 ajusta -20.
A classificação L1 só é recalculada nos procedimentos de ajuste: não se deve
converter a reação 32 da saudação em L1=2 antecipadamente.

O perfil confere fonte, scripts.lst/10 locais, MSG/arestas via perfil textual,
GAM, PRO da anciã, os três blocos de ajuste e todas as faixas de classificação
originais. O armazenamento tem locais **0–9** da identidade da anciã e apenas
16 globais necessários. Locais 10/11 e blocos de outro NPC são recusados.
O estado inicial já representa o contexto da entrada neutra: G10=2, karma15,
reputação65, Wanderer=1 e G619=1. O ensaio escreve G531=1 e os locais da
conversa em sua própria cópia.

As opções 49/50/51 fornecidas a `giq_option` comandam a reação visual da
cabeça no CE; não equivalem a escrever L0/L1. O ensaio acompanha somente as
escritas explícitas do script. Referência: `game_dialog.cc/_gdProcessChoice`
e `_talk_to_critter_reacts`, revisão `e97087b9582f37075db347a89898887320753f8b`.

Testes: **335 grupos sintéticos**, relatório real
`.local/arroyo-elder-detached-states-audit.json`, fingerprint
`479C27D6759C37BD5A4BBCEBAB26C4FE7FDA25AA03239A3A88A5C14208628A2A`.
São verificados caminhos, fim, repetição após fim, opções indisponíveis,
ordem, limite de escolhas, isolamento de cópias e limites de armazenamento.
Nenhum texto proprietário ou estado ativo foi incluído no Git.
Validação completa final: **20/20 etapas**, builds sem avisos/erros,
`.local/temple-validation-20261021-162703-4598b77f/manifest.json`.

Próximo marco: auditar o ramo original de conversa repetida (Node004) e suas
dependências de conhecimento de áreas/quest, antes de ligar o resultado à
partida e permitir reabrir a conversa com comportamento fiel.
Modelo recomendado: **GPT-6.1 Sol, esforço Alto**.
