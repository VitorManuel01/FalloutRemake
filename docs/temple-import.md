# Temple of Trials: contrato inicial de importação

## Confirmado nos dados locais

`maps/artemple.map`, proveniente de `master.dat`, tem 92.780 bytes, versão 20 e
ID de mapa **126**. O registro correspondente foi confirmado em `data/maps.txt`
de `patch000.dat`. Entrada: hex **18492**, elevação **0**, rotação **0**.
O campo de script é **745**: o CE usa `scriptIndex - 1`, que encontra
`ARTemple.int` na posição 744 de `scripts/scripts.lst` (base zero). Tanto essa lista
quanto `scripts/artemple.int` (4.308 bytes) são resolvidos em `patch000.dat`.

Flags **12**: somente a elevação 0 tem camada de quadrados gravada. Ela contém
10.000 palavras, 167 IDs distintos de piso e um ID distinto de teto.
Não são 10.000 áreas caminháveis: incluem células de preenchimento.
O trecho interpretado termina no byte **40236**; os **52544** bytes restantes
contêm a região de scripts/objetos ainda não interpretada nem validada.

## Formatos e dependências

| Recurso | Papel e regra a preservar |
| --- | --- |
| `master.dat`, `critter.dat`, `patchNNN.dat` | Contêiner DAT de Fallout 2: rodapé com tamanho do índice e do contêiner; índice com nome, compressão, tamanhos e offset. Inteiros little-endian, compressão zlib. |
| `maps/artemple.map` | Inteiros big-endian. Cabeçalho de 236 bytes, variáveis globais, locais, camadas, scripts e objetos. |
| Camadas do MAP | Grade 100×100 por elevação. Bits 2/4/8 do cabeçalho indicam camada omitida. Palavra de 32 bits: piso nos 16 baixos, teto nos 16 altos; cada metade contém ID de 12 bits e flags de 4 bits. |
| `art/tiles/tiles.lst` e `.frm` | IDs dos quadrados indexam a lista de arte em base zero. FRM contém sprites, não malhas 3D. Não reordenar a lista nem remover entradas para compactá-la. |
| `proto/{tipo}/{tipo}.lst` e `.pro` | Protótipos determinam o subtipo e o tamanho variável dos dados de cada objeto. Tipo no byte alto do PID; índice baixo de 24 bits é baseado em 1 na lista PRO. |
| `art/{tipo}/{tipo}.lst`, `.frm`/`.fr0`… | Resolução de FID para arte; preservar o FID completo, incluindo orientação/animação. Critters têm regras próprias de nome. |
| `scripts/scripts.lst`, `scripts/artemple.int` | Associação entre IDs/índices e programas. Neste marco nenhum bytecode é executado. |
| `data/maps.txt` | Registro de mapas, identificação e configuração; usar o arquivo efetivamente resolvido pelos patches. |
| `maps/artemple.edg` | Arquivo encontrado pelo catálogo; limites da extensão de alta resolução. Tratamento e formato ficam para etapa posterior. |

Há recursos chamados `temple01…99.frm` em paredes e `temple01…08.frm` em cenários.
Essa busca por nome identifica **candidatos**, não prova que todos sejam usados pelo
mapa. A lista completa de PIDs/FIDs usados só será conhecida após ler os objetos.
`ACTemVil`, `AITemDor`, `ACKlint` e `AITemCst` também são candidatos identificados
pelas descrições da lista de scripts, ainda sem vínculo de instância confirmado.

## Incremento implementado

`src/Import` é C# independente de Godot, compartilhado com o inspetor de console.
Lê DAT, resolve recursos do perfil padrão e interpreta cabeçalho/variáveis/camadas
de MAP versão 20. Guarda nome bruto, campos reservados, índices, flags, palavras
originais e uma cópia integral do MAP em memória. O trecho restante é opaco;
preservar seus bytes **não equivale a importar seus objetos**.

Prioridade do perfil padrão, da maior para a menor: `f2_res.dat`, patches numerados
em ordem decrescente, `data/`, `critter.dat`, `master.dat`. Derivada da ordem de
registro e busca do CE. Não lê caminhos personalizados de CFG/Sfall, mods, o fallback
do diretório de trabalho nem saves `.sav`. Passe a pasta de idioma explicitamente.
Limite de 64 MiB por recurso e índice DAT; versão 19 é recusada.

## Próximo incremento pequeno: instâncias e protótipos

1. Ler as cinco categorias de scripts e seus blocos de 16 registros, respeitando
   os campos variáveis de scripts espaciais/temporizados. Preservar SIDs, índices,
   offsets, campos desconhecidos e bytes; não executar nem aplicar correções do CE.
2. Resolver PRO por PID antes de ler o corpo variável de cada objeto. Ler as
   contagens por elevação e inventários recursivos, impondo limites de profundidade
   e tamanho. Manter `id`, `pid`, `fid`, `sid`, `scriptIndex`, tile, elevação,
   rotação, flags, offsets de sprite, inventário e campos de saída/transição.
   Usar `(hash do MAP, offset do registro)` como chave técnica adicional; nunca
   substituir IDs originais por IDs do Godot nem assumir que `id` é único.
3. Produzir representação intermediária versionada apenas em `.local/`, com origem
   e hash de cada recurso, offsets e dependências resolvidas. Rejeitar truncamento
   ou subtipo desconhecido sem descartar registros silenciosamente. Conferir que
   os blocos consumidos explicam todo o arquivo e que as contagens são consistentes.
4. Só então criar uma cena de inspeção com formas simples: piso, paredes, portas,
   saída e marcadores. Validar entrada/saída, vizinhos e rotações contra o original.

Geometria: preservar separadamente índice da grade de quadrados (100×100), hex
de objetos (200×200), elevação, rotação e deslocamentos 2D. A projeção original
inverte o eixo da coluna e considera paridade nos hexes; não usar uma conversão
quadrada ingênua. Escala em metros, altura de paredes e malhas são decisões futuras,
não dados recuperáveis automaticamente dos sprites. A transformação para Godot
deve ser separada e testada por pontos de referência, sem alterar coordenadas fonte.
Combate, diálogos executáveis e acabamento visual ficam fora deste marco.

## Referência técnica fixada

[Fallout 2 Community Edition](https://github.com/alexbatalov/fallout2-ce), commit
`e97087b9582f37075db347a89898887320753f8b`. A cópia consultada ficou fora deste
repositório. O leitor foi escrito em C# para este marco; não incorpora o motor C++.

- [DAT](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/dfile.cc): `dbaseOpen`.
- [MAP](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/map.cc): `mapHeaderRead`, `_square_load`, `mapLoad`.
- [Objetos](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/object.cc): `objectRead`, `objectLoadAllInternal`, `_obj_load_obj`.
- [Protótipos](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/proto.cc): resolução de listas, `objectDataRead`.
- [Scripts](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/scripts.cc): `scriptRead`, `scriptListExtentRead`, `scriptLoadAll`.
- [Coordenadas](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/tile.cc): `tileToScreenXY`, `squareTileToScreenXY`.
- Prioridade de recursos: `game.cc` → `db.cc` → `xfile.cc` no mesmo commit.
  [Licença da referência](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/LICENSE.md).
