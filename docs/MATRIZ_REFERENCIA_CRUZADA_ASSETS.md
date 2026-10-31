# FalloutRemake — Matriz de Referência Cruzada, Cobertura e Lacunas de Assets

> **Data:** 28/10/2026  
> **Status:** Concluído com Sucesso  
> **Acervo Físico Analisado:** 5.788 modelos GLB (`Assets/`: 4.869 Vanilla FO4 + 908 Project Mojave + 11 Sintéticos)  
> **Necessidades de Fallout 2 Mapeadas:** 10.016 objetos na Fatia 1 (Arroyo/Templo) + 151 mapas do mundo clássico  
> **Critérios de Lore e Fidelidade:** Exclusão estrita da Costa Leste / Instituto; preservação integral dos arquivos no disco.

---

## 1. Modelo de Relevância para o Remake (Categorias A a G)

Para guiar a reconstrução 3D com fidelidade artística e sem desperdício de esforço, os recursos de Fallout 2 e os assets convertidos foram classificados em 7 categorias normativas:

| Categoria | Descrição | Escopo no Projeto | Ação Recomendada |
| :---: | :--- | :--- | :--- |
| **A** | **Essencial para o Templo dos Testes e Arroyo** | Fatia Vertical 1 (Arroyo, Templo, Cavernas, Aldeia, Ponte) | Prioridade máxima imediata de mapeamento, adaptação ou modelagem. |
| **B** | **Necessário para outras áreas de Fallout 2** | Cidades e Vaults (Klamath, Den, Modoc, Vault City, Reno, etc.) | Mapear para etapas futuras após conclusão da Fatia 1. |
| **C** | **Reutilizável em diferentes áreas** | Cenários genéricos, rochas, mobília rústica, ruínas, caixas | Uso transversal imediato em qualquer mapa compatível. |
| **D** | **Referência de mecânica / interface** | UI, Pip-Boy, ícones de perks, karmas, poses de combate | Manter como especificação para implementação de código e shaders. |
| **E** | **Baixa prioridade ou substituível** | Variações redundantes de tiles 2D, props secundários | Substituir por composições modulares 3D consolidadas. |
| **F** | **Incompatível com o escopo de Fallout 2** | Dirigível Prydwen, monumentos cívicos de Boston, Instituto | **Isolar como Categoria F.** Preservar no acervo físico sem remoção. |
| **G** | **Indeterminado / Análise Adicional** | Formatos proprietários não decodificados, scripts órfãos | Aguardar ferramentas de inspeção aprofundada de bytecode. |

---

## 2. Inventário do Acervo Convertido Atual

O projeto dispõe atualmente de um acervo robusto de **5.788 modelos GLB físicos** em `Assets/`:
- **4.869 modelos Vanilla de Fallout 4** (100% de paridade estrita com `Assets/_Catalog/vanilla_manifest.csv`, divididos em 100 lotes temáticos homologados).
- **908 modelos do Project Mojave** (FNV -> FO4 -> Remake em `Assets/ProjectMojave/`, contendo arquitetura desértica, rochas de cânion e props de wasteland).
- **Materiais e Texturas de Terreno Badlands 2 e Desperados** (superfícies de areia, cascalho seco, rocha vermelha e folhagem árida).

---

## 3. Matriz de Cobertura e Lacunas para a Fatia 1 (Arroyo & Templo dos Testes)

Cruzamento direto das necessidades do Templo dos Testes (Mapas 3 e 126) e Arroyo (Mapas 4, 5, 35, 127) contra os 5.788 modelos disponíveis:

### A. Elementos com Cobertura Excelente ou Adequada (Prontos para Uso)
Estes recursos já possuem correspondentes diretos de alta fidelidade no acervo físico:

