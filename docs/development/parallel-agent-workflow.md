# FalloutRemake — Guia de Fluxo de Desenvolvimento Paralelo e Integração

> **Status:** Documento Operacional Canônico  
> **Data de Elaboração:** 28 de Outubro de 2026  
> **Base de Código Oficial:** `a66f31c` (`codex/arroyo-elder-entry-audit`)  
> **Servidor de Memória Compartilhada:** ai-memory MCP (`project: FalloutRemake`)

---

## 1. Visão Geral e Modelo Operacional

O desenvolvimento do **FalloutRemake** adota uma dinâmica colaborativa e desacoplada entre múltiplos agentes de IA (**Gemini Argon**, **Claude** e **Codex**). 

### Princípios Fundamentais:
1. **Intercambialidade Total (Papéis Dinâmicos):** Nenhum agente possui especialização fixa ou monopólio de função. As atribuições (Implementador, Integrador, Revisor Independente) são definidas a cada tarefa.
2. **Isolamento de Árvores de Trabalho (Git Worktrees):** Cada agente atua em um diretório isolado com branch própria, eliminando concorrência de escrita no mesmo sistema de arquivos.
3. **Memória de Longo Prazo Compartilhada (ai-memory MCP):** Todas as decisões, atribuições, contratos de dados e estados de features são centralizados no mesmo escopo `FalloutRemake`.
4. **Política Rígida de Commits:** Implementadores e revisores **nunca fazem commits**. A responsabilidade e autorização de criar um commit local é exclusiva do agente designado como **Integrador** daquela tarefa, e apenas após validação técnica completa e aprovação de revisão independente.
5. **Proibição Estrita de Push:** O projeto é estritamente local. Nenhuma publicação remota (`git push`, criação de remotes no GitHub) é permitida sem autorização expressa do usuário.

---

## 2. Estrutura de Diretórios e Worktrees

O repositório principal reside em `C:\Dev\FalloutRemake` e atua como a base estável de referência. As áreas de trabalho adicionais foram criadas como **Git Worktrees** nativas apontando para o mesmo repositório Git, sem duplicar a pasta `.git` ou a biblioteca de assets:

| Diretório Físico | Identificação / Propósito | Branch Vinculada | Commit Base de Criação |
| :--- | :--- | :--- | :---: |
| `C:\Dev\FalloutRemake` | **Projeto Principal (Base de Referência)** | `codex/arroyo-elder-entry-audit` | `a66f31c` *(com alterações locais em andamento)* |
| `D:\Dev\FalloutRemakeCodex` | **Área de Trabalho do Codex** | `agent/codex-work` | `a66f31c` |
| `C:\Dev\FalloutRemakeClaude` | **Área de Trabalho do Claude** | `agent/claude-work` | `a66f31c` |
| `D:\Dev\FalloutRemakeIntegracao` | **Área Compartilhada de Integração** | `integration/work` | `a66f31c` |

> [!IMPORTANT]
> **Sobre a Área de Integração (`D:\Dev\FalloutRemakeIntegracao`):**  
> Esta pasta é uma área de trabalho compartilhada para montagem e testes de integração. Ela **não** é de uso exclusivo do Gemini Argon. Qualquer agente (Argon, Claude ou Codex) designado como Integrador utilizará esta área durante sua operação. Durante uma integração ativa, apenas um agente opera nesta pasta para evitar colisões.

> [!NOTE]
> **Sobre o Estado das Worktrees vs. Alterações Locais de C::**  
> As worktrees foram criadas a partir do commit `a66f31c`. Arquivos não commitados existentes na pasta principal `C:\Dev\FalloutRemake` (como relatórios de auditoria em `docs/`, `tools/Restore-Asset.ps1` e modificações de ensaio da Anciã) **não** são transportados automaticamente pelo Git para as worktrees. A transferência de artefatos não commitados necessários segue estritamente o protocolo da Seção 6.

---

## 3. Papéis Dinâmicos dos Agentes

Para cada demanda de trabalho, os seguintes papéis são atribuídos explicitamente:

- **Implementador:** Escreve a funcionalidade, cria os testes sintéticos associados na sua worktree isolada e documenta as decisões.
- **Revisor Independente:** Inspeciona o diff da implementação, busca falhas lógicas, quebras de contratos arquiteturais, acoplamentos com Godot e omissões em testes. Obrigatoriamente deve ser um agente diferente do autor principal da implementação.
- **Integrador:** Recebe o diff validado, transfere as mudanças para `D:\Dev\FalloutRemakeIntegracao`, resolve incompatibilidades, executa a suíte completa de testes sintéticos (`TempleInspector`), submete a integração a uma revisão independente e, somente se 100% aprovada, cria o commit local da integração.
- **Revisor Final:** Examina o conjunto integrado na worktree de integração antes do merge ou avanço na base principal.

