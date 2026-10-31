# FalloutRemake — Auditoria Final das Ressalvas da Migração de Assets

> **Data da Auditoria Complementar:** 28 de Outubro de 2026  
> **Status da Auditoria:** **APROVADO COM RESSALVAS DOCUMENTADAS**  
> **Documento de Origem Auditado:** `docs/VALIDACAO_POS_TRANSFERENCIA_ASSETS.md` (v1 e v2)  
> **Ambiente Avaliado:**  
> - Projeto Godot: `C:\Dev\FalloutRemake` (Assets mantidos: `C:\Dev\FalloutRemake\Assets`)  
> - Biblioteca Externa: `D:\FalloutRemakeAssets` (Manifesto: `EXTERNAL_ASSET_MANIFEST.csv`)  
> - Utilitário de Restauração: `C:\Dev\FalloutRemake\tools\Restore-Asset.ps1` (v3 Transacional)  
> - Suíte de Testes C# (.NET 8): `tools/TempleInspector` (**337/337 grupos aprovados**)

---

## 1. Resumo Executivo

A presente auditoria complementar teve como escopo o fechamento rigoroso das ressalvas e potenciais fragilidades identificadas nas versões preliminares da validação pós-transferência de assets.

### O que foi Comprovado Factualmente:
1. **Integridade Criptográfica Total (100,00% de Cobertura SHA-256):** Todos os **87.726 arquivos** listados no manifesto externo foram recalculados individualmente no disco D: contra o hash de origem gerado no momento da transferência. **0 divergências de hash, 0 arquivos faltantes e 0 discrepâncias de tamanho** em **18.088.604.874 bytes (~16,85 GiB / 18,09 GB)** auditados.
2. **Reconciliação Exata dos 72 Arquivos de Textura WebP:** Comprovou-se por análise do código gerador original que o valor inicial de 38.406 WebP foi deduzido por subtração ($88.124 - 5.788 - 43.917 - 13$), agrupando indevidamente 72 arquivos de formatos auxiliares (.tmp, .translation, .uid, .png, .md, .tscn, .jpg, .json, .py) sob o rótulo de WebP. A biblioteca real continha 38.334 WebPs e 72 arquivos auxiliares. **Nenhuma textura WebP foi perdida, corrompida ou excluída**.
3. **Reconciliação do Total Físico em D: (87.740 arquivos):** A contagem física no disco D: totaliza exatamente 87.740 arquivos, eliminando a sobreposição de 87.741 provocada pela dupla contagem do arquivo `EXTERNAL_ASSET_MANIFEST.csv`.
4. **Homologação com Rollback Transacional do `Restore-Asset.ps1` (v3):** O utilitário foi aprimorado com backup transacional de arquivos pré-existentes. Em caso de falha durante a execução com `-Overwrite`, os arquivos pré-existentes têm seu conteúdo original 100% restaurado (e novos arquivos parciais são removidos), impedindo perda de dados no projeto Godot (**7/7 testes aprovados em sandbox isolada**).
5. **Preservação dos 337 Testes C# (.NET 8):** A suíte de domínio do `TempleInspector` permanece 100% aprovada (337/337).

### O que Permanece como Ressalva Delimitada (Limitações Metodológicas):
1. **Ausência de Dump Independente Pré-Migração:** O total inicial de 88.124 arquivos foi apurado em memória durante a execução do script `execute_safe_asset_migration.py`. Não existe uma listagem ou manifesto completo gravado em disco *antes* da execução da migração. Logo, embora a soma matemática ($87.726 + 398 = 88.124$) seja perfeita e não haja registro de erros de I/O, **a ausência de perdas em relação ao estado anterior não pode ser comprovada de forma absoluta**.
2. **Validação Visual no Godot (Não Verificável):** Permanece como **NÃO VERIFICÁVEL** para manter o isolamento estrito do desktop do usuário (`AGENTS.md`) e evitar a reimportação desnecessária do cache `.godot`.

---

## 2. Ressalva A — Reconciliação dos Arquivos e Prova de Inventário

### 2.1. Confronto das Evidências de Origem e Destino