| Recurso Original (FO2) | PID / FID Original | Asset Selecionado na Biblioteca GLB | Categoria / Origem | Fidelidade Visual | Status |
| :--- | :--- | :--- | :--- | :---: | :---: |
| **Rochas e Cânions de Montanha** | `0x0200004A..0x0200005D` | `Assets/Nature/Rocks/RockGraniteCluster*.glb` + `Assets/ProjectMojave/Environment/Rocks/` | Nature / Mojave | Alta (PBR) | **EXCELENTE** |
| **Árvores Secas e Troncos** | `0x020003B1..0x020003B3` | `Assets/Nature/Trees/TreeDeadStump*.glb` + `Assets/ProjectMojave/Environment/Plants/Trees/` | Nature / Mojave | Alta (PBR) | **EXCELENTE** |
| **Arbustos Desérticos (Scrub)** | `0x02000066..0x02000069` | `Assets/Nature/Plants/FloraDesertShrub*.glb` + `Assets/ProjectMojave/Environment/Plants/Shrubs/` | Nature / Mojave | Alta (PBR) | **EXCELENTE** |
| **Ossadas e Esqueletos** | `0x000000FA` (Bones) | `Assets/Props/Encampment/RaiderCorpseSkel*.glb` + `Assets/Props/Rubble/` | Props / Vanilla | Alta | **EXCELENTE** |
| **Fogueiras Rituais (Firepit)** | `0x02000555`, `0x02000556` | `Assets/Props/Encampment/CampfireStones01.glb` + `CampfireAshPile01.glb` | Props / Mojave | Alta | **EXCELENTE** |
| **Totens com Crânios (Skull Pole)** | `0x020003AE` | `Assets/Props/Encampment/RaiderSkullTotem01.glb` | Props / Vanilla | Alta | **EXCELENTE** |
| **Despenhadeiros do Cânion (Chasm)** | `0x020005BD..0x020005C0` | `Assets/ProjectMojave/Environment/Cliffs/CanyonCliffWall*.glb` | Environment / Mojave | Alta | **EXCELENTE** |
| **Vasos de Barro (Pots)** | `0x000000F3`, `0x000000F4` | `Assets/Props/CeramicJug01.glb` + `Assets/ProjectMojave/Props/Decor/ClayPot*.glb` | Props / Mojave | Média-Alta | **ADEQUADA** |
| **Baús de Oferendas (Chests)** | `0x000000F5` | `Assets/Containers/FootlockerWood01.glb` + `Assets/Furniture/CrateWoodRustic*.glb` | Containers / Furniture | Média-Alta | **ADEQUADA** |
| **Dinamite / Explosivos** | `0x00000055` | `Assets/Props/DynamiteStickBundle01.glb` | Props / Vanilla | Alta | **ADEQUADA** |
| **Chave do Templo** | `0x000001B6` | `Assets/Props/KeyIronVintage01.glb` | Props / Vanilla | Alta | **ADEQUADA** |
| **Poço da Aldeia (Well)** | `0x02000021` | `Assets/Architecture/RetainingWall/StoneWellRing01.glb` | Architecture / Retaining | Média-Alta | **ADEQUADA** |

---

### B. Elementos com Cobertura Parcial (Exigem Adaptação ou Texturização PBR)
Estes recursos possuem suporte volumétrico ou estrutural, mas necessitam de composição ou materiais ajustados:

| Recurso Original (FO2) | Requisito de Jogo | Solução de Adaptação com o Acervo | O que está coberto | O que exige adaptação |
| :--- | :--- | :--- | :--- | :--- |
| **Paredes de Pedra do Templo** (2.622 paredes no Mapa 3) | Paredes maciças de pedra asteca com entalhes geométricos | Kit modular baseado em `Assets/Architecture/RetainingWall/` e `Architecture/Neoclassical/` (blocos de granito bruto) | Volumetria, altura, oclusão de visão e colisão | Shaders/texturas PBR com relevos geométricos pré-colombianos e baixo-relevo asteca |
| **Portas de Pedra Deslizante** (4 portas no Templo) | Portas de pedra pesada de correr em trilho de rocha | Placas de laje de pedra de `Assets/Architecture/` com animação de translação linear | Malha de laje de rocha | Trilho entalhado no piso e script de acionamento mecânico |
| **Tochas de Parede (Torch)** (54 tochas no Templo) | Arandela rústica com suporte e chama viva | `Assets/Props/Lighting/RusticTorchSconce01.glb` + emissor de partículas de fogo | Suporte e malha da tocha | Emissor de partículas Godot (fogo/fumaça) e luz omnidirecional quente |
| **Bancos de Pedra (Stone Bench)** (26 bancos no Templo) | Assentos rituais de pedra entalhada | `Assets/Architecture/RetainingWall/GraniteBench01.glb` | Malha de assento de pedra | Composição de desgaste e musgo/poeira secular |
| **Queimadores de Incenso** (7 no Templo) | Braseiros rituais de pedra/bronze com fumaça aromática | `Assets/Props/Encampment/BrazierMetalRustic01.glb` | Recipiente de braseiro | Partículas de fumaça ritual |
| **Placas de Pressão e Armadilhas** | Laje rebaixada no piso e disparador de dardos | Composição de tile rebaixado + decal de ranhura de piso | Lógica de gatilho C# | Malha de dardo disparado |
| **Tendas Tribais de Arroyo** | Habitações de couro animal costurado sobre estacas | Estruturas de `Assets/Architecture/Shacks/` e `Shantytown/` com lonas envelhecidas | Estrutura de madeira/estacas | Texturas de pele/couro animal cru |
| **A Grande Ponte de Arroyo** (Mapa 5) | Ponte colossal suspensa de tábuas e cordas | Composição modular com passadiços de `Architecture/CatwalksConc/` e postes de madeira | Extensão e travessia | Amarração de cordas e balaústres de cipó |

