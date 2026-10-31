# FalloutRemake — Roadmap Geral de Gameplay, Continuidade e Combate Modernizado

> **Status:** Documento Canônico de Planejamento e Arquitetura  
> **Data de Elaboração:** 28 de Outubro de 2026  
> **Autor da Análise:** Gemini Flash 3.8 (Agente Solo)  
> **Base de Código Verificada:** Commit `a66f31c` (`codex/arroyo-elder-entry-audit`)  
> **Repositório Principal:** `C:\Dev\FalloutRemake`  
> **Acervo Externo de Assets:** `D:\FalloutRemakeAssets`  
> **Ambientes de Desenvolvimento Isolados:** `D:\Dev\FalloutRemakeCodex`, `C:\Dev\FalloutRemakeClaude`, `D:\Dev\FalloutRemakeIntegracao`  
> **Documentos Complementares:**
> - [combat-functional-specification.md](file:///c:/Dev/FalloutRemake/docs/roadmap/combat-functional-specification.md)
> - [companions-and-tactical-ai.md](file:///c:/Dev/FalloutRemake/docs/roadmap/companions-and-tactical-ai.md)
> - [tactical-ui-and-qol.md](file:///c:/Dev/FalloutRemake/docs/roadmap/tactical-ui-and-qol.md)
> - [parallel-work-packages.md](file:///c:/Dev/FalloutRemake/docs/roadmap/parallel-work-packages.md)
> - [architecture-and-design-decisions.md](file:///c:/Dev/FalloutRemake/docs/roadmap/architecture-and-design-decisions.md)
> - [requirements-traceability.md](file:///c:/Dev/FalloutRemake/docs/roadmap/requirements-traceability.md)

---

## 1. Resumo Executivo

Este documento consolida o **Roadmap Técnico Geral** do projeto `FalloutRemake`, abrangendo **todas as frentes de desenvolvimento ativas, pendentes e futuras**. A estratégia adotada concilia duas necessidades vitais:

1. **Continuidade das Features Existentes (Frente A):** Retomada imediata dos sistemas iniciados (diálogos, interação com a Anciã de Arroyo, persistência de variáveis e scripts recuperados do Fallout 2 original).
2. **Combate Tático Modernizado (Frente B):** Introdução estruturada e progressiva de um sistema de combate em turnos no grid hexagonal que respeite os fundamentos originais de Fallout 2 (SPECIAL, habilidades, AP, armas, armaduras, DT/DR e críticos por região corporal) e incorpore melhorias de qualidade de vida e referências táticas modernas (pré-visualização de ações, cobertura posicional, overwatch, controle direto de companions e ritmo dinâmico).

A análise foi conduzida de forma autônoma e independente por **Gemini Flash 3.8**, fundamentando cada afirmação em arquivos, classes, métodos e dados inspecionados diretamente no repositório.

---

## 2. Inventário e Estado Real das Frentes do Projeto

Abaixo é apresentada a classificação rigorosa de cada subsistema do projeto, conforme a escala de 6 níveis obrigatória:
1. **Implementado e confirmado:** Existe código ativo e seu comportamento foi verificado em testes.
2. **Dados disponíveis:** Os dados originais foram importados ou podem ser lidos, mas a lógica de uso não está implementada.
3. **Parcialmente implementado:** Existe uma parte funcional, mas faltam componentes essenciais para o fluxo completo.
4. **Documentado, sem implementação confirmada:** Consta em relatórios, mas não possui código ativo verificado.
5. **Ausente:** A funcionalidade necessária ainda não existe no código.
6. **Não verificável:** Não foi possível confirmar por restrições operacionais ou falta de ambiente.

### Tabela Geral de Subsistemas

| Frente / Subsistema | Classificação Real | Evidências no Código / Testes | Descrição do Estado Real |
| :--- | :--- | :--- | :--- |
| **Leitura de Formatos Originais** | **Implementado e confirmado** | `src/Import/DatArchive.cs`, `MapDocument.cs`, `CritterPrototypeData.cs`, `IntProgram.cs`, `MapLayoutWriter.cs` | Leitura determinística de DAT, MAP, PRO, GAM, MSG, FRM e opcodes INT. 337 grupos de testes sintéticos aprovados em `TempleInspector`. |
| **Grid Hexagonal e Matemática** | **Implementado e confirmado** | `src/Geometry/FalloutGrid.cs` | 40.000 hexágonos (200x200), conversão afim 3D/hex, cálculo de distância topológica e identificação de 6 vizinhos. |
| **Pathfinding e Navegação** | **Implementado e confirmado** | `src/Geometry/HexNavigation.cs` | BFS com bloqueadores multihex, suporte a 3 elevações (0, 1, 2) e políticas de saída (`StopAtExits`, `ContinueAcrossExits`). |
| **Sessão e Checkpoints (v1 a v9)** | **Implementado e confirmado** | `src/Runtime/WorldSession.cs`, `WorldCheckpoint.cs`, `AtomicFileReplacement.cs` | Persistência multi-mapa em JSON atômico ($\le$ 1 MB) com migração retroativa de versões anteriores e validação estrita. |
| **Portas, Chaves e Lockpick** | **Implementado e confirmado** | `src/Runtime/TempleDoorAdapter.cs`, `DoorSession.cs`, `PrototypeCharacter.cs` | Abertura/fechamento, arrombamento (Lockpick 9) com 25 XP únicos e porta 100 da vila desbloqueável via tecla U. |
| **Explosivos e Barreira** | **Implementado e confirmado** | `src/Runtime/ExplosiveTimer.cs`, `TempleBarrierProjection.cs` | Dinamite com timer lógico (+1s/+10s), detonação atômica, remoção do bloqueador da barreira do Templo e abertura do caminho para Elevação 2. |
| **Inventário, Peso e Contêineres** | **Implementado e confirmado** | `src/Runtime/ItemTransferSession.cs`, `CarryWeight.cs`, `ItemOriginRoute.cs` | Coleta rápida ("Take All") e devolução integral ("Return All") com limite estrito de peso por Força. Itens rastreados por origem. |
| **Speech do Templo (Cameron)** | **Implementado e confirmado** | `src/Runtime/TempleSpeechDraft.cs`, `WorldTempleSpeech.cs`, `TempleNpcDeparture.cs` | Rota diplomática de 5 escolhas (`actemvil.int`), partida física do NPC e abertura da porta para a vila de Arroyo. |
| **Transição para Arroyo e Recompensas**| **Implementado e confirmado** | `src/Runtime/TempleVillageRoute.cs`, `ArroyoRewardProfile.cs` | Saída pela porta 100 para o mapa 4 (`arvillag.map`), concessão única de 600 XP (acumulando 625 XP totais), Karma 15 e Reputação 65 persistidos no checkpoint v9. |
| **Aproximação da Anciã (Elder)** | **Implementado e confirmado** | `src/Preview/ArroyoElderApproachChecks.cs`, `ArroyoElderApproach.cs` | Rota guiada do personagem até o hex adjacente à Anciã (Tile 18915) na Elevação 0 de Arroyo. |
| **Consulta de Diálogo da Anciã** | **Parcialmente implementado** | `src/Runtime/ArroyoElderConsultation.cs`, `src/Preview/ArroyoElderConsultationChecks.cs` | Ensaio desacoplado funcional exibindo mensagens originais de `ahelder.msg` em janela de texto. Desconectado da partida viva (`AppliedToWorld=false`). |
| **Auditoria da Conversa Repetida** | **Parcialmente implementado** | `tools/TempleInspector/ArroyoElderRepeatAudit.cs`, `ArroyoElderStateTests.cs` | Lógica auditada em testes de domínio C# para `Node004`, `Node013` e `Node999`. Pendente de integração na interface e no runtime. |
| **Aplicação de Efeitos de Diálogo** | **Parcialmente implementado** | `src/Runtime/ArroyoElderStateProfile.cs` | Efeitos de reação (-10, -20) e variáveis (GVAR_TALKED_TO_ELDER 531) projetados em memória, mas desabilitados na partida viva (`RuntimeDialogueEnabled=false`). |
| **Dados de Criaturas (Critters)** | **Dados disponíveis** | `src/Import/CritterPrototypeData.cs` | Leitura de 35 base stats, 35 bonus stats e 18 skills de arquivos `.pro`. Criaturas do Templo confirmadas: Formiga Gigante (PID 382), Escorpião (PID 5) e Dumar/Cameron (PID 274). |
| **Dados de Armas, Armaduras e Munições** | **Dados disponíveis** | `src/Import/PrototypeCatalog.cs` | Validação de tamanho dos arquivos `.pro` de itens. Decodificação detalhada dos campos específicos de combate (dano, DT, DR, alcance, AP) ainda não implementada no parser. |
| **Sistema de Combate em Turnos** | **Ausente** | *(Nenhum código ativo)* | Inexistente no runtime. Sem pontos de ação (AP), ordem de iniciativa, cálculo de acerto/dano, cobertura ou overwatch. |
| **Companions e IA Tática** | **Ausente** | *(Nenhum código ativo)* | Inexistente. Sem recrutamento, sem controle direto de membros do grupo e sem IA utilitária. |
| **Apresentação de Assets 3D (Natureza)**| **Implementado e confirmado** | `src/Preview/NatureModels.cs`, `NaturePicking.cs` | Modelos GLB de rochas e árvores integrados na cena de teste com picking triangular 3D. |
| **Apresentação de Demais Assets 3D** | **Parcialmente implementado** | Pastas em `Assets/Architecture`, `Containers`, `Furniture`, `Props` | Modelos GLB otimizados disponíveis no projeto, aguardando mapeamento na cena para substituir primitivas provisórias. |
| **Build Mobile / Android** | **Não verificável** | `Fallout Remake.csproj` | Configuração condicional presente, mas sem automação de build ou validação em dispositivo real. |

---

## 3. Próximas Tarefas de Continuidade (Frente A)

Para garantir que o trabalho existente na Aldeia de Arroyo e no Templo não seja interrompido, a **Frente A** possui precedência operacional imediata.

### 3.1. O que já está funcional e deve ser preservado
1. **Aproximação da Anciã (`ArroyoElderApproach.cs`):** Caminhada do jogador a partir do ponto de entrada da vila até o hex adjacente à Anciã (Tile 18915), com detecção de bloqueios e cancelamento seguro de rota.
2. **Consulta Textual do Primeiro Diálogo (`ArroyoElderConsultation.cs`):** Janela modal de consulta que carrega os textos originais em inglês de `ahelder.msg` (`Node001`, `Node002`, `Node006`, `Node008`, `Node013`, `Node999`).
3. **Auditoria de Fontes e Refutação de Modificações (`ArroyoElderAudit.cs`):** Verificação rigorosa contra alteração em `scripts/ahelder.int`, `data/vault13.gam` e protótipos de criaturas.
4. **Recompensa do Templo e Checkpoint v9 (`WorldCheckpoint.cs`):** Persistência transacional dos 625 XP acumulados, Karma 15 e Reputação de Arroyo 65.

### 3.2. O que foi validado apenas em ensaio isolado
1. **Projeção de Estados do Diálogo (`ArroyoElderStateProfile.cs`):** Ajustes de reação do jogador perante a Anciã (base 32, redução de -10 em caminhos rudes e -20 em `Node013`).
2. **Auditoria de Conversa Repetida (`ArroyoElderRepeatAudit.cs`):** Verificação de instrução bytecode e opções de diálogo de `Node004`, demonstrando que a opção de área 11 depende de `metarule(17, 11)` (área visitada no mapa-múndi).

### 3.3. O que falta testar e integrar na partida viva
1. **Ensaio Interativo da Conversa Repetida no Godot:** Permitir que o jogador, após concluir o primeiro diálogo na cena `TemplePreview`, consiga abrir novamente a janela e navegar pelas opções de `Node004` (conversa repetida).
2. **Integração Controlada dos Efeitos à Sessão (`CommitElderDialogue`):**
   - Transicionar de `AppliedToWorld = false` para um commit explícito na máquina de estados `WorldSession`.
   - Gravar no bloco de variáveis globais: `GVAR_TALKED_TO_ELDER (531) = 1`.
   - Gravar as variáveis locais do script da Anciã (`scripts/ahelder.int`): Local 0 (reação), Local 1 (banda de reação), Local 2 (já falou) e Local 3 (ajuste acumulado).
3. **Persistência no Checkpoint (Schema v10):**
   - Estender `WorldCheckpoint` para suportar `SavedElderState`, contendo o status da conversa e as variáveis auditadas, mantendo migração compatível a partir do schema v9.

### 3.4. Sequência Recomendada de Execução da Frente A
```mermaid
flowchart TD
    A["Tarefa CONT-001\nEnsaio Interativo de Conversa Repetida na UI"] --> B["Tarefa CONT-002\nAplicação Controlada dos Efeitos à WorldSession"]
    B --> C["Tarefa CONT-003\nMigração de Checkpoint para Schema v10"]
    C --> D["Tarefa CONT-004\nValidação Headless Completa (Validate-Temple.ps1)"]
```

---

## 4. Visão do Combate Tático Modernizado (Frente B)

A **Frente B** introduz o combate por turnos no grid hexagonal com identidade autêntica de RPG tático isométrico e melhorias ergonômicas contemporâneas.

### 4.1. Fundamentos Clássicos Preservados
- **Sistema SPECIAL Integral:** Todos os 7 atributos primários influenciam estatísticas secundárias de combate (HP, AP, AC, Sequence, Melee Damage, Critical Chance e Resistências).
- **Consumo Rigoroso de Action Points (AP):** Cada ação (movimento, ataque desarmado, golpe de lança, troca de item) consome APs finitos.
- **Grid Hexagonal Determinístico:** O posicionamento e o alcance respeitam o espaço de 40.000 hexes de `FalloutGrid.cs`.
- **Armaduras e Tipos de Dano:** Mecânica de Limiar de Dano (Damage Threshold - DT) e Resistência a Dano (Damage Resistance - DR %) para 7 tipos de dano (Normal, Laser, Fogo, Plasma, Elétrico, EMP e Explosão).
- **Tiros Mirados por Região Corporal:** Olhos, Cabeça, Virilha, Braços, Pernas e Tronco com penalidades de acerto e efeitos críticos específicos.

### 4.2. Modernizações Táticas
- **Pré-Visualização Completa de Ações (Action Preview):** Antes de confirmar qualquer ataque ou movimento, a UI detalha a probabilidade exata de acerto, custos de AP, decomposição de modificadores, alcance efetivo, previsão de dano pós-armadura e riscos de fogo amigo.
- **Cobertura Posicional Direcional (Cover System):** Meia Cobertura (-20% acerto) e Cobertura Total (-40% acerto ou quebra de linha de visão), calculada dinamicamente entre o atacante e o alvo através dos obstáculos em hexes adjacentes.
- **Overwatch e Ataques de Reação:** Possibilidade de reservar APs no encerramento do turno para disparar contra inimigos que atravessem a zona vigiada.
- **Controle Direto de Companions por Padrão (Full Party Control):** Controle manual de aliados como padrão, com IA tática utilitária (Utility AI) configurável como modo opcional.
- **Ritmo e Qualidade de Vida (QoL):** Aceleração de animações de inimigos fora de visão, desfazer movimento (*Undo Move*) antes de ações irreversíveis e suporte de primeira classe a Gamepads.

### 4.3. Primeira Fatia Vertical de Combate
Para validar o ciclo de combate sem depender de toda a complexidade de companions, armas automáticas ou utility AI, define-se a menor fatia executável:
- **Cenário:** Templo das Provações — Sala 1 da Elevação 0 (`maps/arcaves.map`).
- **Encontro:** **Duelo 1v1 contra Formiga Gigante** (Giant Ant, PID 382 / `0x0100017E`).
  - *Dados reais da Formiga Gigante:* 18 HP, 7 AP, AC 4, Sequence 50, Unarmed 25%, DT 0, DR 0%, Recompensa de 25 XP.
  - *Dados do Jogador:* Perfil neutro com Força 5, Percepção 5, Resistência 5, Carisma 5, Inteligência 5, Agilidade 5, Sorte 5 $\to$ 30 HP, 7 AP, AC 5, Sequence 10, Unarmed / Lança.
- **Escopo do Ciclo:**
  1. Detecção mútua e transição de exploração para modo de combate.
  2. Rolagem de iniciativa por Sequence (Formiga com 50 vs Jogador com 10 $\to$ Formiga inicia).
  3. Consumo de 1 AP por hexágono percorrido.
  4. Ataque simples (mordida da formiga / soco ou estocada do jogador).
  5. Cálculo de acerto percentual, dedução de DT/DR e redução de HP.
  6. Morte da criatura ao zerar o HP, concessão de 25 XP e encerramento do modo de combate.
  7. Retorno fluído ao estado de exploração com persistência no mapa.

---

## 5. Fases do Roadmap Integrado

```mermaid
flowchart LR
    F0["Fase 0\nLinha de Base & Worktrees"] --> F1["Fase 1\nContinuidade Arroyo (Frente A)"]
    F0 --> F2["Fase 2\nNúcleo de Domínio C# (Frente B)"]
    F1 --> F3["Fase 3\nPrimeiro Combate Godot (1v1 Ant)"]
    F2 --> F3
    F3 --> F4["Fase 4\nPré-Visualização & Cobertura"]
    F4 --> F5["Fase 5\nMira, Rajadas & Overwatch"]
    F5 --> F6["Fase 6\nCompanions & Utility AI"]
    F6 --> F7["Fase 7\nPersistência, Gamepad & QoL"]
    F7 --> F8["Fase 8\nPolimento & Expansão"]
```

### Detalhamento das Fases

#### Fase 0: Linha de Base e Infraestrutura Paralela *(Concluída)*
- Verificação do commit base `a66f31c`.
- Criação e validação das 3 Git worktrees (`D:\Dev\FalloutRemakeCodex`, `C:\Dev\FalloutRemakeClaude`, `D:\Dev\FalloutRemakeIntegracao`).
- Formalização do guia de governança (`docs/development/parallel-agent-workflow.md`).
- Confirmação dos 337 grupos de testes sintéticos aprovados.

#### Fase 1: Continuidade de Arroyo e Estabilização de Contratos (Frente A)
- **Objetivo:** Concluir o ensaio da conversa repetida da Anciã de Arroyo, aplicar os efeitos do diálogo ao `WorldSession` e evoluir o checkpoint para v10.
- **Critério de Saída:** Diálogo completo e repetido da Anciã jogável em `TemplePreview`, persistido no save sem afetar outros mapas, com testes sintéticos e headless verdes.

#### Fase 2: Núcleo de Domínio do Combate Tático (Frente B)
- **Objetivo:** Desenvolver em C# puro (`src/Runtime/Combat/`) o modelo de combatentes mutáveis, SPECIAL, APs, iniciativa, movimento tático, ataque simples e dano pós-armadura.
- **Critério de Saída:** Suíte de testes sintéticos de combate (`CombatTests.cs`) cobrindo 100% dos cálculos matemáticos de acerto, dano e alternância de turnos.

#### Fase 3: Primeira Fatia Vertical de Combate Integrada ao Godot
- **Objetivo:** Integrar o núcleo de combate à cena de apresentação (`TemplePreview`), permitindo o duelo 1v1 contra a Formiga Gigante no Templo.
- **Critério de Saída:** Jogador entra em combate, troca turnos consumindo APs, mata a formiga, ganha 25 XP e o combate encerra no runtime visual sem erros.

#### Fase 4: Pré-Visualização Tática e Cobertura Posicional
- **Objetivo:** Implementar o sistema geométrico de cobertura (meia/total) e a interface preditiva de ações (chance de acerto, AP restante, dano esperado).
- **Critério de Saída:** Hover do mouse sobre o alvo projeta todas as variáveis de previsão com paridade absoluta contra a resolução real do tiro.

#### Fase 5: Ataques Avançados, Mira, Rajadas e Overwatch
- **Objetivo:** Implementar mira por partes do corpo, cones de dispersão de rajada com fogo amigo, propriedades avançadas de munições e reserva de AP para reação.
- **Critério de Saída:** Tiros nos olhos/cabeça causam penalidades e efeitos críticos adequados; rajadas atingem alvos no cone; overwatch interrompe movimento inimigo.

#### Fase 6: Companions e Utility AI
- **Objetivo:** Sistema de companions com controle direto por padrão, inventários próprios, loadouts e IA tática utilitária opcional orientada a papéis.
- **Critério de Saída:** Companion pode ser comandado diretamente ou agir por IA evitando fogo amigo contra aliados.

#### Fase 7: Persistência de Combate, Controles e Qualidade de Vida
- **Objetivo:** Salvamento atômico durante o combate, desfazer movimento seguro, suporte nativo a Gamepad e acelerador de animações.
- **Critério de Saída:** Salvar e carregar no meio do turno restaura exatamente os APs e posições; movimentação cancelável antes de atirar.

#### Fase 8: Refinamento, Balanceamento e Expansão
- **Objetivo:** Ajuste de curvas de dano, ferramentas de depuração em tempo real e expansão dos encontros para todo o Templo e Arroyo.
- **Critério de Saída:** Templo dos Testes totalmente jogável do início ao fim com combate e exploração fluidos.

---

## 6. Governança e Coordenação do Desenvolvimento Paralelo

O desenvolvimento paralelo das Frentes A e B é viabilizado pelo forte desacoplamento arquitetural do projeto:

1. **Isolamento de Código:**
   - **Frente A (Continuidade):** Atua exclusivamente em `src/Runtime/ArroyoElder*`, `src/Preview/ArroyoElder*` e no schema v10 de `WorldCheckpoint.cs`.
   - **Frente B (Combate Núcleo):** Atua em um novo diretório isolado `src/Runtime/Combat/` e em novos testes em `tools/TempleInspector/CombatTests.cs`.
2. **Ponto de Encontro Futuro (Ponto de Integração):**
   - As duas frentes só convergirão na **Fase 3**, quando a interface de combate for acoplada a `WorldSession.cs`.
   - Para evitar conflitos, um contrato abstrato `ICombatSession` será congelado antes da integração no runtime Godot.
3. **Regra de Não Bloqueio:**
   - O desenvolvimento do núcleo de combate em C# puro pode avançar imediatamente em paralelo à continuidade dos diálogos da Anciã, sem que nenhuma frente bloqueie a outra.
