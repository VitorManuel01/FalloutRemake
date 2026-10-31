# FalloutRemake — Matriz de Rastreabilidade de Requisitos, Tarefas e Testes

> **Status:** Matriz Canônica de Rastreabilidade  
> **Data de Elaboração:** 28 de Outubro de 2026  
> **Autor da Análise:** Gemini Flash 3.8 (Agente Solo)  
> **Base de Código:** Commit `a66f31c`  
> **Documento Principal:** [gameplay-combat-roadmap.md](file:///c:/Dev/FalloutRemake/docs/roadmap/gameplay-combat-roadmap.md)  
> **Backlog Associado:** [parallel-work-packages.md](file:///c:/Dev/FalloutRemake/docs/roadmap/parallel-work-packages.md)

---

## 1. Convenção de Identificadores e Níveis de Prioridade

### Convenção de IDs:
- `NARR-###`: Continuidade de Diálogos e Narrativa (Frente A).
- `COM-###`: Núcleo de Combate e Regras Básicas (Frente B).
- `COV-###`: Cobertura Posicional e Linha de Tiro.
- `AIM-###`: Modos de Ataque, Mira por Região e Rajadas.
- `REA-###`: Overwatch e Ataques de Reação.
- `ARM-###`: Armas, Armaduras, Munições e Cálculos de DT/DR.
- `AI-###`: Inteligência Artificial Tática (Utility AI).
- `CMP-###`: Companions e Controle Direto de Grupo.
- `UI-###`: Interface Tática, Timeline e Pré-Visualizações.
- `SAV-###`: Persistência, Checkpoints e Desfazer Movimento.
- `QOL-###`: Qualidade de Vida, Gamepad e Acessibilidade.
- `ARCH-###`: Arquitetura e Contratos Desacoplados.
- `TEST-###`: Testes Sintéticos e Validação In-Engine.

### Classificação de Prioridade:
- **P0 — Fundação:** Indispensável para estabelecer o sistema básico ou destravar trabalho subsequente.
- **P1 — Essencial:** Central para a experiência jogável modernizada.
- **P2 — Avançado:** Depende de fundações anteriores para expansão de escopo.
- **P3 — Opcional / Polimento:** Refinamento ergonômico, cosmético ou melhoria futura.

---

## 2. Matriz Completa de Rastreabilidade

| ID Requisito | Nome do Requisito | Prioridade | Fase | Pacote de Trabalho | Critério de Aceitação | Casos de Teste Associados |
| :--- | :--- | :---: | :---: | :--- | :--- | :--- |
| **NARR-001** | Ensaio Interativo de Conversa Repetida da Anciã | **P0** | Fase 1 | `CONT-001` | AC-NARR-01: Exibir Node004 e opções após primeiro diálogo sem mutar o mapa | `ArroyoElderConsultationChecks.cs` |
| **NARR-002** | Aplicação Transacional dos Efeitos à Sessão | **P0** | Fase 1 | `CONT-002` | AC-NARR-02: Gravar GVAR 531=1 e reação auditada em `WorldSession` | `ArroyoElderStateTests.cs` |
| **SAV-003** | Persistência do Diálogo da Anciã no Save v10 | **P0** | Fase 1 | `CONT-003` | AC-SAV-03: Save/load retém estado da Anciã com migração v1-v9 | `CheckpointTests.cs` |
| **ARCH-001** | Contrato Desacoplado `ICombatSession` em C# Puro | **P0** | Fase 2 | `ARCH-001` | AC-ARCH-01: Interfaces puras .NET 8 sem dependência do namespace Godot | Compilação `TempleInspector` |
| **COM-001** | Modelo de Estado Mutável do Combatente (`CombatantState`) | **P0** | Fase 2 | `COM-001` | AC-COM-01: Instanciar combatente com HP, AP, posições e status mutáveis | `CombatantStatsTests.cs` |
| **COM-002** | Derivação de Atributos Secundários de Combate | **P0** | Fase 2 | `COM-001` | AC-COM-02: SPECIAL gera MaxHP, MaxAP, AC, Sequence e Melee Dmg corretos | `CombatantStatsTests.cs` |
| **ARM-001** | Decodificação de Campos de Armas no Parser PRO | **P0** | Fase 2 | `ARM-001` | AC-ARM-01: Extrair dano min/max, alcance, AP e ammo PID de arquivos PRO | `ItemCombatPrototypeTests.cs` |
| **ARM-002** | Decodificação de Armaduras (DT/DR) e Munições | **P0** | Fase 2 | `ARM-001` | AC-ARM-02: Extrair DT/DR de 7 tipos e modificadores de AC/DR de munições | `ItemCombatPrototypeTests.cs` |
| **COM-003** | Ordem de Iniciativa Baseada em Sequence | **P0** | Fase 2 | `COM-002` | AC-COM-03: Turnos ordenados por Sequence com desempate determinístico | `CombatTurnTests.cs` |
| **COM-004** | Movimentação Tática com Consumo de 1 AP/Hex | **P0** | Fase 2 | `COM-002` | AC-COM-04: Caminhada consome 1 AP/hex; para se AP acabar | `CombatTurnTests.cs` |
| **COM-005** | Cálculo Clássico de Chance de Acerto (5%–95%) | **P0** | Fase 2 | `COM-003` | AC-COM-05: ToHit considera perícia, arma, AC, distância e luz; clamped 5-95 | `CombatDamageTests.cs` |
| **COM-006** | Dedução de Dano Pós-Armadura (DT e DR %) | **P0** | Fase 2 | `COM-003` | AC-COM-06: Dano deduz DT e reduz por DR; 0 HP dispara morte | `CombatDamageTests.cs` |
| **COM-007** | Ciclo de Duelo 1v1 no Godot (Formiga Gigante) | **P0** | Fase 3 | `COM-004` | AC-COM-07: Início, troca de turnos, morte da formiga e concessão de 25 XP | `tools/Validate-Temple.ps1` |
| **COV-001** | Cobertura Posicional Meia e Total Direcional | **P1** | Fase 4 | `COV-001` | AC-COV-01: Meia (-20% ToHit/+10 AC); Total (-40% ToHit ou bloqueio de LOS) | `CombatCoverTests.cs` |
| **UI-001** | Pré-Visualização Detalhada de Ações no HUD | **P1** | Fase 4 | `UI-001` | AC-UI-01: Exibir decomposição de ToHit, AP gasto e dano esperado pós-DT/DR | `ActionPreviewTests.cs` |
| **UI-002** | Timeline de Iniciativa e Status de Combatentes | **P1** | Fase 4 | `UI-001` | AC-UI-02: Barra superior com retratos ordenados, HP e condições ativas | Validação Headless |
| **AIM-001** | Tiros Mirados por Região Corporal (Targeted) | **P1** | Fase 5 | `AIM-001` | AC-AIM-01: Penalidades por membro e críticos (olhos 3x, cabeça 2.5x, virilha 2x)| `TargetedShotsTests.cs` |
| **AIM-002** | Disparo em Rajada (Burst Fire) com Dispersão | **P1** | Fase 5 | `AIM-002` | AC-AIM-02: Cone de tiro com dispersão de projéteis e risco de fogo amigo | `BurstFireTests.cs` |
| **AIM-003** | Alerta e Trava de Confirmação de Fogo Amigo | **P1** | Fase 5 | `AIM-002` | AC-AIM-03: Aviso na UI e confirmação obrigatória se aliado estiver no cone | `BurstFireTests.cs` |
| **REA-001** | Postura de Overwatch e Ataques de Reação | **P1** | Fase 5 | `REA-001` | AC-REA-01: Reserva de AP; interrupção de inimigo ao entrar no cone vigiado | `OverwatchTests.cs` |
| **CMP-001** | Controle Direto de Companions por Padrão | **P1** | Fase 6 | `CMP-001` | AC-CMP-01: Alternância manual de controle, inventário individual e ações | `PartySessionTests.cs` |
| **AI-001** | Utility AI Baseada em Pontuação de Candidatos | **P1** | Fase 6 | `AI-001` | AC-AI-01: IA avalia (pos, ação) escolhendo maior pontuação utilitária | `UtilityAiTests.cs` |
| **AI-002** | Papéis Táticos (Atirador, Linha de Frente, Suporte)| **P2** | Fase 6 | `AI-001` | AC-AI-02: Pesos de IA alteram comportamento conforme papel configurado | `UtilityAiTests.cs` |
| **AI-003** | Painel de Depuração de Decisões de IA (F11) | **P2** | Fase 6 | `AI-001` | AC-AI-03: Exibir ranking de candidatos e decomposição de termos de utilidade | Validação In-Engine |
| **SAV-001** | Salvamento Atômico durante o Combate | **P1** | Fase 7 | `SAV-001` | AC-SAV-01: Salvar rodada, turnos, APs restantes e estado determinístico do RNG | `CombatSaveTests.cs` |
| **SAV-002** | Desfazer Movimento (Movement Undo) Restrito | **P1** | Fase 7 | `SAV-001` | AC-SAV-02: Desfazer restaura AP antes de ataques, danos ou névoa revelada | `MovementUndoTests.cs` |
| **QOL-001** | Aceleração de Animação e Omissão Fora do FOV | **P2** | Fase 7 | `QOL-001` | AC-QOL-01: Fast-forward 4x e resolução matemática de turnos invisíveis | `CombatPacingTests.cs` |
| **QOL-002** | Suporte Integral a Gamepad com Snap Hexagonal | **P2** | Fase 7 | `QOL-001` | AC-QOL-02: Navegação tática no grid, seleção de alvos e mira via analógico | Validação In-Engine |
| **QOL-003** | Destaque de Objetos Interagíveis (Tab) e Saque | **P2** | Fase 7 | `QOL-001` | AC-QOL-03: Contorno brilhante de itens/portas e saque rápido em área | Validação In-Engine |

---

## 3. Resumo Estatístico de Rastreabilidade

- **Total de Requisitos Mapeados:** 29 requisitos formais.
  - *P0 — Fundação:* 13 requisitos (45%).
  - *P1 — Essencial:* 10 requisitos (34%).
  - *P2 — Avançado:* 6 requisitos (21%).
- **Total de Pacotes de Trabalho (Tasks):** 16 pacotes delimitados.
- **Total de Decisões de Design Registradas:** 15 decisões formais (`DEC-001` a `DEC-015`).
- **Rastreabilidade Bidirecional:** 100% dos requisitos possuem pacotes de trabalho associados, critérios de aceitação específicos e casos de teste identificados.
