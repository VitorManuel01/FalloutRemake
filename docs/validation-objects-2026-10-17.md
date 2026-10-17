# Segundo marco — concluído em 17/10/2026 (America/Bahia)

Trabalho iniciado em 16/10 e concluído em 17/10.

Branch: `milestone/temple-object-import`, criada a partir de `ca721de`.
Ambiente e dados Epic permanecem os confirmados no primeiro marco.

## Identificação corrigida

O primeiro marco associou o nome `ARTEMPLE.MAP` ao interior por engano. Os objetos
e scripts agora permitem distinguir as áreas: `ARTEMPLE` contém Klint e ligações
para `ARCAVES` e a vila; `ARCAVES` contém os registros de ratos, escorpiões, armadilhas,
portas e `ACTemVil` (personagem do desafio). `data/maps.txt` registra respectivamente
`Arroyo Temple` (126) e `Arroyo Caves` (3). O padrão do inspetor foi corrigido para 3.

| Resultado | Interior `ARCAVES.MAP` | Exterior `ARTEMPLE.MAP` |
| --- | ---: | ---: |
| ID | 3 | 126 |
| Bytes lidos/reconstruídos | 526972 | 92780 |
| Objetos diretamente no mapa | 4528 | 567 |
| Registros em inventários | 8 | 1 |
| Scripts ativos serializados | 47 | 3 |
| PIDs distintos | 146 | 37 |
| FIDs distintos | 140 | 34 |
| Bytes restantes | 0 | 0 |

Interior: entrada **28906/0/0** (hex/elevação/rotação); três camadas de 100×100,
21 variáveis globais e zero locais. Objetos por elevação: **1842 / 1387 / 1299**.
Scripts: 18 espaciais, 7 de item e 22 de criatura. O script de mapa é resolvido
separadamente: cabeçalho 25 → índice 24 de `scripts.lst`.
Foram identificados quatro registros de porta e 74 grades de saída (incluindo
ligações internas e saídas para os mapas 126/4). Não são contagens de portas
abertas ou de saídas distintas jogáveis.

O interior tem 179 dependências resolvidas: 176 de `master.dat`, 3 de `patch000.dat`.
Essas dependências cobrem protótipos/listas e programas referidos; não constituem
um inventário completo de arte, som, diálogos ou dependências transitivas do bytecode.

SHA-256 do MAP interno descompactado:
`BA35474B8EA67AEC50B6A962A1BC767A87366449D4B631FD1B4CE241847B5BFE`.

## Validações

- 18 grupos de testes sintéticos passaram (8 anteriores e 10 estruturais).
  Cobrem blocos de 16 scripts, slots não usados, scripts espaciais/temporizados,
  todos os subtipos de item/cenário, criaturas, grades de saída, listas em base 1,
  inventário aninhado, IDs duplicados, dados truncados, contagens incoerentes,
  subtipo desconhecido, limite de recursão e dependências ausentes.
- Reconstrução em memória a partir dos campos interpretados: igualdade byte a byte
  para os dois MAPs. Nenhum bloco do MAP é copiado como fallback pelo reconstrutor.
- `dotnet build 'Fallout Remake.csproj'`: zero erros e zero avisos.
- Cena Godot .NET: `IMPORT_SMOKE_OK: ARCAVES.MAP; map ID 3; 4528 root objects;
  526972 bytes parsed; exact reconstruction verified.`, saída 0.
- Exportação JSON v2 reaberta: hash dos bytes de origem e contagem de objetos
  preservados. Arquivo local de aproximadamente 5,9 MB, ignorado pelo Git.

Uma divergência original foi registrada: objeto ID **90**, offset **355536**, hex
**21522**, elevação **1**, SID **50331650**. Seu índice de script gravado é **-1**;
o registro desse SID aponta para **511**, `animfrvr.int`. O importador preserva os
dois valores e emite `script-index`; não faz a correção que o motor aplica em memória.
Nenhum SID ausente ou conflito de proprietário foi encontrado nesses dois mapas.

Relatórios privados: `.local/arcaves-inspection.json`, `.local/arcaves.v2.json` e
`.local/temple-objects-inspection.json` (exterior). Comandos no README.

## Próximo marco

Criar uma cena de inspeção 3D com formas simples a partir de `ARCAVES`, começando
pela elevação 0. Separar transformação geométrica e dados fonte; conferir entrada,
paredes, portas, grades de saída e orientação antes de navegação/combate.
Ainda não há execução de INT, interpretação de todos os atributos PRO, resolução
completa de FID→arte, reprodução de FRM, colisão ou confirmação visual no jogo original.
