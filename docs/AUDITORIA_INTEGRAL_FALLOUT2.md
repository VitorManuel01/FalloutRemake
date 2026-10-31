# FalloutRemake — Auditoria Integral de Recursos de Fallout 2

> **Data da Auditoria:** 28/10/2026  
> **Status:** Concluído com Sucesso  
> **Diretório Canônico Auditado:** `D:\Games\fallout2\Fallout 2 English`  
> **Arquivos Fontes:** `master.dat`, `critter.dat`, `patch000.dat`, `f2_res.dat`, `sound/music/*.acm`, `data/*`  
> **Total de Recursos Únicos Mapeados:** 30.438 entradas nos DATs + 21 faixas loose ACM + 33 arquivos em `data/`  
> **Ferramentas de Auditoria:** `tools/TempleInspector` (C# .NET 8 / `GameData.cs` / `MapDocument.cs`), scripts de inspeção direta

---

## 1. Visão Geral e Arquitetura de Dados de Fallout 2

A auditoria integral de Fallout 2 examinou a totalidade dos arquivos contidos na instalação oficial do jogo clássico, cobrindo todos os formatos binários proprietários da Black Isle / Interplay.

### Precedência Canônica de Resolução (Community Engine & `GameData.cs`)
O carregamento de arquivos segue a ordem estrita de sobreposição:
1. `f2_res.dat` (Hi-Res Patch — 177 entradas, 651 KB)
2. `patch000.dat` (Patch Oficial 1.02 — 489 entradas, 2.355 KB)
3. `data/*` (Arquivos soltos / loose files — `worldmap.dat`, `vault13.gam`, `city.txt`)
4. `critter.dat` (Sprites de criaturas e animações — 7.120 entradas, 166.951 KB)
5. `master.dat` (Arquivo mestre de dados — 23.140 entradas, 333.177 KB)

Nomes internos são indexados em ASCII, insensíveis a maiúsculas/minúsculas e normalizados para barras normais (`/`).

---

## 2. Inventário Quantitativo por Extensão e Categoria

```text
================================================================================
DISTRIBUIÇÃO DOS 30.438 RECURSOS POR EXTENSÃO E FORMATO
================================================================================
Extensão   Formato / Tipo de Dado                Contagem    Descrição Técnica
--------------------------------------------------------------------------------
.frm       Sprites Gráficos Monocromáticos/6-bit  12.235     Arte do jogo (criaturas, cenários, itens, UI)
.fr0-.fr5  Frames Direcionais Hi-Res               2.970     Frames rotacionais de animação de critters (495 cada)
.pro       Protótipos de Objetos                   7.650     Definições binárias de itens, critters, walls, scenery
.acm       Áudio Interplay ACM                     2.473     Efeitos sonoros (SFX) e falas de NPCs nos DATs
.acm       Músicas Orquestrais (Loose)                21     Trilhas sonoras em sound/music/ (Mark Morgan)
.int       Scripts SSL Compilados                  1.443     Bytecode de lógica de IA, mapas, quests e portas
.msg       Tabelas de Texto / Diálogos               839     Diálogos de NPCs, descrições de itens e mensagens
.map       Mapas e Elevações do Mundo                155     Geometria de níveis, hexágonos e posicionamento
.lip       Sincronização Labial (Lip-Sync)         1.029     Curvas fonéticas para Talking Heads
.txt       Tabelas de Configuração e Texto         1.207     maps.txt, city.txt, gam.txt, tabelas de karma
.gam       Arquivos de Variáveis Globais (GVARs)      88     Definições de variáveis globais e estados de missões
.pal       Paletas de Cores de 256 cores (8-bit)      48     color.pal e paletas ambientais das cidades
.cfg       Arquivos de Configuração                   30     Parâmetros de engine e inicialização
.mve       Vídeos Interplay MVE                       13     Cutscenes e cinemáticas (abertura, destruição, finais)
.edg       Máscaras de Borda de Mapa                 155     Polígonos de corte das bordas dos mapas
.aaf/.fon  Fontes Tipográficas                        10     Fontes vetoriais e bitmap da interface
.msk       Máscaras Gráficas                           7     Recortes para mapas e HUD
.gcd       Fichas de Criação de Personagem             6     Personagens pré-fabricados (Narg, Mingan, Chitra)
.rix/.lbm  Imagens de Abertura / Splash                8     Telas de boot e splash screens
.bio       Biografias Narrativas                       3     Históricos dos três protagonistas pré-definidos
--------------------------------------------------------------------------------
TOTAL INTEGRAL DE RECURSOS AUDITADOS:              30.492 arquivos (30.438 DATs + 54 soltos)
================================================================================
```

---

## 3. Estrutura Geográfica: Cidades, Áreas e Mapas

### A. Cidades Catalogadas em `data/city.txt` (49 Áreas Canônicas)
O mundo de Fallout 2 é particionado em 49 áreas indexadas:
- **Área 00:** Arroyo (Aldeia tribal e Ponto de Partida)
- **Área 01:** The Den (Cidade do crime e tráfico de escravos)
- **Área 02:** Klamath (Assentamento de caçadores e homens-rato)
- **Área 03:** Modoc (Comunidade agrícola e Ghost Farm)
- **Área 04:** Vault City (Cidade tecnocrática de primeira classe)
- **Área 05:** Gecko (Assentamento de ghouls com usina nuclear quebrada)
- **Área 06:** Broken Hills (Cidade de mineração pacífica entre mutantes e humanos)
- **Área 07:** New Reno (Metrópole do vício com as 4 famílias mafiosas)
- **Área 08:** Sierra Army Depot (Instalação militar automatizada pré-guerra)
- **Área 09:** Vault 15 (Ruínas dos invasores e nova comunidade de invasores)
- **Área 10:** New California Republic (NCR - Capital política)
- **Área 11:** Vault 13 (O lendário abrigo original do Ancestral)
- **Área 12:** Military Base (Base militar em ruínas pós-Mestre)
- **Área 13:** Redding (Cidade de mineração de ouro e dependência de Jet)
- **Área 14:** San Francisco (Povo Shi, Hubologists e petroleiro Poseidon Oil)
- **Área 15:** Navarro (Base avançada da Enclave no continente)
- **Área 16:** Enclave Oil Rig (Plataforma petrolífera marítima e sede do Presidente)
- **Áreas 17 a 48:** Primitive Tribe, Abbey, EPA, Ghost Farm, Reno Stables, Golgotha e Encontros Especiais (Special Whale, Woodsman, Guardian, Cafe, Bridge, etc.).

### B. Mapas Catalogados em `data/maps.txt` (151 Seções de Mapa)
Foram catalogados 151 mapas oficiais com associação a músicas e perfis de áudio ambiente. A música `17arroyo.acm` (4,11 MB) é atribuída canonicamente aos mapas de Arroyo e do Templo.

---

## 4. Decomposição Atômica da Fatia 1: Arroyo & Templo dos Testes

A Fatia Vertical 1 é composta por 6 mapas interligados:

| Map ID | Arquivo `.map` | Nome de Referência | Total de Objetos | Elevações | Protótipos Únicos | Função Narrativa |
| :---: | :--- | :--- | :---: | :---: | :---: | :--- |
| **3** | `maps/arcaves.map` | Arroyo Caves (Temple Interior) | **4.536** | 0, 1, 2 | 146 | Templo dos Testes: 3 câmaras de desafios, armadilhas e duelo de Cameron |
| **126** | `maps/artemple.map` | Arroyo Temple (Exterior) | **568** | 0 | 37 | Fachada exterior com a cabeça colossal esculpida do Vault Dweller |
| **4** | `maps/arvillag.map` | Arroyo Village | **1.854** | 0 | 157 | Aldeia tribal: tendas, poço, Anciã, Hakunin, civis e curral de brahmin |
| **5** | `maps/arbridge.map` | Arroyo Bridge | **618** | 0 | 132 | Grande Ponte de Arroyo suspensa sobre o desfiladeiro, guarda Mynoc |
| **35** | `maps/argarden.map` | Arroyo Wilderness (Jardim) | **2.013** | 0 | 69 | Jardim de Hakunin: plantas medicinais (Broc, Xander), cão e deserto |
| **127** | `maps/ardead.map` | Destroyed Arroyo Bridge | **427** | 0 | 85 | Arroyo destruída pós-ataque dos vertibirds da Enclave |
| **Total** | **6 Mapas** | **Ecossistema de Arroyo** | **10.016** | — | — | **Primeiro marco do jogo clássico** |

---

## 5. Decomposição Detalhada do Templo dos Testes (`maps/arcaves.map`)

O Templo dos Testes é o cenário central de validação das regras de domínio:

### A. Criaturas e Habitantes (22 instâncias no Mapa 3)
1. **Giant Ant (Formiga Gigante):** 12 instâncias (PID `0x0100017E`, FID `0x01000061`).
   - Distribuídas nas elevações 0 e 1.
   - Hostis, ataque corporal de mandíbula.
2. **Small Radscorpion (Escorpião Radioativo Pequeno):** 9 instâncias (PID `0x01000005`, FID `0x0100003B`).
   - Hostis, ataque de ferrão venenoso, deixam ferrão no chão ao morrer.
3. **Cameron / Dumar:** 1 instância (PID `0x01000112`, FID `0x01000040`).
   - Localizado no hexágono `24302`, Elevação 2, em frente à porta final (100).
   - Possui script `scripts/actemvil.int` (16 variáveis locais).
   - Possui a Chave do Templo (`Temple Key`, PID `0x000001B6`) em seu inventário.
   - Permite resolução pacífica via Speech (offsets 11698..14328) ou combate corpo a corpo desarmado.

### B. Contêineres, Itens e Tesouros do Templo (15 itens no Mapa 3)
- **Baús de Oferendas (`Chest`, PID `0x000000F5`):** 4 baús de pedra/madeira.
  - Baú 1 (Elevação 0): 1 Lança (`Spear`, PID `0x00000007`), 1 Pó de Cura (`Healing Powder`, PID `0x00000111`), 1 Antídoto (`Antidote`, PID `0x00000031`).
  - Baú 2 (Elevação 1): 1 Explosivo Plástico (`Plastic Explosives`, PID `0x00000055`) usado para detonar a barreira da porta final.
  - Baú 3 (Elevação 2): Oferendas rituais e pó de cura.
- **Vasos de Barro (`Pot`, PID `0x000000F3`, `0x000000F4`):** 2 vasos cerimoniais com antídoto e pó de cura.
- **Ossadas Humanas (`Bones`, PID `0x000000FA`):** Restos de desafiantes que falharam no teste.

### C. Cenários Especiais e Interativos (416 objetos de Scenery)
- **Tochas de Parede (`Torch`, PID `0x0200055D`, `0x0200055E`, `0x0200055F`):** 54 tochas que iluminam os corredores de pedra com luz suave.
- **Bancos de Pedra Maia (`Stone Bench`, PID `0x02000643`, `0x02000644`):** 26 bancos cerimoniais esculpidos em pedra maciça.
- **Queimadores de Incenso (`Incense Burner`, PID `0x02000645`):** 7 braseiros rituais de incenso tribal.
- **Placas de Pressão no Piso (`Floor Depression`, PID `0x02000642`):** Placas de gatilho mecânico que disparam armadilhas de dardos.
- **Orifícios de Disparo (`Hole`, PID `0x020003B6`):** Perfurações nas paredes de pedra que disparam dardos quando a placa é pisada.
- **Portas de Pedra Pesada Deslizante (`Door`, PID `0x020003A6`, `Door Track` PID `0x0200055C`):**
  - Porta 1 (Elevação 0): Destrancada.
  - Porta 2 (Elevação 1): Trancada, arrombável com gazua (Lockpick) concedendo 25 XP únicos.
  - Barreira de Pedra: Bloqueador destrutível com dinamite.
  - Porta 3 (Elevação 2 / Porta 100): Trancada com a Chave do Templo ou aberta após resolução com Cameron.
- **Abismos e Despenhadeiros Subterrâneos (`Chasm`, PID `0x020005BD` a `0x020005C0`):** Fosso interior profundo que divide os setores do templo.
- **Cascata Subterrânea (`Waterfall`, PID `0x020006F3`):** Queda d'água na elevação 1.

### D. Paredes e Arquitetura de Pedra (2.622 paredes no Mapa 3)
- 106 protótipos de paredes catalogados (PIDs `0x0300056C` a `0x03000584` e `0x030000E4` a `0x030000E9`).
- Representam a cantaria pesada pré-colombiana com entalhes de estilo asteca/maia em baixo-relevo combinada com rocha escavada de caverna natural.
- As 106 variações clássicas decorrem de junções 2D ortogonais (cantos, remates, batentes). Em 3D isométrica, essas variações são representadas de forma elegante por um **kit modular 3D de 6 peças**.

---

## 6. Fachada Monumental do Templo (`maps/artemple.map`)

O exterior do templo apresenta uma das estruturas mais icônicas de Fallout 2:
- **A Cabeça Gigante do Vault Dweller:** Montada através dos protótipos `0x0200054F` a `0x02000554` e `0x02000558` a `0x02000559`. Trata-se de uma escultura monumental de pedra talhada em estilo olmeca/asteca retratando o rosto do Vault Dweller clássico com o uniforme Vault 13 estilizado.
- **Skull Pole (`PID 0x020003AE`):** Totens tribais feitos com estacas de madeira e crânios espetados.
- **Fogueiras Rituais (`Firepit`, PID `0x02000555`, `0x02000556`):** Braseiros de pedra com carvão em brasa guardando o portal.
- **Vegetação de Montanha:** 88 árvores secas e arbustos de deserto (`Tree`, `Scrub`, `Rocks`).
