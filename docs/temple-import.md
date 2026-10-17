# Temple of Trials: contrato de importação

## Área correta e estado atual

**Interior: `maps/arcaves.map`, ID 3. Exterior: `maps/artemple.map`, ID 126.**
Esta distinção corrige a identificação do primeiro marco, baseada apenas no nome
`ARTEMPLE`. Foi confirmada pelo cadastro `data/maps.txt`, pelas ligações entre mapas
e pelos scripts de criaturas, portas e armadilhas das instâncias.

O leitor interpreta estruturalmente os dois MAPs até o fim e verifica sua
reconstrução byte a byte em memória. O interior tem 4.528 objetos diretamente no
mapa, 8 registros de inventário e 47 registros ativos de scripts. Números, hashes,
divergências e comandos estão no [registro de validação](validation-objects-2026-10-17.md).

## Formatos e dependências

| Recurso | Regra preservada |
| --- | --- |
| DAT de Fallout 2 | Índice/rodapé little-endian; nomes, compressão zlib, tamanhos e offsets. |
| MAP v20 | Inteiros big-endian; cabeçalho de 236 bytes, variáveis globais/locais, camadas, scripts e objetos. |
| Camadas | 100×100 quadrados por elevação. Bits 2/4/8 indicam camada omitida. Palavra de 32 bits: piso nos 16 baixos, teto nos 16 altos; cada metade tem ID de 12 bits e flags de 4 bits. |
| Scripts no MAP | Cinco categorias, blocos com 16 registros físicos e contagem de slots ativos. Tamanho varia pelo tipo do SID de cada registro, inclusive slots de preenchimento. |
| Objetos no MAP | Contagem total de raízes e contagem por elevação. Cabeçalho de 18 inteiros, metadados de inventário e corpo variável conforme tipo/subtipo PRO. Inventários são recursivos e possuem quantidades. |
| `proto/{tipo}/{tipo}.lst`/`.pro` | Tipo no byte alto do PID; índice baixo de 24 bits baseado em **1** na lista. Comprimento e subtipo PRO validados; atributos completos permanecem em bytes brutos. |
| `scripts/scripts.lst`/`.int` | Índice do cabeçalho MAP baseado em **1**; índice dos registros de script baseado em **0**. Programa associado pelo SID, com origem/hash. INT não é executado. |
| `art/{tipo}/{tipo}.lst`, FRM/FR0… | Arte futura. IDs das camadas indexam a lista de tiles em base zero. FIDs completos são conservados; resolução de arte/variantes ainda não implementada. |
| `data/maps.txt`, EDG | Cadastro usado para identificar as áreas; EDG é extensão de limites e ainda não é interpretado. |

O catálogo não elimina linhas vazias ou comentários para compactar listas: isso
mudaria os IDs. Uma referência a uma linha sem nome válido é rejeitada.

## Representação e limites

`src/Import` é C# independente de Godot, usado pelo console e pela cena de diagnóstico.
`MapDocument` contém a estrutura, dependências e diagnósticos; `MapLayoutWriter`
reconstrói os bytes exclusivamente a partir dos campos interpretados para validação.
`MapPrefix` continua disponível como leitura parcial, sem afirmar que valida o restante.

Preservados: IDs, PIDs, FIDs, SIDs, índice de script, hex, elevação gravada, elevação
do contêiner, rotação, deslocamentos 2D, frame, flags, iluminação, campos desconhecidos,
campos de combate/inventário e destinos das grades de saída. A chave técnica é
`SHA256-do-MAP:offset-do-registro`; IDs originais nunca são substituídos nem presumidos únicos.
Offsets/comprimentos distinguem o próprio registro e a subárvore de inventário.
Capacidade e ponteiros antigos são dados brutos, nunca usados para alocar memória.

A exportação opcional JSON **v2**, apenas em `.local/<mapa>.v2.json`, inclui os bytes
originais do MAP/PRO, campos interpretados, slots não usados, relações de inventário
e origem/hash das dependências efetivamente lidas. Não versionar ou distribuir essa
exportação. Ela não é um pacote completo de arte, áudio, diálogos ou dependências
transitivas de scripts. As listas e programas são referenciados por hash, não embutidos.

Perfil padrão, maior prioridade primeiro: `f2_res.dat`, patches numerados decrescentes,
`data/`, `critter.dat`, `master.dat`. Não interpreta caminhos CFG/Sfall personalizados,
mods, fallback do diretório de trabalho ou saves. Suporta MAP v20, até 64 MiB por
recurso/índice DAT, 100.000 registros de objetos (incluindo inventários), 100.000 scripts
por categoria e profundidade de inventário até 32. PRO de criatura com 412 ou 416 bytes
é aceito; o campo final opcional não é inventado. PID especial de jogador, outros
formatos/subtipos e extensões de PRO são recusados.

Truncamento, contagens incompatíveis, subtipos desconhecidos e bytes extras causam
erro. Divergências de associação (SID ausente, proprietário, índice de script ou
elevação gravada) viram diagnósticos sem apagar/corrigir o valor fonte. Validação
estrutural não confirma o comportamento dos scripts nem a correção visual.

## Próximo incremento: cena de inspeção

Começar pela elevação 0 de `ARCAVES`: piso, paredes, portas, saídas e marcadores com
formas simples. Manter dados fonte separados dos nós Godot e selecionar uma instância
pela chave técnica para consultar IDs/flags. Conferir entrada, saídas, vizinhos,
paridade dos hexes e as seis rotações com pontos de referência do original.

Não confundir quadrados (100×100) com hexes de objetos (200×200). A projeção original
inverte a coluna e considera paridade dos hexes. Escala em metros e altura das malhas
são decisões futuras, não geometria 3D recuperável automaticamente de FRM.
Combate, execução de diálogos e acabamento inspirado em Wasteland 3 ficam para depois.

## Referência técnica

[Fallout 2 Community Edition](https://github.com/alexbatalov/fallout2-ce), commit
`e97087b9582f37075db347a89898887320753f8b`, consultado fora deste repositório.
O leitor C# não incorpora o motor C++.

- [DAT](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/dfile.cc): `dbaseOpen`.
- [MAP](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/map.cc): `mapHeaderRead`, `_square_load`, `mapLoad`.
- [Objetos](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/object.cc): `objectRead`, `objectLoadAllInternal`, `_obj_load_obj`, `objectSaveAll`.
- [PRO e corpos variáveis](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/proto.cc): `_proto_list_str`, `protoRead`, `objectDataRead`.
- [PRO de criatura](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/critter.cc): `protoCritterDataRead`.
- [Scripts](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/scripts.cc): `scriptRead`, `scriptListExtentRead`, `scriptLoadAll`.
- [Coordenadas](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/tile.cc): `tileToScreenXY`, `squareTileToScreenXY`.
- Prioridade de recursos: `game.cc` → `db.cc` → `xfile.cc` no mesmo commit.
  [Licença da referência](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/LICENSE.md).
