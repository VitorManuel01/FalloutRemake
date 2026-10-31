# Inventário de Dependências do Godot e Justificativa de Retenção

> **Data:** 28/10/2026  
> **Escopo:** `C:\Dev\FalloutRemake\Assets`  
> **Status:** Concluído e Reconciliado  
> **Total de Arquivos na Origem:** 88.124 arquivos (17,04 GB)  
> **Arquivos Retidos no Projeto Godot:** **398 arquivos**  
> **Arquivos Transferidos para o Repositório Externo:** **87.726 arquivos**

---

## 1. Princípio Fundamental de Retenção

Para manter o projeto Godot enxuto, ágil e livre de sobrecarga na indexação de recursos, somente foram retidos na pasta `Assets/` os arquivos que possuem **uso confirmado pelo código ativo** ou que integram os **ambientes de teste sintético e ensaios de cena** do projeto.

Todos os demais recursos, que compõem a biblioteca de produção para marcos futuros de Fallout 2 ou que representam assets incompatíveis de lore, foram transferidos para o repositório externo dedicado `D:\FalloutRemakeAssets`.

---

## 2. Categorias e Relação dos 398 Arquivos Retidos

### A. Modelos GLB de Produção em Uso Ativo (13 GLBs)
Estes modelos são instanciados e mapeados no runtime C# pelas classes [`src/Preview/NatureModels.cs`](file:///c:/Dev/FalloutRemake/src/Preview/NatureModels.cs) e [`src/Geometry/NaturePresentation.cs`](file:///c:/Dev/FalloutRemake/src/Geometry/NaturePresentation.cs):

| Modelo GLB | Caminho Relativo em Assets/ | Código que Referencia | Função no Jogo |
| :--- | :--- | :--- | :--- |
| **RiverRock1.glb** | `Nature/RiverRock1.glb`<br/>`Nature/Rocks/riverrock1.glb` | `NaturePresentation.cs#L14`<br/>`NatureModels.cs#L23` | Substituição 3D de pedras de rio na aldeia de Arroyo (Map 4). |
| **RockS01.glb** | `Nature/RockS01.glb`<br/>`Nature/Rocks/rocks01.glb` | `NaturePresentation.cs#L15`<br/>`NatureModels.cs#L23` | Rochas graníticas de pequeno/médio porte em Arroyo e Templo. |
| **BlastedForestBoulder01.glb** | `Nature/BlastedForestBoulder01.glb`<br/>`Nature/Rocks/blastedforestboulder01.glb` | `NatureModels.cs#L23` | Matacões rochosos de deserto para montanhas e colinas. |
| **BlastedForestCliff01.glb** | `Nature/BlastedForestCliff01.glb`<br/>`Nature/Rocks/blastedforestcliff01.glb` | `NatureModels.cs#L23` | Paredes rochosas verticais e contrafortes do platô de Arroyo. |
| **BlastedForestBurntTreeStump01.glb** | `Nature/BlastedForestBurntTreeStump01.glb`<br/>`Nature/Trees/blastedforestburnttreestump01.glb` | `NaturePresentation.cs#L18`<br/>`NatureModels.cs#L23` | Árvores secas e tocos calcinados na entrada do Templo (Map 126). |
| **CliffCanyonL01.glb** | `ProjectMojave/Environment/Cliffs/CliffCanyonL01.glb` | `TempleEntranceComposition` | Penhascos do grande cânion da entrada de Arroyo. |
| **RockCanyonCaveEntrace01.glb** | `ProjectMojave/Environment/Cliffs/RockCanyonCaveEntrace01.glb` | `TempleEntranceComposition` | Formação rochosa da boca da caverna do Templo dos Testes. |
| **RockCanyon01.glb** | `ProjectMojave/Environment/Rocks/RockCanyon01.glb` | `TempleEntranceComposition` | Grandes blocos de rocha vermelha do cânion. |

---

### B. Modelos de Teste e Bancada Experimental em `Assets/Testes/` (13 GLBs)
Modelos utilizados exclusivamente por suítes de teste de integração, ensaios de câmera, testes de colisão e composição visual preliminar:

| Modelo GLB | Subpasta em Assets/ | Finalidade Técnica |
| :--- | :--- | :--- |
| **RockS01.glb** | `Testes/RockS01.glb` | Teste sintético de picking 3D e colisão por triângulos. |
| **ancient+temple+entrance+3d+model.glb** | `Testes/AI Generated/` | Ensaio volumétrico de portal de entrada do Templo. |
| **Meshy_AI_The_Stoneface_Citadel*.glb** | `Testes/` | Ensaio experimental de proporção para a Cabeça do Vault Dweller. |
| **CliffCanyonL01.glb** | `Testes/ProjectMojaveBatch01/` | Ensaio desacoplado de composição de desfiladeiro. |
| **CreosoteBushSM.glb** | `Testes/ProjectMojaveBatch01/` | Teste de folhagem e transparência de arbustos desérticos. |
| **JoshuaTreeANV.glb** | `Testes/ProjectMojaveBatch01/` | Teste de vegetação árida de grande porte. |
| **RockCanyon01.glb** | `Testes/ProjectMojaveBatch01/` | Teste de sombreamento PBR em rochas. |
| **RockCanyonCaveEntrace01.glb** | `Testes/ProjectMojaveBatch01/` | Ensaio de iluminação na embocadura da caverna. |
| **ladder.glb** | `Testes/FNVtoFO4toGLB/sandcrete/` | Validação de colisão de escadas e passagens verticais. |
| **scintstairsleft.glb** | `Testes/FNVtoFO4toGLB/sandcrete/` | Validação de escadarias modulares de adobe/sandcrete. |
| **SCWall1a.glb** | `Testes/FNVtoFO4toGLB/sandcrete/` | Teste de alinhamento modular de paredes de cantaria. |

---

### C. Texturas WebP e Arquivos `.import` Associados (367 arquivos)
- Para cada um dos 26 modelos GLB retidos, todas as suas texturas PBR associadas (Albedo `_d.webp`, Normal OpenGL `_n_normal_gl.webp`, ORM `_orm.webp`) e seus respectivos arquivos `.import` gerados pelo Godot permanecem no projeto.
- Isso assegura que nenhum modelo ativo apresente texturas faltantes, materiais corrompidos ou erros de importação na inicialização do motor.

---

### D. Catálogos e Metadados do Projeto em `Assets/_Catalog/` (5 arquivos)
- `Assets/_Catalog/vanilla_manifest.csv`: Manifesto canônico de 4.869 registros da conversão de Fallout 4. Mantido no projeto para rastreabilidade de procedência de cada modelo.
- Scripts auxiliares e documentação interna do catálogo.

---

## 3. Justificativa Técnica para a Transferência dos 87.726 Arquivos

1. **Ausência de Dependência Atual:** Nenhum script C#, GDScript, cena `.tscn` ou recurso serializado do projeto referencia qualquer um dos 87.726 arquivos transferidos.
2. **Desempenho do Editor e Engine Godot:** O carregamento e indexação de 88.000 arquivos gerava sobrecarga massiva no Godot 4.7, lentidão na verificação de UIDs e footprint excessivo de 17 GB. Com a retenção estrita de 398 arquivos, a inicialização e execução do Godot tornam-se praticamente instantâneas.
3. **Segurança e Rastreabilidade:** Todos os arquivos transferidos foram verificados bit a bit por hash SHA-256 e estão catalogados no manifesto `D:\FalloutRemakeAssets\EXTERNAL_ASSET_MANIFEST.csv`, permitindo que Codex e Claude recuperem qualquer recurso pontual conforme a necessidade de construção dos novos mapas.
