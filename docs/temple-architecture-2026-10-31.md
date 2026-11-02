# Arquitetura 3D do Temple com o kit modular

31/10/2026 · worktree `C:\Dev\FalloutRemakeClaude`, branch `agent/claude-work`, base `a66f31c`.

`TemplePreview` deixa de usar caixas como representação principal de `ARCAVES`
(mapa 3, três elevações). Paredes, colunas e piso do templo agora são o kit modular
v2 (`Assets/TempleOfTrials/Godot_Modular_glTF/`, alvenaria escura, juntas com terra
e vinhas secas), e as tochas originais ganharam luz quente. **P / Arquitetura 3D**
volta às formas provisórias para comparação. Sem o kit local, a cena continua
funcionando só com as formas provisórias (`TEMPLE_KIT_UNAVAILABLE`).

## Grade e planta original

`src/Geometry/TempleArchitecture.cs`, C# sem Godot, faz o plano:

- **Entrada:** hexes de parede visíveis da elevação, ou seja, objetos `Wall` com
  arte (`block.frm` é bloqueador invisível no CE) mais o cenário "Wall"
  (PIDs `0x02000640`/`0x02000641`, `pro_scen.msg` 160000/160100).
- **Linhas de parede:** uma coluna com linhas consecutivas forma a linha Z. Uma
  sequência de colunas em zigue-zague de meia linha forma a linha X, traçada na
  linha média.
- **Paredes paralelas:** linhas a uma coluna de distância são engrossadas em 25%
  para não deixar fenda entre elas.
- **Módulos:** cada linha vira módulos de 4, 2 e 1 m na escala `KitScale = 0,75`.
  A parede de 2,97 m fica com 2,23 unidades, o dobro do ator provisório. O
  comprimento é ajustado exatamente ao da linha. A ordem dos módulos, a face
  exposta e as vinhas variam de forma determinística pelos hexes.
- **Pilares:** um hex de parede isolado vira coluna (`Pilar_Pedra`). Pontas livres,
  como batentes de porta e terminações, também recebem pilar. Cantos não recebem.
- **Bloqueadores entre paredes:** o sprite original da parede é mais largo que o
  hex, e o mapa preenche os hexes entre objetos de parede com bloqueadores
  invisíveis (`block.frm`, parede ou cenário sem flag `HIDDEN`/`NO_BLOCK`). Um
  bloqueador situado entre dois hexes de parede que não se tocam vira pedra, em
  até duas passagens. Bloqueadores apenas ao lado de uma parede continuam vazios,
  e hexes caminháveis nunca são preenchidos, então os vãos de porta permanecem.
  Placas vermelhas de obstáculo absorvidas pela parede ficam ocultas, mas o
  objeto continua selecionável.
- **Juntas:** `UncoveredLinks` amostra 11 pontos do segmento entre cada par de
  hexes de parede vizinhos. Onde algum ponto ficou fora da pedra (cantos e
  ligações diagonais entre linhas X e Z), um pilar é posto nesse ponto, em até
  quatro rodadas.
- **Piso:** `Tile_Piso_1.73x2.0` cobre exatamente um quadrado de piso
  (√3 × 2 unidades). É usado só nos quadrados com arte `tepflr*`. O preenchimento
  de caverna (`cav4009`/`cav4010`, nenhum quadrado alcançável na elevação 0) fica
  escuro, como o vazio ao redor das salas.

A grade hexagonal e a navegação não mudaram. A passabilidade continua vindo de
`HexNavigation` (FID/flags), e as portas continuam usando `DoorSession`. O kit é
só apresentação: não cria colisão física nova. Os corpos de seleção existentes
ficam com a altura da parede (`Dimensions`), para que o clique e o recorte V
correspondam ao que se vê. As caixas das paredes ficam sem malha, mas preservam
nó, chave fonte, metadados e camada de seleção.

## Integração

| Sistema | Comportamento |
| --- | --- |
| Filtro Paredes / recorte V | Os módulos seguem o filtro Paredes. Um módulo some quando qualquer parede fonte que ele representa é recortada pela vista do personagem. Paredes com flag `HIDDEN` ficam fora do plano. |
| Piso / O | O piso do kit segue o filtro Piso. O piso original (O) substitui o kit sem sobreposição. |
| Portas | Ficam orientadas pelo eixo da parede vizinha, têm 1,95 de altura e cor de bronze. A pose aberta, a seleção e os bloqueios não mudaram. |
| Tochas | `atorch3/4/5` (sobre a linha de parede) recebem suporte e chama na face voltada ao emissor original vizinho (`block.frm`, PID `0x0200008D`, raio 3). Cada emissor gera uma `OmniLight3D` quente com sombra. A luminária provisória fica oculta, mas o objeto continua selecionável. |
| Iluminação | Fundo quase preto, ambiente frio a 0,55, sol frio a 0,45, AgX, SSAO e glow. |
| Importação | `addons/temple_kit_import` (ativado no `project.godot`) e o runtime reaplicam `vertex_color_use_as_albedo` nos materiais `M_Temple_*`/`M_Dry_Vines`, por causa do bug godot#120625 do Godot 4.7.x. |

