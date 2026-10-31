# Diretrizes para Agentes de IA — FalloutRemake

## 1. Validação durante o uso simultâneo do computador (Regra Crítica)

Por solicitação do usuário em 18/10/2026, o Godot não deve interferir no uso do
computador enquanto ele realiza outros testes. Esta preferência vale até o
usuário alterá-la.

- Use `--headless` para validação sem imagem.
- Para capturas, use `tools/Run-QuietGodot.ps1`: ele inicia somente a instância de
  teste em um desktop isolado, sem trocar o desktop de entrada.
- Não abra o editor ou o jogo no desktop ativo. `Start-Process -WindowStyle Hidden`
  sozinho esconde o console, mas não garante isolar a janela criada pelo Godot.
- Se o runner isolado falhar, continue as verificações sem janela e registre a
  limitação; não abra automaticamente uma janela normal como fallback.
- Não altere, mova, minimize, encerre ou envie entrada às instâncias do usuário.
  Uma abertura em monitor específico pode ser feita se o usuário a solicitar.

## 2. Documentação Operacional e Consulta Proporcional

Antes de iniciar ou intervir em uma tarefa, consulte as instruções gerais e o contexto relevante **de forma proporcional à complexidade da tarefa**, evitando releituras indiscriminadas:

- **Inspeção do Git:** Inspecione o estado do Git (`git status --short`) antes de editar, distinguindo alterações preexistentes das suas próprias modificações.
- **Consulta Seletiva de Documentação:**
  - `docs/PROJECT_STATUS.md`: Fonte de verdade do estado atual dos sistemas. Consulte para verificar o marco oficial ativo e as dependências ao iniciar novas etapas.
  - `docs/ARCHITECTURE.md`: Modelo mental do projeto e contratos de dados. Consulte pontualmente para mapear classes, responsabilidades e fluxos centrais ao modificar subsistemas.
  - `docs/WORKFLOW.md`: Guia de comandos rápidos. Consulte para comandos de compilação, validações operacionais e rotinas de teste.
  - **Decisões Históricas (AI Memory):** Consulte (`memory_query`) somente quando relevante para entender a justificativa de uma escolha, uma restrição ou um problema recorrente.
- **Validação Proporcional:** Execute testes pertinentes aos componentes afetados e amplie a validação quando a mudança tiver impacto compartilhado ou risco significativo. Não presuma que um teste sintético comprova o funcionamento integrado do jogo ou a validação de dados originais.

## 3. Protocolo de Verificação de Código C# (.NET)

Os subsistemas de domínio (`src/Import`, `src/Geometry`, `src/Runtime`) são C# puro (.NET 8) desacoplados do Godot. O projeto de console `tools/TempleInspector` referencia esses módulos diretamente e executa uma suíte completa de testes sintéticos em ~1,4 segundo.

**Regra obrigatória:**
- Execute a suíte de testes sintéticos **antes** e **depois** de qualquer alteração relevante em C#:
  ```powershell
  dotnet run --project "tools/TempleInspector/TempleInspector.csproj" -- --self-test
  ```
- Respeite o escopo e as dependências: alterações em `src/Import`, `src/Geometry` ou `src/Runtime` não devem quebrar nenhum dos 335+ grupos de teste existentes.
- Para alterações na camada de apresentação Godot (`src/Preview`), execute a validação headless correspondente (ex.: `tools/Validate-Temple.ps1` ou flags `--preview-test` / `--nature-test` via Godot `--headless`), mantendo a regra de desktop isolado.

## 4. Rigor Técnico, Evidências e Classificação de Funcionalidades

- **Exigência de evidências:** Qualquer afirmação sobre o comportamento ou existência de uma funcionalidade deve ser fundamentada com caminhos de arquivos, símbolos de classes/métodos, linhas de código ou resultados verificados de testes.
- **Proibição de falsos positivos de gameplay:** É estritamente proibido classificar protótipos visuais, ensaios desacoplados (`*Preview`, `*Consultation`, `*Draft`, `*StateProfile`) ou testes de auditoria como "gameplay integrado" ou "funcionalidade pronta no jogo".
- Diferencie rigorosamente:
  1. *Fato verificado:* Comprovado por código ativo e testes aprovados.
  2. *Inferência técnica:* Deduzido a partir de dados originais ou código do Community Engine, mas ainda não integrado.
  3. *Informação desconhecida:* Comportamento não verificado nos dados ou no runtime.

## 5. Integridade de Dados, Assets e Sistemas Consolidados

- **Proibição de alterações não solicitadas:** Não modifique IDs originais do Fallout 2 (PIDs, FIDs, SIDs, IDs de mapas), offsets binários de scripts INT, tabelas de texto MSG ou arquivos GAM/PRO originais.
- **Arquivos originais e de configuração:** Nunca altere ou substitua os arquivos da instalação clássica de Fallout 2 (`master.dat`, `patch000.dat`, `data/*`).
- **Assets 3D:** Não altere, remova ou regenere arquivos `.glb` ou `.import` na pasta `Assets/` sem instrução explícita.
- **Preservação de sistemas consolidados:** Não refatore nem altere contratos de sistemas já homologados (como versões de checkpoint v1–v9, cálculo atômico de peso, roteamento de itens ou protocolos de porta) a menos que a tarefa solicite explicitamente essa mudança.

## 6. Intercambialidade e Responsabilidades dos Agentes

As atribuições entre agentes (Codex, Claude, Gemini Argon) são **especializações preferenciais**, nunca fixas ou exclusivas:
- **Codex:** Preferencial para gameplay, regras de domínio, integração de sistemas e testes C#. Claude também pode executar essas tarefas.
- **Claude:** Preferencial para análise visual, proporções estéticas, reconstrução artística e tarefas de arquitetura. Codex também pode executá-las.
- **Gemini Argon:** Preferencial para automação e conversão de assets 3D (`FalloutAssetPipeline`), sem exclusividade.
- **Substituição e Continuidade:** O usuário pode alternar agentes conforme disponibilidade, limites de modelo ou cotas. O agente substituto deve obrigatoriamente inspecionar o estado do Git, as decisões relevantes, a documentação canônica (`docs/PROJECT_STATUS.md`, `docs/ARCHITECTURE.md`) e os testes pertinentes antes de intervir.
- **Espírito Crítico:** Nenhum agente deve assumir que o trabalho anterior está correto apenas porque foi gerado por outro modelo; valide sempre no código e nos testes.