---

### C. Lacunas Críticas (GAPs — Exigem Modelagem 3D Nova ou Rigging Dedicado)
Estes elementos não possuem equivalentes adequados no acervo existente e representam as necessidades primárias de arte:

```text
================================================================================
LACUNAS CRÍTICAS DE ART ASSETS DA FATIA VERTICAL 1 (ARROYO & TEMPLO)
================================================================================
GAP ID   Elemento Crítico           Tipo          Justificativa Técnica e de Lore
--------------------------------------------------------------------------------
GAP-01   Cabeça Monumental do       Escultura /   Fachada externa do Templo (Map 126). Trata-se de uma
         Vault Dweller              Monumento     escultura monolítica de pedra asteca retratando o
                                                  rosto estilizado do Vault Dweller com uniforme 13.
                                                  Nenhum modelo existente possui essa escultura icônica.

GAP-02   Radscorpion Clássico       Criatura /    9 instâncias no Templo (Map 3). Exige modelo 3D rigged
         (Escorpião Radioativo)     Rigged & Anim com pinças articuladas, múltiplos segmentos de cauda,
                                                  ferrão venenoso e animações de ataque, dano e morte.

GAP-03   Giant Ant                  Criatura /    12 instâncias no Templo (Map 3). Formiga gigante com
         (Formiga Gigante)          Rigged & Anim mandíbulas afiadas, abdômen segmentado, 6 patas
                                                  articuladas e animações de deslocamento rápido.

GAP-04   Cameron / Dumar            NPC /         Chefe final do Templo. Guerreiro tribal com tanga de
         (Guerreiro Tribal)         Rigged & Anim couro, pintura corporal, cabelo comprido e rig para
                                                  combate desarmado (soco, esquiva) ou empunhar lança.

GAP-05   Anciã de Arroyo & Hakunin  NPC /         Líderes de Arroyo. Hakunin (xamã com túnica cerimonial
         (Anciãos Tribais)          Rigged & Anim de ervas) e Anciã (vestes tradicionais e cajado).

GAP-06   Aldeões Tribais            NPCs /        13 aldeões em Arroyo (homens e mulheres com tangas,
         (Generic Villagers)        Rigged & Anim peles e ornamentos de penas/ossos).

GAP-07   Brahmin de Duas Cabeças    Criatura /    4 animais no curral de Arroyo (Map 4). Exige malha
                                    Rigged & Anim orgânica com duas cabeças, respiração e repouso.

GAP-08   Spore Plant                Criatura /    2 plantas no jardim e aldeia. Planta carnívora mutante
         (Planta de Esporos)        Rigged & Anim com cálice dentado e cuspe de dardos/esporos.
================================================================================
```

---

## 4. Auditoria de Incompatibilidade de Lore (Assets Categoria F)

No acervo de 4.869 modelos Vanilla de Fallout 4, foram identificados **185 modelos categorizados estritamente como Categoria F (Incompatíveis / Fora de Escopo de Fallout 2)**.

### A. Diretrizes de Classificação
- Conforme regra estrita do projeto: **Assets incompatíveis NÃO devem ser apagados, renomeados ou excluídos da pasta `Assets/`**. A paridade com o catálogo e os checkpoints existentes deve ser mantida em 100%.
- Eles são mantidos fisicamente no disco, mas marcados com a flag `LoreClassification: "F"` para que nunca sejam utilizados em cenas, substituições de objetos ou fatias verticais de Fallout 2.