Os assets ficam fora do Git (`/Assets/` ignorado). No worktree, foi copiado do
checkout principal só o conjunto `.gltf` + `.bin` + `textures/` compartilhado
(cerca de 16 MB). Os `.glb` autocontidos, o `.blend`, os renders e os relatórios
continuam em `C:\Dev\FalloutRemake\Assets\TempleOfTrials\`.

## Validação

- **Testes sintéticos:** 340 grupos (eram 335). Os cinco novos cobrem a linha Z
  exata, o zigue-zague X sem invadir as linhas caminháveis, cantos sem pilar,
  vãos de porta com batentes, colunas isoladas, bloqueadores entre paredes,
  juntas diagonais, passagens sem bloqueador mantidas abertas, determinismo,
  hexes inválidos e paredes paralelas.
- **Mapa real (`TEMPLE_ARCHITECTURE_LAYER_OK`):**

  | Elevação | Paredes fonte | Bloqueadores fechados | Módulos | Ladrilhos | Luzes |
  | --- | ---: | ---: | ---: | ---: | ---: |
  | 0 | 975 | 181 | 1010 | 750 | 33 |
  | 1 | 658 | 106 | 621 | 429 | 20 |
  | 2 | 618 | 98 | 628 | 452 | 23 |

  Toda parede fonte tem módulo. Nenhum par de hexes de parede vizinhos fica com
  vão aberto: antes da correção eram 82, 37 e 50 ligações abertas, mais 82, 42 e
  34 bloqueadores vazios entre paredes. Toda pedra extra está sobre um bloqueador
  original. Nenhum hex caminhável, com as portas abertas, fica sob pedra, nem
  mesmo com margem de 0,1. Os únicos hexes bloqueados que continuam vazios entre
  paredes têm bancos de pedra (`TEMPSF04/05`) em nichos: são 8, 1 e 1, e são props,
  não falta de parede.
- **`TEMPLE_ARCHITECTURE_OK`:** filtros, piso original, alternância P com
  alcance/objetos idênticos, retorno ao kit e bytes do MAP.
- **`tools/Validate-Temple.ps1` (headless):** 20 passos aprovados, com a bateria
  inteira de `--preview-test` e `--nature-test` (portas, arrombamento, viagens,
  checkpoints, diálogo/Speech de Cameron, NPC, itens, recorte, Arroyo e outros).
- **`tools/Run-QuietGodot.ps1`:** passou no desktop isolado em cerca de 114 s, com
  o desktop de entrada `Default` antes e depois. As capturas
  `.local/kit/temple.architecture*.png` foram revisadas.

## Limites e próximos passos

- **Props sem modelo no kit:** banco de pedra (`TEMPSF04/05`), incensário
  (`TEMPSF06`), depressão no piso (`TEMPSF03`), portas de caverna e saídas
  continuam provisórios.
- **Rocha natural:** o kit não tem rocha de caverna. As paredes `ca0xx.frm` também
  usam alvenaria.
- **Repetição do piso:** existem só duas variantes (0° e 180°).
- **Personagens:** criaturas e ator ainda são cápsulas. A escala 0,75 do kit foi
  escolhida em relação ao ator de 1,1 unidade.
- **Câmera e oclusão:** paredes de 2,2 unidades escondem mais área na câmera
  isométrica. O recorte V só atua em torno do personagem. Uma vista em corte
  (paredes baixas no lado da câmera) fica para depois.
- **Comparação visual:** ainda não foi feita lado a lado com o Fallout 2 original.

## Recipientes em nichos (31/10/2026)

Todo pote do Templo fica sobre um bloqueador invisível original (`block.frm`, PID 0x02000043). Três deles ficam entre duas paredes:

- elevação 1, hexes 14080 e 20080;
- elevação 2, hex 20132.

O preenchimento de vãos transformava esses bloqueadores em pedra e escondia o pote.

- **Regra:** `PlanWalls` recebe `keepClear`, os hexes de itens e recipientes visíveis. Um bloqueador num desses hexes nunca vira pedra.
- **`--architecture-clearance-report`:** lista os objetos visíveis sob peças do kit. Restam só tochas, suportes de lança e peças `tempsf04` presas às paredes, além de marcadores invisíveis.
- **Validação:**
  - teste sintético do nicho;
  - `CheckTempleArchitecture` recusa qualquer item sob uma peça do kit;
  - `Validate-Temple` aprovado nos 21 passos;
  - captura isolada `architecture-niche-pot.png`.
- **Runner:** `QuietGodot.cs` ainda limitava o timeout a 300 s. Agora aceita os 900 s que o `.ps1` já anunciava.