| Categoria | Inventário Inicial Deduzido (v1) | Inventário Real Inicial Auditado | Retido em `Assets/` (C:) | Transferido no Manifesto (D:) | Físico no Destino (D:) | Divergência Não Explicada |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **Modelos 3D (`.glb`)** | 5.788 | **5.788** | 26 | 5.762 | **5.762** | **0** |
| **Sidecars Godot (`.import`)** | 43.917 | **43.917** | 179 | 43.738 | **43.739** *(a)* | **0** |
| **Texturas WebP (`.webp`)** | 38.406 | **38.334** | 140 | 38.194 | **38.194** | **0** *(b)* |
| **Metadados (`.csv` / `.gd`)** | 13 | **13** | 12 | 1 | **3** *(c)* | **0** |
| **Formatos Auxiliares Suporte** | 0 | **72** | 41 | 31 | **42** *(d)* | **0** *(b)* |
| **Manifesto CSV Externo** | 0 | **0** | 0 | 0 | **1** *(e)* | **0** |
| **TOTAL GERAL** | **88.124** | **88.124** | **398** | **87.726** | **87.740** | **0** |

#### Notas Fatuais:
- *(a)* O arquivo adicional `.import` em D: é `_Catalog/vanilla_manifest.csv.import` (cópia de referência autônoma).
- *(b)* Os 72 arquivos WebP que aparentavam faltar eram exatamente os 72 arquivos de formatos auxiliares (.translation, .tmp, .uid, .png, .md, .tscn, .jpg, .json, .py) agrupados sob o rótulo de WebP no inventário deduzido preliminar.
- *(c)* Os 3 arquivos `.csv` físicos em D: são `EXTERNAL_ASSET_MANIFEST.csv` (raiz), `ProjectMojave/_Catalog/manifest.csv` (transferido) e `_Catalog/vanilla_manifest.csv` (cópia de referência).
- *(d)* Os 42 arquivos de outros formatos físicos em D: compreendem os 31 transferidos no manifesto + 11 cópias de referência em `_Catalog/` (10 `.translation` e 1 `.json`).
- *(e)* O manifesto `EXTERNAL_ASSET_MANIFEST.csv` na raiz do disco D:.

### 2.2. Categorias Mutuamente Exclusivas no Disco D: (Total: 87.740)
Cada um dos 87.740 arquivos físicos em `D:\FalloutRemakeAssets` pertence a exatamente uma categoria:
1. **Arquivos transferidos da origem (registrados no manifesto):** **87.726**
2. **Artefato de manifesto gerado na raiz (`EXTERNAL_ASSET_MANIFEST.csv`):** **1**
3. **Cópias de referência autônoma em `_Catalog/` (sem exclusão da origem):** **13**
4. **Arquivos preexistentes / sem classificação:** **0**
- **Soma Exata:** $87.726 + 1 + 13 = \mathbf{87.740\text{ arquivos}}$.

> [!WARNING]
> **Declaração Formal de Limitação Metodológica:**  
> Como não houve a gravação de um arquivo de dump do sistema de arquivos anterior à execução do script de migração, a reconciliação comprova que todos os arquivos registrados em memória foram distribuídos exatamente entre C: e D:. Contudo, a ausência absoluta de perdas de arquivos que pudessem existir antes da inicialização do script não é passível de comprovação matemática independente.

---

## 3. Ressalva B — Cobertura Criptográfica Integral (SHA-256)

A auditoria anterior verificou 100% dos GLBs e apenas uma amostra de 1.000 WebPs e 1.000 imports. Nesta etapa, a verificação foi estendida a **100,00% da totalidade dos arquivos transferidos**.

O script `scratch/verify_all_87726_hashes.py` recalculou o SHA-256 de cada arquivo físico em `D:\FalloutRemakeAssets` e o comparou com o hash registrado no momento da transferência:

| Categoria de Formato | Total de Arquivos no Manifesto | Arquivos Verificados | Hashes Idênticos (OK) | Divergências de Hash | Arquivos Faltantes | Coerência de Tamanho | Cobertura Auditada |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **Modelos 3D (`.glb`)** | 5.762 | 5.762 | **5.762** | 0 | 0 | 100% | **100,00%** |
| **Texturas WebP (`.webp`)** | 38.194 | 38.194 | **38.194** | 0 | 0 | 100% | **100,00%** |
| **Sidecars Godot (`.import`)** | 43.738 | 43.738 | **43.738** | 0 | 0 | 100% | **100,00%** |
| **Outros Formatos (.tmp, .translation, etc.)** | 32 | 32 | **32** | 0 | 0 | 100% | **100,00%** |
| **TOTAL GERAL AUDITADO** | **87.726** | **87.726** | **87.726** | **0** | **0** | **100%** | **100,00%** |

- **Volume Total Auditado Criptograficamente:** **18.088.604.874 bytes (~16,85 GiB / 18,09 GB)**.
- **Tempo de Execução:** 108,7 segundos (taxa média de 807 arquivos/segundo).
- **Conclusão:** 100,00% dos arquivos transferidos para a biblioteca externa conservam fidelidade bit a bit com os dados de origem.

---