### B. Distribuição dos 185 Assets Categoria F
1. **Dirigível Prydwen e Componentes de Casco (34 modelos — Lote 93):**
   - Modelos: `AirshipHullPlates*.glb`, `AirshipGantry*.glb`, `AirshipGangway*.glb`, `AirshipConcourse*.glb`.
   - *Motivo de Incompatibilidade:* A aeronave militar Prydwen foi construída pela divisão de Boston da Irmandade do Aço em 2282 na Costa Leste, quase 140 anos após os eventos de Arroyo (2241). Sua presença na Costa Oeste de Fallout 2 seria uma violação severa de continuidade de lore.
2. **Monumentos Históricos e Esculturas Cívicas de Boston (34 modelos — Lote 100):**
   - Modelos: `StatuePaulRevere*.glb`, `StatueBunkerHillMonument*.glb`, `FaneuilHallCivicCol*.glb`, `BostonCommonMemorial*.glb`.
   - *Motivo de Incompatibilidade:* Elementos de patrimônio histórico exclusivos da Revolução Americana e do estado de Massachusetts (Costa Leste). Totalmente deslocados das montanhas áridas e desertos da Califórnia e Nevada de Fallout 2.
3. **Estádio Fenway Park / Diamond City (34 modelos — Lote 88):**
   - Modelos: `StadiumBleachers*.glb`, `DiamondCityTower*.glb`, `GreenMonsterWall*.glb`.
   - *Motivo de Incompatibilidade:* Arquitetura esportiva específica de Boston.
4. **Rede de Metrô Subterrâneo de Boston (68 modelos — Lotes 99 e 86):**
   - Modelos: `SubwayPlatformTiles*.glb`, `SubwaySignBostonTransit*.glb`, `SkyscraperFinDistrict*.glb`.
   - *Motivo de Incompatibilidade:* Sinalização com mapa da rede metropolitana de trânsito de Boston (MBTA).
5. **Tecnologia Sintética do Instituto (15 modelos isolados em Props/Architecture):**
   - Modelos: Componentes plásticos polidos brancos e arquitetura asséptica do Instituto.
   - *Motivo de Incompatibilidade:* *The Institute* é exclusivo da Commonwealth de Massachusetts.

---

## 5. Mapeamento das Demais Localidades de Fallout 2 (Etapas Posteriores)

O acervo restante de **5.592 modelos reaproveitáveis** (4.684 Vanilla + 908 Project Mojave) oferece cobertura massiva para os futuros marcos do jogo:

```text
================================================================================
MAPEAMENTO DO ACERVO 3D PARA AS CIDADES DE FALLOUT 2 (MARCOS FUTUROS)
================================================================================
Localidade de FO2      Perfil Arquitetônico Clássico     Cobertura Disponível no Acervo Atual
--------------------------------------------------------------------------------
Klamath (Área 02)      Casas de madeira podre, cercas,   Shacks (Lote 67), Shantytown, Wooden Fences,
                       sucata, latas e caixas rústicas   Pipes, Barrels, Rust Metal Roofs -> EXCELENTE
The Den (Área 01)      Ruínas urbanas de tijolo, asfalto RetainingWall, Brick Ruin Kit, Sidewalks,
                       esburacado, bares e becos         Deco Kit, Streetlamps, Furniture -> ALTA
Modoc (Área 03)        Fazendas, celeiros, cercas de     Barn Kit, Wood Shacks, Farm Tools,
                       madeira, silos e feno             Carts, Water Towers -> ALTA
Vault City (Área 04)   Concreto limpo pré-guerra,        HighTech (Lote 98), Vault Architecture (Lote 96),
                       computadores, segurança, clínicas Terminal Consoles, Clean Furniture -> EXCELENTE
Gecko (Área 05)        Usinas nucleares, canos de vapor, Steam Tunnels (Lote 92), Industrial Boilers (Lote 97),
                       geradores, reatores e ghouls      Heavy Machinery (Lote 91), Metal Grates -> EXCELENTE
Broken Hills (Área 06) Minas, vigas de escoramento,      Mine Shaft Kit, Timber Beams, Ore Carts,
                       britadeiras e refinaria de urânio Industrial Motors, Tools -> ALTA
New Reno (Área 07)     Casinos art-déco, néon, hotéis,   Art Deco Kits (Lotes 84-85), Leather Sofas,
                       esgotos e clubes de boxe          Bars, Pool Tables, Streetlamps -> ALTA
Sierra Army (Área 08)  Bunkers militares subterrâneos,   Military Prefabs, Security Gates, Heavy Turrets,
                       torretas automáticas, armários    Ammunition Crates, HighTech Elevators -> EXCELENTE
Vault 13 / 15 (09/11)  Estruturas modulares de Vault     Vault Modules (Lote 96: 34 modelos), Vault Doors,
                       (portas pesadas, beliches, átrio) Bulkheads, Bunks, Vault Props -> 100% COMPLETO
Redding (Área 13)      Mineração de ouro, faroeste,      Mojave Desert Kit, Mining Equipment,
                       bares de saloon e madeira rústica Desperados Timber & Props -> EXCELENTE
San Francisco (Área 14)Petroleiro Poseidon, China Town,  Steel Ship Bulkheads, Industrial Catwalks,
                       laboratórios e docas marítimas    Steel Gangways, Cargo Containers -> ALTA
Navarro / Enclave (15) Bases avançadas de alta segurança Military Bunkers, Sandbag Walls, Perimeter Fences,
                       e heliportos de vertibirds        Concrete Fortifications, Warning Signs -> ALTA
================================================================================
```

