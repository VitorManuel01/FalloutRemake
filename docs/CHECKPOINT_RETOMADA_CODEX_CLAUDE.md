# Checkpoint de Retomada e Estado do Projeto (Codex & Claude)

> **Data:** 28/10/2026  
> **Marco:** Preparação Concluída para Construção de Cenários e Sistemas  
> **Status dos Testes C# (.NET 8):** **337/337 grupos de teste aprovados** em `TempleInspector`  
> **Estado do Repositório Godot (`Assets/`):** **398 arquivos retidos** (100% dos modelos ativos, testes e metadados)  
> **Estado do Repositório Externo (`D:\FalloutRemakeAssets`):** **87.726 arquivos transferidos** com validação SHA-256 e manifesto CSV  
> **Branch Ativa:** `codex/arroyo-elder-entry-audit`

---

## 1. O que foi Concluído nesta Etapa

1. **Auditoria Integral de Fallout 2 (`docs/AUDITORIA_INTEGRAL_FALLOUT2.md`):**
   - 30.438 recursos mapeados nos arquivos DATs (`master.dat`, `critter.dat`, `patch000.dat`, `f2_res.dat`).
   - 151 mapas e 49 cidades indexadas em `maps.txt` e `city.txt`.
   - 10.016 objetos mapeados nos 6 mapas da Fatia 1 (Arroyo e Templo dos Testes).
2. **Matriz de Referência Cruzada e Cobertura (`docs/MATRIZ_REFERENCIA_CRUZADA_ASSETS.md`):**
   - Cruzamento entre protótipos de FO2 e o acervo de 5.788 modelos GLB.
   - Mais de 90% dos elementos de natureza, rochas, árvores secas, contêineres e props já possuem cobertura completa.
   - 8 lacunas críticas (GAPs) isoladas e catalogadas.
3. **Higienização e Separação Segura de Assets:**
   - 87.726 arquivos não utilizados pelo conteúdo atual foram transferidos com validação de hash bit a bit para `D:\FalloutRemakeAssets`.
   - O projeto Godot foi reduzido de 17 GB para apenas ~50 MB na pasta `Assets/`, eliminando a sobrecarga de indexação do editor.
4. **Catálogo de Assets Exclusivos de Fallout 2 (`docs/CATALOGO_ASSETS_EXCLUSIVOS_FALLOUT2.md`):**
   - Documentação técnica e artística para Claude recriar no futuro: Cabeça monumental do Vault Dweller, portas de pedra deslizante, grande ponte de Arroyo, tendas, poço, Formiga Gigante, Radscorpion e NPCs tribais.