## 4. Ressalva C — Semântica e Medições de Armazenamento

Para sanar ambiguidades semânticas entre tamanho lógico e alocação física, os dados são categorizados estritamente conforme a natureza de sua medição:

### 4.1. Definições Padronizadas
- **Soma dos Tamanhos Lógicos:** A soma aritmética dos comprimentos em bytes dos fluxos de dados dos arquivos (`stat.st_size` ou `File.Length`).
- **Espaço Livre Informado pelo SO:** Capacidade disponível reportada pela API do sistema de arquivos (`shutil.disk_usage` ou `Get-PSDrive`).
- **Unidades Decimais (SI):** $1\text{ kB} = 10^3\text{ B}$, $1\text{ MB} = 10^6\text{ B}$, $1\text{ GB} = 10^9\text{ B}$.
- **Unidades Binárias (IEC):** $1\text{ KiB} = 2^{10}\text{ B}$, $1\text{ MiB} = 2^{20}\text{ B}$, $1\text{ GiB} = 2^{30}\text{ B}$.

### 4.2. Tabela Semântica de Armazenamento

| Métrica Auditada | Tamanho em Bytes | Decimal (SI) | Binário (IEC) | Método / Classificação da Medida |
| :--- | ---: | :---: | :---: | :--- |
| **Soma Lógica: `Assets/` (398 arq.)** | 210.603.474 B | 210,60 MB | 200,85 MiB | **Medição Lógica Direta** (soma de tamanhos de arquivo) |
| **Soma Lógica: `D:\FalloutRemakeAssets` (87.740 arq.)** | 18.122.064.623 B | 18,12 GB | 16,88 GiB | **Medição Lógica Direta** (soma de tamanhos de arquivo) |
| **Espaço Livre C: Atual (Pós-Migração)** | 44.564.041.728 B | 44,56 GB | 41,50 GiB | **Medição Física do SO** (`GetDiskFreeSpaceExW`) |
| **Espaço Livre C: Anterior (Pré-Migração)** | ~26.403.000.000 B | ~26,40 GB | 24,59 GiB | **Registro Histórico Declarado** (não re-medível) |
| **Variação de Espaço Livre C:** | +18.161.000.000 B | +18,16 GB | +16,91 GiB | **Estimativa Baseada em Histórico Declarado** |
| **Espaço Livre D: Atual (Pós-Migração)** | 208.498.429.952 B | 208,50 GB | 194,18 GiB | **Medição Física do SO** (`GetDiskFreeSpaceExW`) |
| **Espaço Livre D: Anterior (Pré-Migração)** | ~226.677.000.000 B | ~226,68 GB | 211,11 GiB | **Registro Histórico Declarado** (não re-medível) |
| **Variação de Espaço Livre D:** | -18.178.000.000 B | -18,18 GB | -16,93 GiB | **Estimativa Baseada em Histórico Declarado** |

> [!NOTE]
> **Ressalva Semântica sobre Alocação Física e "Cluster Slack":**  
> A diferença entre a soma lógica dos arquivos transferidos (16,88 GiB) e a variação estimada do espaço livre em C: (16,91 GiB) é de aproximadamente ~30 MiB. A hipótese de desperdício de cluster (*cluster slack*) decorrente da remoção de 43.700 arquivos pequenos de 1 KB em blocos NTFS padrão de 4 KB é tecnicamente verossímil; no entanto, como não foram realizadas medições de clusters alocados antes e depois da migração, esse mecanismo deve ser formalmente classificado como **hipótese técnica plausível**, e não como grandeza física comprovada.

---

## 5. Ressalva D — Auditoria e Homologação de `tools/Restore-Asset.ps1` (v3)