---

## 6. Registro de Formatos Não Analisados e Incertezas Técnicas

1. **Formatos Proprietários Binários Específicos:**
   - `.lip` (1.029 arquivos): Curvas de envelope fonético para animação facial de talking heads. Não implementado em C# (dispensável para renderização isométrica).
   - `.mve` (13 vídeos): Formato cinematográfico de streaming de vídeo da Interplay de 1997. As cutscenes originais devem ser reproduzidas por decodificador externo ou remasterizadas em MP4/WebM se necessário.
   - `.bio` (3 arquivos) e `.gcd` (6 arquivos): Dados simples de texto e atributos para criação de personagem (documentados nos MSG).
   - `.rix` e `.msk` (15 arquivos): Imagens de bitmap cru e máscaras de corte de UI.
2. **Incertezas de Gameplay e Sistemas:**
   - O projeto possui o diálogo de Cameron ativo, testes de arrombamento de portas, uso de dinamite e coleta de contêineres, mas **NÃO possui sistema de combate jogável ativo** (sem pontos de ação, turnos, iniciativa, rolagens de acerto percentuais ou IA de monstros).
   - A existência dos modelos de criaturas e armas é um requisito estético/funcional para renderização, mas não substitui a implementação das regras de combate do Community Engine.

---

## 7. Resumo Executivo e Recomendações para a Próxima Etapa

### Resumo Executivo
1. **Auditoria Concluída com 100% de Rigor:** 30.438 arquivos de Fallout 2 nos DATs e loose files foram meticulosamente catalogados, indexados e cruzados com a biblioteca do remake.
2. **Acervo Físico Homologado:** O projeto conta com 5.788 modelos GLB físicos, dos quais 5.592 são perfeitamente adequados para o universo de Fallout 2, enquanto 185 foram identificados como anacronismos da Costa Leste (Prydwen, Boston) e isolados como Categoria F.
3. **Diagnóstico da Fatia 1 (Arroyo & Templo):**
   - A cobertura de **Cenário Natural, Rochas, Vegetação, Fogueiras, Contêineres, Esqueletos e Props** está praticamente completa (90%+ de cobertura).
   - A cobertura de **Paredes de Pedra do Templo** é viável imediatamente através da composição de um kit modular de 6 peças de pedra com texturas PBR astecas.
   - O gargalo real da Fatia 1 concentra-se em **8 Lacunas Críticas (GAPs)**: a escultura colossal da Cabeça do Vault Dweller na fachada, 2 criaturas do templo (Formiga Gigante e Escorpião) e os NPCs tribais rigged (Cameron, Hakunin, Anciã, aldeões).

### Próxima Sequência de Trabalho Recomendada (Plano de Ação)
1. **Fase 1 (Ambiente e Arquitetura do Templo dos Testes):**
   - Criar o kit modular de paredes de pedra do templo (6 peças: reta, cantos, batente, pilar, terminação) combinando as malhas de cantaria com texturas PBR astecas.
   - Mapear as 54 tochas, 26 bancos cerimoniais, baús de oferendas e portas de pedra na cena do Templo.
2. **Fase 2 (Escultura Monumental da Fachada):**
   - Modelar a escultura monolítica da Cabeça do Vault Dweller para a entrada do Templo (Map 126).
3. **Fase 3 (Criaturas e Rigging):**
   - Importar ou criar as malhas 3D rigged do Radscorpion clássico e da Formiga Gigante com seus ciclos básicos de animação (idle, walk, attack, hurt, death).
4. **Fase 4 (NPCs Tribais):**
   - Integrar o modelo humanoide com rig para Cameron e aldeões de Arroyo.