### Critérios de Alocação Dinâmica:
- **Disponibilidade e Janela de Contexto:** Agentes com cotas livres e contexto desobstruído assumem tarefas de maior fôlego.
- **Adequação Técnica:**
  - *Codex:* Especialmente ágil para implementação de regras de domínio C#, parsers binários e lógica desacoplada.
  - *Claude:* Especialmente detalhista em consistência arquitetural, revisão crítica, análise visual e contratos de apresentação.
  - *Gemini Argon:* Especialmente eficaz para automação, orquestração, scripts de apoio e integrações transversais.
- **Rotatividade:** O integrador de uma tarefa pode ser o implementador da tarefa seguinte, e o implementador anterior atua como revisor independente.

---

## 4. Política de Git e Regras de Commit

### 4.1. Proibição de Commits por Implementadores e Revisores
- Os agentes atuando como implementadores ou revisores **NÃO DEVEM** criar commits nas suas branches de trabalho (`agent/codex-work`, `agent/claude-work`).
- O trabalho é mantido como alterações locais de trabalho (*working tree changes*) e compartilhado via patches/diffs ou listagem explícita de arquivos.

### 4.2. Autorização Exclusiva do Agente Integrador
Apenas o agente designado formalmente como **Integrador** daquela tarefa possui autorização para criar **um commit local de integração** na branch `integration/work`.

#### Condições Obrigatórias para a Criação do Commit Local:
1. **Integração Concluída:** Todos os arquivos novos e modificados foram posicionados e verificados em `D:\Dev\FalloutRemakeIntegracao`.
2. **Resolução de Conflitos:** Nenhum marcador de conflito ou colisão de nomes/namespaces remanescente.
3. **Critérios de Aceitação Satisfeitos:** Todos os requisitos da tarefa foram comprovados factualmente.
4. **Testes Aprovados:** A suíte sintética (`dotnet run --project tools/TempleInspector -- --self-test`) passa com 100% de sucesso sem regressão nos 335+ testes existentes.
5. **Revisão Independente Aprovada:** Pelo menos um revisor independente aprovou formalmente o diff final. Caso o próprio integrador tenha escrito a maior parte do código, a revisão independente prévia por outro agente é indispensável.
6. **Inspeção de Diff Limpo:** O `git diff` final contém estritamente os arquivos daquela feature, sem inclusão acidental de arquivos temporários, logs ou lixo de compilação.

### 4.3. Atualização da Base Principal (`C:\Dev\FalloutRemake`)
Após a criação e validação do commit na branch `integration/work`, o integrador poderá sincronizar a base principal:
- Utilizar avanço rápido (*fast-forward*) quando as árvores permitirem.
- Se houver alterações locais não salvas na base principal que entrem em conflito, **interromper a operação imediatamente** e notificar o usuário para decidir o destino das alterações locais.
- **É terminantemente proibido** utilizar `git reset --hard`, `git clean -fdx` ou comandos destrutivos para forçar a sincronização da base principal.

### 4.4. Proibição Absoluta de Push
- Sob nenhuma hipótese qualquer agente executará `git push`, criará remotes externos ou publicará branches no GitHub.
- Todos os commits permanecem 100% locais no ambiente da máquina do usuário.

---

## 5. Prevenção de Conflitos em Desenvolvimento Paralelo

Para permitir que implementações ocorram em paralelo sem quebras:

1. **Separação por Camadas:**
   - Recursos de domínio puros residem em `src/Import`, `src/Geometry` e `src/Runtime`.
   - Recursos de apresentação residem em `src/Preview`.
   - Modificações em arquivos centrais compartilhados (como `WorldSession.cs`, `TemplePreview.cs` ou `Program.cs`) devem ter seus contratos ou métodos novos acordados previamente via ai-memory.
2. **Definição de Contratos Primeiro:**
   - Se duas features dependem da mesma classe de domínio (por exemplo, `CombatantState`), o contrato público (propriedades, assinaturas de métodos e tipos) deve ser definido e validado em ensaio sintético antes que ambos os agentes expandam a lógica em paralelo.
3. **Escopos de Arquivo Exclusivos:**
   - Cada feature deve criar seus próprios arquivos novos temáticos (ex.: `src/Runtime/CombatRules.cs`, `tools/TempleInspector/CombatTests.cs`) em vez de inflar arquivos monolíticos existentes.

---

## 6. Protocolo de Transferência de Alterações sem Commits

Como o implementador não realiza commits, a passagem de bastão para o integrador segue este protocolo rigoroso:

1. **Manifesto de Entrega do Implementador:**
   O implementador emite um relatório com:
   - Lista exata de arquivos criados (`A`), modificados (`M`) e removidos (`D`).
   - O diff completo das alterações em relação ao commit base `a66f31c`.
   - O resultado verificado dos testes executados na sua worktree.
2. **Verificação pelo Integrador:**
   O integrador inspeciona o manifesto na sua sessão, confirma que a base utilizada foi `a66f31c` e que não há dependências cíclicas não atendidas.
