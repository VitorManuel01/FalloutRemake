# Registro de Exclusões de Lore e Potencial de Adaptação de Assets de Fallout 4

> **Data:** 28/10/2026  
> **Classificação Normativa:** **Categoria F (Incompatível com Fallout 2 para Uso Direto)**  
> **Total de Assets Classificados:** 185 modelos GLB  
> **Localização Atual:** Preservados integralmente em `D:\FalloutRemakeAssets`  
> **Diretriz de Segurança:** Nenhum asset foi excluído permanentemente; todos permanecem catalogados com rastreabilidade completa.

---

## 1. Princípios de Compatibilidade de Lore em FalloutRemake

O projeto **FalloutRemake** prioriza a reconstrução fiel de **Fallout 2**, cuja narrativa transcorre no ano de **2241** no norte da Califórnia e Nevada (Costa Oeste dos EUA). 

A biblioteca de assets importada de Fallout 4 (ambientado em **2287** em Boston, Massachusetts — Costa Leste) contém estruturas e adereços narrativos específicos daquela região e época. A inserção desses elementos em Fallout 2 violaria a ambientação pós-apocalíptica árida, a cronologia histórica e a identidade artística do jogo clássico.

---

## 2. Inventário Detalhado dos 185 Assets Excluídos

```text
================================================================================
DISTRIBUIÇÃO DOS 185 ASSETS CLASSIFICADOS COMO CATEGORIA F
================================================================================
Grupo / Categoria Temática       Qtd Modelos  Origem / Lote   Motivo Principal da Exclusão
--------------------------------------------------------------------------------
1. Dirigível Militar Prydwen      34 modelos   Lote 93         Anacronismo cronológico e geográfico da BoS
2. Monumentos Cívicos de Boston   34 modelos   Lote 100        Patrimônio histórico exclusivo de Massachusetts
3. Estádio Fenway / Diamond City  34 modelos   Lote 88         Arquitetura cívica/esportiva de Boston
4. Metrô Subterrâneo MBTA Boston  68 modelos   Lotes 99 e 86   Sinalização e infraestrutura metropolitana local
5. Tecnologia Sintética Instituto 15 modelos   Props/Arch      Facção exclusiva da Commonwealth de 2287
--------------------------------------------------------------------------------
TOTAL INTEGRAL CATEGORIA F:      185 modelos  (Preservados no repositório externo)
================================================================================
```

---

## 3. Análise Individual por Grupo Temático

### Grupo 1: Dirigível Militar Prydwen (34 modelos — Lote 93)
- **Modelos:** `AirshipHullPlates*.glb`, `AirshipGantry*.glb`, `AirshipGangway*.glb`, `AirshipConcourse*.glb`, `AirshipBridgeArch*.glb`.
- **Origem:** `Fallout4 - Meshes.ba2` (`Architecture/AirshipRigging/`).
- **Incompatibilidade:** O dirigível militar Prydwen foi projetado e construído pela divisão de Boston da Irmandade do Aço por volta de 2282, utilizando tecnologia da base da Força Aérea Adams. Em 2241 (Fallout 2), a Irmandade da Costa Oeste operava em bunkers subterrâneos austeros (como em Lost Hills, Den e San Francisco).
- **Potencial de Adaptação:** Baixo para o dirigível completo. Vigas de sustentação e ferragens de passadiço (`AirshipGantry`) podem ser reaproveitadas isoladamente em instalações militares da Enclave (Navarro) ou no petroleiro Poseidon Oil de San Francisco.

---

### Grupo 2: Monumentos Históricos e Esculturas Cívicas de Boston (34 modelos — Lote 100)
- **Modelos:** `StatuePaulRevere*.glb`, `StatueBunkerHillMonument*.glb`, `FaneuilHallCivicCol*.glb`, `BostonCommonMemorial*.glb`, `StatueGeorgeWashington*.glb`.
- **Origem:** `Fallout4 - Meshes.ba2` (`Props/Statues/` e `Architecture/Neoclassical/`).
- **Incompatibilidade:** Estátuas de heróis da Revolução Americana do século XVIII e marcos patrióticos de Massachusetts não existem nos desertos da Califórnia, nas aldeias tribais ou nos assentamentos de caçadores de Fallout 2.
- **Potencial de Adaptação:** Quase nulo para uso direto. Os pedestais de granito e degraus de calcário podem ser reaproveitados em praças centrais de cidades avançadas como Vault City ou a capital da NCR.

---

### Grupo 3: Estádio Fenway Park / Diamond City (34 modelos — Lote 88)
- **Modelos:** `StadiumBleachers*.glb`, `DiamondCityTower*.glb`, `GreenMonsterWall*.glb`, `DugoutInnStairs*.glb`.
- **Origem:** `Fallout4 - Meshes.ba2` (`Architecture/DiamondCity/`).
- **Incompatibilidade:** Arquitetura do estádio de beisebol Fenway Park adaptada como cidade comercial de Boston.
- **Potencial de Adaptação:** Chapas de compensado verde e sucata solta podem ser desmontadas para compor barracos em The Den ou Klamath.

---

### Grupo 4: Rede de Metrô Subterrâneo de Boston (68 modelos — Lotes 99 e 86)
- **Modelos:** `SubwayPlatformTiles*.glb`, `SubwaySignBostonTransit*.glb`, `SubwayTurnstileMBTA*.glb`, `SkyscraperFinDistrict*.glb`.
- **Origem:** `Fallout4 - Meshes.ba2` (`Architecture/Subway/` e `Architecture/Skyscraper/`).
- **Incompatibilidade:** Placas com o mapa de linhas da MBTA (Massachusetts Bay Transportation Authority), nomes de paradas e estações de Boston.
- **Potencial de Adaptação:** Pisos de cerâmica rachada, trilhos enferrujados e catracas genéricas podem ser reaproveitados em túneis industriais e esgotos de New Reno após a remoção de texturas de sinalização.

---

### Grupo 5: Tecnologia e Arquitetura Sintética do Instituto (15 modelos)
- **Modelos:** Portais brancos curvos, mobiliário asséptico translúcido e consoles sintéticos de laboratório polido.
- **Origem:** `Fallout4 - Meshes.ba2` (`Architecture/` e `Props/`).
- **Incompatibilidade:** A facção *The Institute* e sua estética de ficção científica asséptica são exclusivas de Massachusetts. Fallout 2 adota a estética retrofuturista dieselpunk/atompunk dos anos 1950 (computadores de válvula, fita magnética e aço fundido).
- **Potencial de Adaptação:** Zero para a superfície. Rejeição total de uso em qualquer cenário de Fallout 2.

---

## 4. Regras para os Agentes Futuros (Codex & Claude)

1. Nunca utilize qualquer arquivo classificado como Categoria F em mapas de Arroyo, Templo dos Testes, Klamath, Den, Modoc, Vault City, Gecko, Redding ou NCR.
2. Em caso de necessidade de componentes genéricos (placas de metal neutras, blocos de concreto bruto), certifique-se de que o modelo não possui marcas registradas, nomes de cidades de Boston ou insígnias visíveis.