5. **Guia Operacional e Ferramentas para Codex & Claude (`docs/GUIA_CONSULTA_ASSETS_CODEX_CLAUDE.md`):**
   - Script automatizado de recuperação sob demanda: [`tools/Restore-Asset.ps1`](file:///c:/Dev/FalloutRemake/tools/Restore-Asset.ps1) (v2: proteção padrão contra sobrescrita com `-Overwrite`, cópia de sidecars `.import`, verificação pós-cópia de SHA-256 com rollback e suporte a sandboxes via `-DestinationAssetsPath`).
   - Manifesto pesquisável completo: `D:\FalloutRemakeAssets\EXTERNAL_ASSET_MANIFEST.csv`.
6. **Reconciliação e Validação Pós-Transferência (`docs/VALIDACAO_POS_TRANSFERENCIA_ASSETS.md`):**
   - Reconciliação dos 72 arquivos WebP matematicamente comprovada (eram 72 arquivos de outros formatos agrupados no inventário preliminar; zero perdas de texturas).
   - Reconciliação do total físico em D: (87.740 arquivos em categorias mutuamente exclusivas).
   - Confirmação de 100% de texturas presentes para todos os 26 GLBs mantidos.
7. **Registro de Exclusões de Lore (`docs/REGISTRO_EXCLUSOES_LORE_FALLOUT4.md`):**
   - 185 modelos da Costa Leste de Boston (Prydwen, monumentos cívicos, Fenway Park, metrô e Instituto) categorizados como Categoria F e bloqueados para uso em Fallout 2.

---

## 2. Mapa dos Entregáveis e Documentos de Consulta

| Entregável | Caminho do Arquivo Local | Função para Codex e Claude |
| :--- | :--- | :--- |
| **Relatório Geral da Auditoria** | [`docs/AUDITORIA_INTEGRAL_FALLOUT2.md`](file:///c:/Dev/FalloutRemake/docs/AUDITORIA_INTEGRAL_FALLOUT2.md) | Consultar contagens, formatos DAT, mapas e predecessão de dados. |
| **Matriz de Referência Cruzada** | [`docs/MATRIZ_REFERENCIA_CRUZADA_ASSETS.md`](file:///c:/Dev/FalloutRemake/docs/MATRIZ_REFERENCIA_CRUZADA_ASSETS.md) | Consultar PIDs, FIDs, correspondências de assets e GAPs. |
| **Catálogo de Exclusivos FO2** | [`docs/CATALOGO_ASSETS_EXCLUSIVOS_FALLOUT2.md`](file:///c:/Dev/FalloutRemake/docs/CATALOGO_ASSETS_EXCLUSIVOS_FALLOUT2.md) | Guia visual e dimensional para modelagem artística com Claude. |
| **Inventário de Dependências** | [`docs/INVENTARIO_DEPENDENCIAS_GODOT.md`](file:///c:/Dev/FalloutRemake/docs/INVENTARIO_DEPENDENCIAS_GODOT.md) | Justificativa dos 398 arquivos retidos no projeto Godot. |
| **Guia de Consulta de Assets** | [`docs/GUIA_CONSULTA_ASSETS_CODEX_CLAUDE.md`](file:///c:/Dev/FalloutRemake/docs/GUIA_CONSULTA_ASSETS_CODEX_CLAUDE.md) | Fluxo de recuperação de modelos sob demanda para novos mapas. |
| **Registro de Exclusões de Lore** | [`docs/REGISTRO_EXCLUSOES_LORE_FALLOUT4.md`](file:///c:/Dev/FalloutRemake/docs/REGISTRO_EXCLUSOES_LORE_FALLOUT4.md) | Relação dos 185 assets de Boston/Instituto proibidos para uso direto. |
| **Script de Recuperação** | [`tools/Restore-Asset.ps1`](file:///c:/Dev/FalloutRemake/tools/Restore-Asset.ps1) | Utilitário PowerShell para puxar assets de D: para C: automaticamente. |
| **Manifesto Externo (CSV)** | `D:\FalloutRemakeAssets\EXTERNAL_ASSET_MANIFEST.csv` | Índice completo de todos os arquivos do repositório externo. |
| **Relatório de Transferência** | [`docs/relatorio_transferencia_assets.json`](file:///c:/Dev/FalloutRemake/docs/relatorio_transferencia_assets.json) | Auditoria técnica de integridade de cópia e remoção da origem. |
| **Validação Pós-Transferência** | [`docs/VALIDACAO_POS_TRANSFERENCIA_ASSETS.md`](file:///c:/Dev/FalloutRemake/docs/VALIDACAO_POS_TRANSFERENCIA_ASSETS.md) | Relatório canônico formal de validação contábil, hashes SHA-256 e ressalvas técnicas. |
| **Auditoria Final de Ressalvas** | [`docs/AUDITORIA_FINAL_RESSALVAS_MIGRACAO_ASSETS.md`](file:///c:/Dev/FalloutRemake/docs/AUDITORIA_FINAL_RESSALVAS_MIGRACAO_ASSETS.md) | Fechamento rigoroso de ressalvas, 100% SHA-256 (87.726 arq.), rollback transacional no restaurador v3. |

---

## 3. Próximo Passo Exato para Retomada (Semana Seguinte)

Para os agentes que assumirem o desenvolvimento na próxima etapa:

1. **Passo 1 (Codex / Claude — Kit Modular de Paredes do Templo):**
   - Criar em C# ou cena Godot o kit modular de 6 peças de pedra de cantaria (usando os modelos de `RetainingWall` recuperáveis via `Restore-Asset.ps1`) para substituir os blocos provisórios da cena [`TemplePreview`](file:///c:/Dev/FalloutRemake/src/Preview/TemplePreview.cs).
2. **Passo 2 (Claude — Escultura da Cabeça do Templo):**
   - Iniciar a modelagem artística da Cabeça Monumental do Vault Dweller para a fachada de `artemple.map` seguindo as proporções e silhueta descritas em [`docs/CATALOGO_ASSETS_EXCLUSIVOS_FALLOUT2.md`](file:///c:/Dev/FalloutRemake/docs/CATALOGO_ASSETS_EXCLUSIVOS_FALLOUT2.md).
3. **Passo 3 (Codex / Claude — Rigging das Criaturas do Templo):**
   - Obter/riggar as malhas da Formiga Gigante (12 instâncias) e do Radscorpion (9 instâncias) para habilitar a renderização dos inimigos no Templo dos Testes.
4. **Passo 4 (Codex — Mapeamento de Cameron):**
   - Integrar a malha do guerreiro tribal para Cameron no hexágono 24302 da porta 100, sincronizado com o script de diálogo já ativo [`TempleSpeechDraft`](file:///c:/Dev/FalloutRemake/src/Runtime/TempleSpeechDraft.cs).