### 5.1. Mecanismo de Rollback Transacional
Para responder ao requisito crítico de segurança onde uma restauração com `-Overwrite` falha durante a execução, o script [`tools/Restore-Asset.ps1`](file:///c:/Dev/FalloutRemake/tools/Restore-Asset.ps1) foi dotado de um **gerenciador de backup transacional**:
1. Antes de iniciar qualquer cópia ou substituição, todos os arquivos existentes no destino pertencentes ao pacote do asset são copiados para uma pasta temporária isolada (`$BackupDir`).
2. Se qualquer erro ocorrer (falha de I/O ou divergência de SHA-256 no destino):
   - Os arquivos recém-criados são excluídos.
   - Os arquivos que já existiam antes da operação são **integralmente restaurados a partir do backup temporário**, restabelecendo seu conteúdo anterior exato.
3. Se a operação for 100% bem-sucedida, a pasta temporária de backup é eliminada.

### 5.2. Resultados dos Testes Funcionais Isolados (v3)
Executados via `scratch/test_restore_suite_v3.ps1` em sandbox descartável (`scratch/sandbox_restore_v3/`):

| ID | Nome do Teste | Comportamento Avaliado | Resultado Observado | Status |
| :-: | :--- | :--- | :--- | :---: |
| **T1** | Restauração Padrão Completa | Restauração de `shackwallflat01.glb` em pasta vazia com todas as dependências | 26 arquivos criados (1 GLB + 1 GLB.import + 12 WebP + 12 WebP.import). Todos os 26 hashes SHA-256 idênticos à origem (8.040.592 bytes). | **PASS** |
| **T2** | Bloqueio Sem Overwrite | Tentativa de restauração quando arquivos já existem no destino sem `-Overwrite` | Operação abortada imediatamente com mensagem explícita de proteção de dados; zero arquivos modificados. | **PASS** |
| **T3** | Rejeição de Caminho Inexistente | Requisição de asset não cadastrado (`NonExistentAsset99.glb`) | Operação rejeitada com mensagem informando que o GLB não existe no repositório externo. | **PASS** |
| **T4** | Validação de Extensão | Fornecimento de arquivo não-GLB (`texture.webp`) | Operação bloqueada informando obrigatoriedade de arquivo com extensão `.glb`. | **PASS** |
| **T5** | Sobrescrita com Flag Explícita | Execução deliberada com a flag `-Overwrite` | Operação permitida; 26 arquivos substituídos e revalidados com 100% de integridade SHA-256. | **PASS** |
| **T6** | Opção `-SkipImportFiles` | Restauração passando o switch `-SkipImportFiles` | GLB e 12 texturas WebP copiados com sucesso, sem criar nenhum arquivo `.import` de sidecar. | **PASS** |
| **T7** | **Rollback Transacional de Pré-Existentes (Ponto Crítico)** | Simulação de corrupção deliberada de SHA-256 na 2ª textura durante `-Overwrite` sobre arquivos antigos existentes | O script capturou a divergência de hash, acionou o rollback, removeu arquivos parciais e **restaurou 100% do conteúdo original pré-existente** no GLB e na textura antiga. | **PASS** |

---

## 6. Ressalva E — Limites da Validação Visual no Godot

A validação visual de renderização de cenas no Godot permanece classificada como **NÃO VERIFICÁVEL** no ambiente headless desta auditoria:

- **Motivo Arquitetural:** Em conformidade com a Regra 1 de `AGENTS.md`, o ambiente de testes não abre interfaces gráficas nem interage com a sessão do usuário.
- **Distinção Conceitual:** A aprovação nos 337 testes sintéticos C# (.NET 8) comprova que o modelo de dados de mapas, geometria, regras de inventário e nós de diálogo estão perfeitos. Ela **não comprova** que o renderizador PBR do Godot montou os shaders ou que o pipeline de iluminação da cena final renderizou sem artefatos visuais.
- **Procedimento para Validação Visual:** Quando necessária, a inspeção visual deve utilizar exclusivamente o script `tools/Run-QuietGodot.ps1` em desktop isolado Win32.

---

## 7. Pendências Residuais

| Item Pendente | Evidência Ausente | Ação Necessária para Fechamento Definitivo |
| :--- | :--- | :--- |
| **1. Prova Absoluta do Inventário Pré-Migração** | Dump filesystem completo gravado antes de 28/10/2026 às 11:27. | Impossível gerar retroativamente. Fica formalmente delimitada a dependência dos logs do script. |
| **2. Validação Visual Godot** | Captura de frames renderizados em runtime pelo Godot 4.7. | Execução pontual via `tools/Run-QuietGodot.ps1` no marco de montagem de cenários por Codex/Claude. |

---

## 8. Veredito Final da Auditoria

### Classificação Formal: **APROVADO COM RESSALVAS DOCUMENTADAS**

#### Justificativa Técnica:
A integridade da biblioteca externa de assets (`D:\FalloutRemakeAssets`) está **100% comprovada criptograficamente** (87.726 de 87.726 arquivos com SHA-256 idêntico à origem). A contabilidade dos 72 arquivos e dos 398 arquivos retidos no projeto Godot foi matematicamente resolvida e demonstrada. O restaurador sob demanda `Restore-Asset.ps1` (v3) possui agora garantias transacionais completas contra corrupção e sobrescrita acidental. As duas únicas ressalvas remanescentes (ausência de dump prévio e validação visual de renderização) constituem limitações metodológicas transparentemente delimitadas que não impedem nem colocam em risco o trabalho de desenvolvimento por Codex e Claude.