3. **Aplicação na Área de Integração:**
   O integrador copia cirurgicamente apenas os arquivos listados para `D:\Dev\FalloutRemakeIntegracao` (usando comandos de cópia explícitos por caminho de arquivo, sem clonar árvores cegas).
4. **Validação e Regressão:**
   Na pasta `D:\Dev\FalloutRemakeIntegracao`, o integrador executa a compilação e a suíte completa de testes sintéticos.
5. **Revisão e Commit:**
   Após a aprovação do revisor independente, o integrador efetua o commit local único daquela integração.

---

## 7. Coordenação e Compartilhamento de Contexto via ai-memory MCP

Todos os agentes utilizam a mesma instância local do **ai-memory MCP**:
- **Escopo Central:** `project: FalloutRemake`, `workspace: default`.
- **Servidor Local:** `http://127.0.0.1:49374/mcp`.

### Configuração por Ambiente:
- **Gemini Argon (Antigravity):** Conexão direta via plugin MCP nativo do Antigravity.
- **Claude (Claude Desktop / Extension):** Configurado em `C:\Users\VITOR\.claude.json` através do `ai-memory.exe mcp-bridge --server-url http://127.0.0.1:49374/mcp`.
- **Codex:** Configurado em `C:\Users\VITOR\.codex\config.toml` sob `[mcp_servers.ai-memory]` apontando para `http://127.0.0.1:49374/mcp`.

### Protocolo de Registro na Memória:
1. **Ao Iniciar Tarefa:** Registrar a atribuição da tarefa na página `tasks/<nome-da-tarefa>.md`, indicando o objetivo, o Implementador, o Integrador e o Revisor Independente.
2. **Durante a Execução:** Registrar decisões de design, novos contratos ou limitações encontradas em `decisions/<topico>.md`.
3. **Ao Concluir a Integração:** Atualizar a página da tarefa com o status formal, testes aprovados, caminhos dos arquivos integrados e o hash do commit local gerado pelo Integrador.

---

## 8. Modelos Reutilizáveis de Prompts Operacionais

### 8.1. Prompt para o Agente Implementador
```markdown
Você está atuando como IMPLEMENTADOR na tarefa: [NOME_DA_TAREFA].
Sua área de trabalho é: [CAMINHO_WORKTREE] (Branch: [NOME_BRANCH]).
Base utilizada: a66f31c.

Diretrizes obrigatórias:
1. Desenvolva estritamente o escopo acordado, sem acoplamento indevido com o Godot.
2. Escreva testes sintéticos desacoplados em tools/TempleInspector para validar a nova lógica.
3. Execute `dotnet run --project tools/TempleInspector/TempleInspector.csproj -- --self-test` e certifique-se de que todos os testes passem.
4. NÃO CRIE COMMITS. Mantenha todas as alterações no working tree.
5. Ao concluir, apresente a lista de arquivos criados/modificados, o diff completo e os resultados dos testes para o Revisor Independente e o Integrador.
```

### 8.2. Prompt para o Agente Revisor Independente
```markdown
Você está atuando como REVISOR INDEPENDENTE na tarefa: [NOME_DA_TAREFA].
Implementação fornecida por: [AGENTE_IMPLEMENTADOR].

Analise criticamente o diff e as evidências fornecidas:
1. Verifique se as regras do Fallout 2 original foram respeitadas e se há regressões de lógica.
2. Verifique se o código de domínio em src/Runtime ou src/Geometry permanece 100% desacoplado do Godot.
3. Avalie se a cobertura de testes sintéticos é suficiente para cobrir os critérios de aceitação e casos de borda.
4. Identifique se houve alterações fora do escopo autorizado.
5. Emita seu parecer formal: APROVADO, APROVADO COM RESSALVAS ou REJEITADO (com justificativa técnica e apontamento de linhas).
```

### 8.3. Prompt para o Agente Integrador
```markdown
Você está atuando como INTEGRADOR na tarefa: [NOME_DA_TAREFA].
Sua área de integração é: D:\Dev\FalloutRemakeIntegracao (Branch: integration/work).

Procedimento obrigatório:
1. Transfira cirurgicamente os arquivos validados pelo Implementador e aprovados pelo Revisor Independente para D:\Dev\FalloutRemakeIntegracao.
2. Compile o projeto e execute a suíte sintética de testes:
   dotnet run --project "D:\Dev\FalloutRemakeIntegracao\tools\TempleInspector\TempleInspector.csproj" -- --self-test
3. Inspecione o `git diff` final para garantir que contém apenas as alterações aprovadas da feature.
4. SE E SOMENTE SE todos os testes passarem e a revisão independente estiver aprovada, crie UM COMMIT LOCAL de integração:
   git commit -m "[INTEGRATION] Implementação de [NOME_DA_TAREFA]"
5. Registre a conclusão no ai-memory MCP e apresente o relatório final ao usuário.
6. NUNCA execute git push.
```
