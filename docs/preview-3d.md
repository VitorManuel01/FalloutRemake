# Prévia espacial do Temple of Trials

`scenes/TemplePreview.tscn` é a cena principal. Lê `maps/arcaves.map` diretamente
da instalação local, verifica a reconstrução exata e cria volumes de referência.
Configuração privada: `.local/preview-settings.json`, chave `GameDataDirectory`.

## Separação dos dados

- `src/Import`: dados fonte imutáveis, incluindo IDs, bytes e inventários.
- `src/Geometry/FalloutGrid.cs`: transformação de coordenadas sem dependência de Godot.
- `src/Geometry/PreviewLayout.cs`: seleção de camadas e classificação visual.
- `src/Preview`: câmera, primitivas, filtros e inspeção. O mapa não é regravado.

Cada objeto raiz com hex válido produz um nó identificado pelo offset, com metadados
`source_key`, `source_id`, `pid`, `fid` e `hex`. A chave contém hash do MAP e offset;
IDs duplicados são permitidos. Inventários permanecem ligados aos objetos fonte e
não viram elementos soltos no cenário. Registros sem hex (`-1`) não são posicionados.

Os pisos usam MultiMesh. Objetos usam formas simples e corpos estáticos exclusivamente
para seleção por raio. Filtros desligam também a seleção dos objetos escondidos.
Objetos com flag fonte `HIDDEN` ficam ocultos por padrão, assim como marcadores.
Piso ID 1 e pisos com bit 0 das flags não são desenhados, conforme CE.

## Um sistema de coordenadas

Distância entre centros de hexes vizinhos: **1 unidade**, sem significado em metros.
Chão no plano XZ. Para o hex `h`, use `c = 199 - h % 200`, `r = h / 200` inteiro:

```text
X = (c + 1/12) × √3/2
Z = r + (c mod 2)/2 - 5/8
```

Para o quadrado `s`, use `c = 99 - s % 100`, `r = s / 100` inteiro:

```text
X = c × √3
Z = r × 2
```

As constantes alinham os centros lógicos usando `tileToScreenXY` + (16,8) e o
teste de coordenadas `squareTileScreenToCoord` da referência CE. Subtrai-se o centro
do hex de entrada **28906** de ambos: a entrada fica em `(0,0)` em todas as camadas.
A rotação fonte `d` vira a direção `(cos(dπ/3 − π/6), sin(dπ/3 − π/6))` no plano XZ,
mostrada pela seta de seleção. Vizinhos respeitam paridade e limites, sem envolver
uma borda do mapa na outra. Testes conferem essas relações no console.

Deslocamentos `X/Y` do sprite, `ScreenX/ScreenY`, frame e rotação continuam nos dados
originais. Deslocamentos e caches de tela não deslocam a posição lógica do objeto.
Orientação e extensão de malhas de paredes/portas ainda não são inferidas do FID.

## Limites e próximo passo

Alturas, larguras, materiais e iluminação são escolhas provisórias. Esta cena não
recupera modelos 3D dos sprites. A reconstrução espacial usa os índices fonte, mas
a aparência ainda não foi comparada lado a lado com o jogo original. Não há tetos,
arte FRM, execução de scripts ou personagem com regras completas de jogo.

A [camada de navegação](navigation.md) agora classifica bloqueio por FID/flags e move
um marcador na elevação do personagem. É independente das formas usadas para seleção e dos
filtros visuais. A primeira porta já pode ser [aberta e fechada](doors-2026-10-17.md);
a segunda possui [arrombamento com perfil de teste](lockpick-preview-2026-10-18.md).
As [passagens internas entre elevações](travel-2026-10-18.md) já preservam personagem,
portas e XP. Scripts desconhecidos e carregamento de outro MAP continuam indisponíveis.

Referência consultada localmente: [Fallout 2 CE, tile.cc](https://github.com/alexbatalov/fallout2-ce/blob/e97087b9582f37075db347a89898887320753f8b/src/tile.cc),
commit `e97087b9582f37075db347a89898887320753f8b`; também `object.cc` e `obj_types.h`.
